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
/// Also implements <see cref="IUnrealEyeCaptureClient"/> (request/response <c>eye_capture</c>)
/// and <see cref="IUnrealCallCameraClient"/> (waist-up <c>call_capture</c>).
/// </summary>
public sealed class UnrealVerbClientStub : IUnrealVerbClient, IUnrealEyeCaptureClient, IUnrealCallCameraClient, IAsyncDisposable
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
            ? "ws://house-victoria:8888"
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
        SpeakAsync(new { text }, cancellationToken);

    public Task<bool> SpeakAsync(object speakPayload, CancellationToken cancellationToken = default) =>
        SendVerbAsync(UnrealVerbTypes.Speak, speakPayload, cancellationToken);

    public Task<bool> PlayAnimationAsync(string animationName, CancellationToken cancellationToken = default) =>
        SendVerbAsync(UnrealVerbTypes.PlayAnimation, new { name = animationName }, cancellationToken);

    public Task<bool> LocoAsync(object locoPayload, CancellationToken cancellationToken = default) =>
        SendVerbAsync(UnrealVerbTypes.Loco, locoPayload, cancellationToken);

    public Task<bool> MoveToAsync(object moveToPayload, CancellationToken cancellationToken = default) =>
        SendVerbAsync(UnrealVerbTypes.MoveTo, moveToPayload, cancellationToken);

    public Task<bool> StopAsync(CancellationToken cancellationToken = default) =>
        SendVerbAsync(UnrealVerbTypes.Stop, payload: null, cancellationToken);

    public Task<bool> LookAsync(object lookPayload, CancellationToken cancellationToken = default) =>
        SendVerbAsync(UnrealVerbTypes.Look, lookPayload, cancellationToken);

    /// <inheritdoc />
    public Task<EyeFrame?> CaptureEyeAsync(CancellationToken cancellationToken = default) =>
        CaptureSceneFrameAsync(
            commandName: "eye_capture",
            acceptedTypes: new[] { "eye_frame" },
            cancellationToken);

    /// <inheritdoc />
    public Task<EyeFrame?> CaptureCallFrameAsync(CancellationToken cancellationToken = default) =>
        CaptureSceneFrameAsync(
            commandName: "call_capture",
            acceptedTypes: new[] { "call_frame", "avatar_call_frame" },
            cancellationToken);

    private async Task<EyeFrame?> CaptureSceneFrameAsync(
        string commandName,
        string[] acceptedTypes,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return null;

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        if (!IsConnected || _socket is null)
            return null;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsConnected || _socket is null)
                return null;

            var wire = "{\"type\":\"command\",\"payload\":{\"name\":\"" + commandName + "\",\"args\":{}}}";
            var bytes = Encoding.UTF8.GetBytes(wire);
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
                .ConfigureAwait(false);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));

            var buffer = new byte[1024 * 1024];
            while (!timeout.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, timeout.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return null;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var json = Encoding.UTF8.GetString(ms.ToArray());
                if (TryParseCaptureFrame(json, acceptedTypes, out var frame))
                    return frame;
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "{Command} failed", commandName);
        }
        finally
        {
            _gate.Release();
        }

        return null;
    }

    private static bool TryParseCaptureFrame(string json, string[] acceptedTypes, out EyeFrame? frame)
    {
        frame = null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(type)
                || !Array.Exists(acceptedTypes, a => string.Equals(a, type, StringComparison.OrdinalIgnoreCase)))
                return false;

            var b64 = root.TryGetProperty("bytes_b64", out var b) ? b.GetString()
                : root.TryGetProperty("bytes", out var b2) ? b2.GetString() : null;
            if (string.IsNullOrWhiteSpace(b64))
                return false;

            var bytes = Convert.FromBase64String(b64);
            var format = root.TryGetProperty("format", out var f) ? f.GetString() ?? "png" : "png";
            var width = root.TryGetProperty("width", out var w) && w.TryGetInt32(out var wi) ? wi : 0;
            var height = root.TryGetProperty("height", out var h) && h.TryGetInt32(out var hi) ? hi : 0;
            frame = new EyeFrame(bytes, format, width, height);
            return true;
        }
        catch
        {
            return false;
        }
    }

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
