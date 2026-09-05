using System.Net.WebSockets;
using System.Text;
using SoulCore.Protocol;

namespace SoulCore.Host.Ws;

internal static class WsFrameSender
{
    internal static async Task SendFrameAsync(
        WebSocket socket,
        SoulCoreFrame frame,
        CancellationToken cancellationToken)
    {
        if (socket.State != WebSocketState.Open)
            return;

        var bytes = Encoding.UTF8.GetBytes(frame.ToJson());
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
            .ConfigureAwait(false);
    }
}
