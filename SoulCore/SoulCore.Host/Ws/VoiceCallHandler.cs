using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Host.Voice;
using SoulCore.Protocol;

namespace SoulCore.Host.Ws;

/// <summary>
/// PROP-21: Call session — mic → Soniox realtime STT → transcript frames;
/// reply speak path emits voice.speaking + voice.audio for the desk sphere.
/// </summary>
public sealed class VoiceCallHandler
{
    private readonly IRealtimeSttSessionFactory _sttFactory;
    private readonly VoiceSpeakService? _speak;
    private readonly VoiceOptions _options;
    private readonly ILogger<VoiceCallHandler> _logger;
    private readonly ConcurrentDictionary<Guid, CallState> _calls = new();

    public VoiceCallHandler(
        IRealtimeSttSessionFactory sttFactory,
        IOptions<VoiceOptions> options,
        ILogger<VoiceCallHandler> logger,
        Adapters.Ws.IVoiceSpeakService? voiceSpeak = null)
    {
        _sttFactory = sttFactory ?? throw new ArgumentNullException(nameof(sttFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _speak = voiceSpeak as VoiceSpeakService;
    }

    public bool IsInCall(Guid connectionSessionId) => _calls.ContainsKey(connectionSessionId);

    public async Task HandleStartAsync(
        WebSocket socket,
        SoulCoreFrame frame,
        Guid connectionSessionId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.HasApiKey)
        {
            await WsFrameSender.SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new { code = "voice.unavailable", message = "Soniox voice not configured" },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await EndInternalAsync(connectionSessionId, cancellationToken).ConfigureAwait(false);

        var session = await _sttFactory.StartAsync(
            async (text, isFinal, ct) =>
            {
                await WsFrameSender.SendFrameAsync(
                    socket,
                    SoulCoreFrame.Create(
                        SoulCoreFrameTypes.VoiceTranscript,
                        new { text, final = isFinal },
                        id: frame.Id),
                    ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        _calls[connectionSessionId] = new CallState(session);
        await WsFrameSender.SendFrameAsync(
            socket,
            SoulCoreFrame.Create(
                SoulCoreFrameTypes.VoiceSpeaking,
                new { speaking = false, call = true },
                id: frame.Id),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleAudioAsync(
        WebSocket socket,
        SoulCoreFrame frame,
        Guid connectionSessionId,
        CancellationToken cancellationToken)
    {
        if (!_calls.TryGetValue(connectionSessionId, out var state))
        {
            await WsFrameSender.SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new { code = "voice.call.inactive", message = "Call not started" },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        string? b64 = null;
        if (frame.Payload is { } payload
            && payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("audio", out var a)
            && a.ValueKind == JsonValueKind.String)
            b64 = a.GetString();
        if (string.IsNullOrEmpty(b64))
            return;

        try
        {
            var bytes = Convert.FromBase64String(b64);
            await state.Session.SendAudioAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "voice.call.audio decode/send failed");
        }
    }

    public Task HandleEndAsync(
        WebSocket socket,
        SoulCoreFrame frame,
        Guid connectionSessionId,
        CancellationToken cancellationToken)
        => EndInternalAsync(connectionSessionId, cancellationToken);

    /// <summary>Stream Soniox TTS to the Call desk (sphere + playback).</summary>
    public async Task StreamReplyAsync(
        WebSocket socket,
        string? correlationId,
        string reply,
        CancellationToken cancellationToken)
    {
        if (_speak is null || string.IsNullOrWhiteSpace(reply))
            return;

        await WsFrameSender.SendFrameAsync(
            socket,
            SoulCoreFrame.Create(
                SoulCoreFrameTypes.VoiceSpeaking,
                new { speaking = true, level = 0.6 },
                id: correlationId),
            cancellationToken).ConfigureAwait(false);

        try
        {
            await _speak.SpeakStreamingAsync(
                reply,
                async (chunk, end, ct) =>
                {
                    if (chunk.Length > 0)
                    {
                        await WsFrameSender.SendFrameAsync(
                            socket,
                            SoulCoreFrame.Create(
                                SoulCoreFrameTypes.VoiceAudio,
                                new
                                {
                                    audio = Convert.ToBase64String(chunk.Span),
                                    format = "pcm_s16le",
                                    sampleRate = _options.TtsSampleRate,
                                    end = false
                                },
                                id: correlationId),
                            ct).ConfigureAwait(false);
                    }

                    if (end)
                    {
                        await WsFrameSender.SendFrameAsync(
                            socket,
                            SoulCoreFrame.Create(
                                SoulCoreFrameTypes.VoiceAudio,
                                new { audio = "", format = "pcm_s16le", sampleRate = _options.TtsSampleRate, end = true },
                                id: correlationId),
                            ct).ConfigureAwait(false);
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await WsFrameSender.SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.VoiceSpeaking,
                    new { speaking = false, level = 0 },
                    id: correlationId),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EndInternalAsync(Guid connectionSessionId, CancellationToken cancellationToken)
    {
        if (_calls.TryRemove(connectionSessionId, out var state))
            await state.Session.DisposeAsync().ConfigureAwait(false);
    }

    private sealed record CallState(IRealtimeSttSession Session);
}
