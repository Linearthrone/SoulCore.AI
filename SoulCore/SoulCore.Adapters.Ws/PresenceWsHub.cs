using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SoulCore.Adapters.Ws;

/// <summary>
/// Tracks active Presence WS sessions for optional broadcast (emotion.snapshot etc.).
/// <see cref="SendAsync"/> is reserved for multi-client fan-out; V1 chat path
/// writes replies on the requesting socket only.
/// </summary>
public sealed class PresenceWsHub : IWsFrameAdapter
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _sockets = new();
    private readonly ILogger<PresenceWsHub> _logger;

    public PresenceWsHub(ILogger<PresenceWsHub> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Guid Register(WebSocket socket)
    {
        var id = Guid.NewGuid();
        _sockets[id] = socket;
        return id;
    }

    public void Unregister(Guid id) => _sockets.TryRemove(id, out _);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PresenceWsHub ready (loopback /ws)");
        return Task.CompletedTask;
    }

    public async Task SendAsync(string jsonFrame, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jsonFrame))
            return;

        var bytes = Encoding.UTF8.GetBytes(jsonFrame);
        foreach (var pair in _sockets)
        {
            var socket = pair.Value;
            if (socket.State != WebSocketState.Open)
            {
                _sockets.TryRemove(pair.Key, out _);
                continue;
            }

            try
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
            {
                _sockets.TryRemove(pair.Key, out _);
            }
        }
    }
}
