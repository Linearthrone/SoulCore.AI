using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using SoulCore.Adapters.Ws;
using SoulCore.Protocol;

namespace SoulCore.Host.Ws;

/// <summary>
/// WebSocket session loop: handshake, frame receive, route to focused handlers.
/// </summary>
public sealed class ChatWebSocketSessionRunner
{
    private readonly PresenceWsHub _hub;
    private readonly EmotionSnapshotSender _emotionSnapshot;
    private readonly ChatSendHandler _chatSend;
    private readonly EmotionCorrectHandler _emotionCorrect;
    private readonly LoopTickHandler _loopTick;
    private readonly ILogger<ChatWebSocketSessionRunner> _logger;

    public ChatWebSocketSessionRunner(
        PresenceWsHub hub,
        EmotionSnapshotSender emotionSnapshot,
        ChatSendHandler chatSend,
        EmotionCorrectHandler emotionCorrect,
        LoopTickHandler loopTick,
        ILogger<ChatWebSocketSessionRunner> logger)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _emotionSnapshot = emotionSnapshot ?? throw new ArgumentNullException(nameof(emotionSnapshot));
        _chatSend = chatSend ?? throw new ArgumentNullException(nameof(chatSend));
        _emotionCorrect = emotionCorrect ?? throw new ArgumentNullException(nameof(emotionCorrect));
        _loopTick = loopTick ?? throw new ArgumentNullException(nameof(loopTick));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RunAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var sessionId = _hub.Register(socket);
        _logger.LogInformation("WS session {SessionId} accepted", sessionId);

        try
        {
            await WsFrameSender.SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.PresenceStatus,
                    new { alive = true, warm = true, phase = 1 }),
                cancellationToken).ConfigureAwait(false);

            await _emotionSnapshot.SendAsync(socket, correlationId: null, cancellationToken)
                .ConfigureAwait(false);

            var buffer = new byte[16 * 1024];
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "bye",
                            CancellationToken.None).ConfigureAwait(false);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                var json = Encoding.UTF8.GetString(ms.ToArray());
                await HandleTextAsync(socket, json, sessionId, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _hub.Unregister(sessionId);
            _logger.LogInformation("WS session {SessionId} closed", sessionId);
        }
    }

    private async Task HandleTextAsync(
        WebSocket socket,
        string json,
        Guid connectionSessionId,
        CancellationToken cancellationToken)
    {
        if (!SoulCoreFrame.TryParse(json, out var frame) || frame is null)
        {
            await WsFrameSender.SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new { code = "frame.invalid", message = "Expected SoulCore JSON envelope {v,type,id,ts,payload}" }),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        switch (frame.Type)
        {
            case SoulCoreFrameTypes.Ping:
                await WsFrameSender.SendFrameAsync(
                    socket,
                    SoulCoreFrame.Create(SoulCoreFrameTypes.Pong, new { }, id: frame.Id),
                    cancellationToken).ConfigureAwait(false);
                break;

            case SoulCoreFrameTypes.ChatSend:
                await _chatSend.HandleAsync(socket, frame, connectionSessionId, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case SoulCoreFrameTypes.EmotionCorrect:
                await _emotionCorrect.HandleAsync(socket, frame, cancellationToken).ConfigureAwait(false);
                break;

            case SoulCoreFrameTypes.LoopTick:
                await _loopTick.HandleAsync(socket, frame, cancellationToken).ConfigureAwait(false);
                break;

            default:
                await WsFrameSender.SendFrameAsync(
                    socket,
                    SoulCoreFrame.Create(
                        SoulCoreFrameTypes.Error,
                        new { code = "frame.unsupported", message = $"Unsupported type '{frame.Type}'" },
                        id: frame.Id),
                    cancellationToken).ConfigureAwait(false);
                break;
        }
    }
}
