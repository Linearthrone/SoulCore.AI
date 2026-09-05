namespace SoulCore.Host.Ws;

/// <summary>
/// Thin facade over <see cref="ChatWebSocketSessionRunner"/> for DI and WS endpoint wiring.
/// </summary>
public sealed class ChatWebSocketHandler
{
    private readonly ChatWebSocketSessionRunner _runner;

    public ChatWebSocketHandler(ChatWebSocketSessionRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public Task RunAsync(System.Net.WebSockets.WebSocket socket, CancellationToken cancellationToken) =>
        _runner.RunAsync(socket, cancellationToken);
}
