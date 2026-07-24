using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws.Protocol;
using SoulCore.Config;

namespace SoulCore.Adapters.Ws;

/// <summary>
/// Optional outbound WS client to Unreal (:8888 by default).
/// Serializes SoulCore verbs to UE wire frames: plain <c>speak</c> / <c>move_avatar_relative</c> (PlainArgs),
/// and <c>{type:command,payload:{name,args}}</c> for play_animation / look / set_emotion.
/// Connection failures are logged; Host must keep running.
/// </summary>
public sealed class UnrealVerbClientStub : IUnrealVerbClient, IAsyncDisposable
{
    private readonly UnrealBridgeOptions _options;
    private readonly ILogger<UnrealVerbClientStub> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _unsupportedLogged = new(StringComparer.Ordinal);
    private ClientWebSocket? _socket;

    public UnrealVerbClientStub(
        IOptions<UnrealBridgeOptions> options,
        ILogger<UnrealVerbClientStub> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        TargetUrl = string.IsNullOrWhiteSpace(_options.WsUrl)
            ? "ws://127.0.0.1:8888"
            : _options.WsUrl.Trim();
    }

    public bool IsConnected => _socket is { State: WebSocketState.Open };

    public string TargetUrl { get; }

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        if (IsConnected)
            return;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected)
                return;

            _socket?.Dispose();
            _socket = new ClientWebSocket();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.ConnectTimeoutSeconds)));

            try
            {
                await _socket.ConnectAsync(new Uri(TargetUrl), timeout.Token).ConfigureAwait(false);
                _logger.LogInformation("Unreal WS connected: {Url}", TargetUrl);
            }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException
                or OperationCanceledException or UriFormatException or InvalidOperationException)
            {
                _logger.LogWarning(
                    ex,
                    "Unreal WS unavailable at {Url} — verbs will no-op until UE is up (Host continues)",
                    TargetUrl);
                _socket.Dispose();
                _socket = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<bool> SetEmotionAsync(object emotionPayload, CancellationToken cancellationToken = default) =>
        SendVerbAsync(UnrealVerbTypes.SetEmotion, emotionPayload, cancellationToken);

    public Task<bool> SpeakAsync(string text, CancellationToken cancellationToken = default) =>
        SendVerbAsync(UnrealVerbTypes.Speak, new { text }, cancellationToken);

    public Task<bool> PlayAnimationAsync(string animationName, CancellationToken cancellationToken = default) =>
        SendVerbAsync(UnrealVerbTypes.PlayAnimation, new { name = animationName }, cancellationToken);

    public Task<bool> LocoAsync(object locoPayload, CancellationToken cancellationToken = default) =>
        SendVerbAsync(UnrealVerbTypes.Loco, locoPayload, cancellationToken);

    public Task<bool> LookAsync(object lookPayload, CancellationToken cancellationToken = default) =>
        SendVerbAsync(UnrealVerbTypes.Look, lookPayload, cancellationToken);

    private async Task<bool> SendVerbAsync(string type, object? payload, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Unreal verb skipped (bridge disabled): {Type}", type);
            return false;
        }

        var mapped = UeVerbWireMapper.Map(type, payload);
        if (mapped.Kind == UeVerbWireMapper.UeWireMapKind.Unsupported)
        {
            if (_unsupportedLogged.TryAdd(type, 0))
            {
                _logger.LogInformation(
                    "Unreal verb unsupported on UE bridge (no-op, logged once): {Type}",
                    type);
            }

            return false;
        }

        if (string.IsNullOrEmpty(mapped.WireJson))
        {
            _logger.LogDebug("Unreal verb map produced empty wire frame: {Type}", type);
            return false;
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        if (!IsConnected || _socket is null)
        {
            _logger.LogDebug("Unreal verb dropped (not connected): {Type}", type);
            return false;
        }

        var bytes = Encoding.UTF8.GetBytes(mapped.WireJson);

        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Unreal verb sent: soul={Type} ue={UeName} frame={Frame}",
                type,
                mapped.UeCommandName,
                mapped.WireJson.Length > 120 ? mapped.WireJson[..117] + "..." : mapped.WireJson);
            return true;
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Unreal verb send failed: {Type}", type);
            try
            {
                _socket.Dispose();
            }
            catch
            {
                // ignore dispose races
            }

            _socket = null;
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_socket is not null)
            {
                try
                {
                    if (_socket.State == WebSocketState.Open)
                    {
                        await _socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "host dispose",
                            CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // ignore
                }

                _socket.Dispose();
                _socket = null;
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
