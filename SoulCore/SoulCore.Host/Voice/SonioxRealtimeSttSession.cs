using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Host.Voice;

public sealed class SonioxRealtimeSttSessionFactory : IRealtimeSttSessionFactory
{
    private readonly IOptions<VoiceOptions> _options;
    private readonly ILoggerFactory _loggerFactory;

    public SonioxRealtimeSttSessionFactory(IOptions<VoiceOptions> options, ILoggerFactory loggerFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public async Task<IRealtimeSttSession> StartAsync(
        Func<string, bool, CancellationToken, Task> onTranscript,
        CancellationToken cancellationToken = default)
    {
        var session = new SonioxRealtimeSttSession(
            _options.Value,
            onTranscript,
            _loggerFactory.CreateLogger<SonioxRealtimeSttSession>());
        await session.ConnectAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }
}

/// <summary>PROP-21: Call-mode Soniox realtime STT over WebSocket.</summary>
public sealed class SonioxRealtimeSttSession : IRealtimeSttSession
{
    private readonly VoiceOptions _options;
    private readonly Func<string, bool, CancellationToken, Task> _onTranscript;
    private readonly ILogger _logger;
    private readonly ClientWebSocket _ws = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _receiveLoop;
    private bool _started;

    public SonioxRealtimeSttSession(
        VoiceOptions options,
        Func<string, bool, CancellationToken, Task> onTranscript,
        ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _onTranscript = onTranscript ?? throw new ArgumentNullException(nameof(onTranscript));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (!_options.HasApiKey)
            throw new InvalidOperationException("Voice:ApiKey / SONIOX_API_KEY is not configured");

        _ws.Options.SetRequestHeader("Authorization", "Bearer " + _options.ApiKey.Trim());
        await _ws.ConnectAsync(new Uri(_options.SttWsUrl), cancellationToken).ConfigureAwait(false);

        var start = JsonSerializer.Serialize(new
        {
            model = _options.SttRealtimeModel,
            audio_format = "auto",
            language_hints = new[] { "en" },
            enable_endpoint_detection = true
        });
        var bytes = Encoding.UTF8.GetBytes(start);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        _started = true;
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token), CancellationToken.None);
    }

    public async Task SendAudioAsync(ReadOnlyMemory<byte> pcmOrEncoded, CancellationToken cancellationToken = default)
    {
        if (!_started || _ws.State != WebSocketState.Open || pcmOrEncoded.Length == 0)
            return;
        await _ws.SendAsync(pcmOrEncoded, WebSocketMessageType.Binary, true, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task FinishAsync(CancellationToken cancellationToken = default)
    {
        if (_ws.State == WebSocketState.Open)
        {
            // Empty text frame ends the stream per Soniox docs.
            await _ws.SendAsync(ReadOnlyMemory<byte>.Empty, WebSocketMessageType.Text, true, cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            _cts.Cancel();
            if (_receiveLoop is not null)
                await Task.WhenAny(_receiveLoop, Task.Delay(2000, CancellationToken.None)).ConfigureAwait(false);
        }
        catch
        {
            /* ignore */
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await FinishAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            /* ignore */
        }

        _cts.Dispose();
        _ws.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        var finals = new StringBuilder();
        try
        {
            while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                var json = Encoding.UTF8.GetString(ms.ToArray());
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("tokens", out var tokens)
                    || tokens.ValueKind != JsonValueKind.Array)
                    continue;

                var nonFinal = new StringBuilder();
                foreach (var t in tokens.EnumerateArray())
                {
                    var piece = t.TryGetProperty("text", out var tt) ? tt.GetString() ?? "" : "";
                    var isFinal = t.TryGetProperty("is_final", out var f) && f.ValueKind == JsonValueKind.True;
                    if (isFinal)
                        finals.Append(piece);
                    else
                        nonFinal.Append(piece);
                }

                if (finals.Length > 0)
                {
                    var text = finals.ToString().Trim();
                    finals.Clear();
                    if (text.Length > 0)
                        await _onTranscript(text, true, ct).ConfigureAwait(false);
                }
                else if (nonFinal.Length > 0)
                {
                    await _onTranscript(nonFinal.ToString().Trim(), false, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            /* normal */
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Soniox realtime STT receive ended");
        }
    }
}
