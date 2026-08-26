using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SoulCore.Protocol;

namespace House.ChatDesktop.Services;

public enum WsConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Unavailable,
    /// <summary>Host answered but rejected companion auth (401 / missing token).</summary>
    AuthRejected,
    Blocked
}

/// <summary>
/// Presence WebSocket client for SoulCore Host.
/// Default: ws://127.0.0.1:7700/ws (loopback only). No LLM calls from UI.
/// </summary>
public sealed class SoulCoreWsClient : IAsyncDisposable
{
    /// <summary>
    /// Host accepts Bearer or X-Api-Key. Desktop uses X-Api-Key so ClientWebSocket
    /// does not depend on Authorization (restricted / proxy-stripped on some stacks).
    /// </summary>
    public const string AuthHeaderName = "X-Api-Key";

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _receiveCts;

    public WsConnectionState State { get; private set; } = WsConnectionState.Disconnected;
    public string LastError { get; private set; } = string.Empty;

    public event Action<WsConnectionState, string>? StateChanged;
    public event Action<SoulCoreFrame>? FrameReceived;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            SetState(WsConnectionState.Blocked, $"Non-loopback host blocked: {ConnectionDefaults.Host}");
            return;
        }

        await DisconnectAsync(notifyDisconnected: false).ConfigureAwait(false);
        SetState(WsConnectionState.Connecting, $"Connecting {ConnectionDefaults.WsUri}");

        var token = CompanionToken.Resolve();
        var tokenPresent = !string.IsNullOrEmpty(token);
        var tokenLen = token?.Length ?? 0;
        // #region agent log
        AgentDebugLog("A", "SoulCoreWsClient.ConnectAsync:entry", "connect_begin", new
        {
            uri = ConnectionDefaults.WsUri.ToString(),
            tokenPresent,
            tokenLen,
            port = ConnectionDefaults.Port
        });
        // #endregion

        var socket = new ClientWebSocket();
        var headerAttached = false;
        try
        {
            if (tokenPresent)
            {
                // Prefer X-Api-Key: Host accepts it; avoids Authorization restricted-header /
                // WinHTTP / proxy footguns that leave /health "up" while /ws stays 401.
                socket.Options.SetRequestHeader(AuthHeaderName, token);
                headerAttached = true;
            }

            // #region agent log
            AgentDebugLog("A", "SoulCoreWsClient.ConnectAsync:headers", "header_ready", new
            {
                header = headerAttached ? AuthHeaderName : "none",
                tokenPresent,
                tokenLen
            });
            // #endregion

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await socket.ConnectAsync(ConnectionDefaults.WsUri, timeout.Token).ConfigureAwait(false);
            _socket = socket;
            SetState(
                WsConnectionState.Connected,
                $"WS connected · {ConnectionDefaults.WsUri} · tokenPresent={tokenPresent} tokenLen={tokenLen} header={(headerAttached ? AuthHeaderName : "none")}");
            // #region agent log
            AgentDebugLog("E", "SoulCoreWsClient.ConnectAsync:ok", "connected", new
            {
                tokenPresent,
                tokenLen,
                header = headerAttached ? AuthHeaderName : "none"
            });
            // #endregion
            _receiveCts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
        }
        catch (Exception ex) when (ex is WebSocketException or HttpRequestException or OperationCanceledException
                                       or InvalidOperationException or ArgumentException)
        {
            socket.Dispose();
            _socket = null;
            var authFail = GuessAuthFailure(ex);
            var tokenMeta = $"tokenPresent={tokenPresent} tokenLen={tokenLen} header={(headerAttached ? AuthHeaderName : "none")}";
            string hint;
            WsConnectionState failState;
            if (authFail)
            {
                failState = WsConnectionState.AuthRejected;
                hint = tokenPresent
                    ? $"Host rejected WS auth at {ConnectionDefaults.WsUri} ({tokenMeta}). Match {CompanionToken.EnvName} in SoulCore/.env to Host (restart ChatDesktop after .env changes)."
                    : $"Host requires companion token at {ConnectionDefaults.WsUri} ({tokenMeta}). Set {CompanionToken.EnvName} in SoulCore/.env and restart ChatDesktop.";
            }
            else
            {
                failState = WsConnectionState.Unavailable;
                hint = $"Host WS down at {ConnectionDefaults.WsUri} ({tokenMeta}; {ex.GetType().Name}: {ex.Message}). Start SoulCore.Host, then Refresh.";
            }

            // #region agent log
            AgentDebugLog(authFail ? "D" : "C", "SoulCoreWsClient.ConnectAsync:fail", authFail ? "auth_rejected" : "unavailable", new
            {
                tokenPresent,
                tokenLen,
                header = headerAttached ? AuthHeaderName : "none",
                exType = ex.GetType().Name,
                exMessage = ex.Message,
                failState = failState.ToString()
            });
            // #endregion
            SetState(failState, hint);
        }
    }

    public async Task<bool> SendChatAsync(string text, CancellationToken cancellationToken = default)
    {
        var frame = SoulCoreFrame.Create(
            SoulCoreFrameTypes.ChatSend,
            new { text, sessionId = "presence-local" });

        return await SendFrameAsync(frame, "chat.send", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Forces one SoulLoop cycle on the Host (<c>loop.tick</c>).
    /// Host acks with <c>loop.tick.ok</c> and broadcasts <c>loop.want</c> when enabled.
    /// </summary>
    public async Task<bool> SendLoopTickAsync(CancellationToken cancellationToken = default)
    {
        var frame = SoulCoreFrame.Create(SoulCoreFrameTypes.LoopTick, new { });
        return await SendFrameAsync(frame, "loop.tick", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends <c>emotion.correct</c> with valence/arousal/dominance/focus + optional note.
    /// SoulCore Host persists the correction and echoes <c>emotion.snapshot</c> when WS is open.
    /// </summary>
    public async Task<bool> SendEmotionCorrectAsync(
        double valence,
        double arousal,
        double dominance,
        double focus,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var frame = SoulCoreFrame.Create(
            SoulCoreFrameTypes.EmotionCorrect,
            new
            {
                valence,
                arousal,
                dominance,
                focus,
                note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            });

        return await SendFrameAsync(frame, "emotion.correct", cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> SendFrameAsync(
        SoulCoreFrame frame,
        string typeLabel,
        CancellationToken cancellationToken)
    {
        if (_socket is not { State: WebSocketState.Open })
        {
            var token = CompanionToken.Resolve();
            LastError =
                $"WS unavailable — {typeLabel} not sent. State={State}; tokenPresent={!string.IsNullOrEmpty(token)} tokenLen={token?.Length ?? 0}. Host must be up on loopback /ws with matching {CompanionToken.EnvName}.";
            return false;
        }

        var bytes = Encoding.UTF8.GetBytes(frame.ToJson());
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    public async Task DisconnectAsync() => await DisconnectAsync(notifyDisconnected: true).ConfigureAwait(false);

    private async Task DisconnectAsync(bool notifyDisconnected)
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _receiveCts = null;

        if (_socket is not null)
        {
            try
            {
                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client close", CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
            catch
            {
                // ignore close races
            }

            _socket.Dispose();
            _socket = null;
        }

        if (notifyDisconnected
            && State is not WsConnectionState.Unavailable
                and not WsConnectionState.AuthRejected
                and not WsConnectionState.Blocked)
        {
            SetState(WsConnectionState.Disconnected, "Disconnected from SoulCore WS");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8 * 1024];
        while (!cancellationToken.IsCancellationRequested && _socket is { State: WebSocketState.Open })
        {
            try
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        SetState(WsConnectionState.Disconnected, "Host closed WS — reconnect with Refresh when Host is back.");
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var json = Encoding.UTF8.GetString(ms.ToArray());
                if (SoulCoreFrame.TryParse(json, out var frame) && frame is not null)
                {
                    FrameReceived?.Invoke(frame);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                SetState(WsConnectionState.Disconnected, $"WS receive error: {ex.Message}. Host may be down.");
                return;
            }
        }
    }

    private void SetState(WsConnectionState state, string detail)
    {
        State = state;
        LastError = detail;
        StateChanged?.Invoke(state, detail);
    }

    public static bool GuessAuthFailure(Exception ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("401", StringComparison.Ordinal)
            || msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("403", StringComparison.Ordinal);
    }

    // #region agent log
    private static void AgentDebugLog(string hypothesisId, string location, string message, object data)
    {
        try
        {
            var line = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["hypothesisId"] = hypothesisId,
                ["location"] = location,
                ["message"] = message,
                ["data"] = data,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["runId"] = Environment.GetEnvironmentVariable("SOULCORE_DEBUG_RUN_ID") ?? "pre-fix"
            });
            File.AppendAllText("/opt/cursor/logs/debug.log", line + Environment.NewLine);
        }
        catch
        {
            // Diagnostics only — never break connect on Windows / missing path.
        }
    }
    // #endregion

    public async ValueTask DisposeAsync() => await DisconnectAsync().ConfigureAwait(false);
}
