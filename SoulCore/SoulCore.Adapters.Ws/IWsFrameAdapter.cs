namespace SoulCore.Adapters.Ws;

/// <summary>
/// JSON frame adapter surface for chat + Unreal bridge.
/// V1 listeners bind 127.0.0.1 only (SEC-004). Presence clients use Host <c>/ws</c>.
/// </summary>
public interface IWsFrameAdapter
{
    /// <summary>
    /// Broadcast a JSON frame to connected Presence clients (best-effort).
    /// Reserved for multi-client fan-out (e.g. emotion.snapshot); V1 chat replies
    /// are sent directly on the requesting socket and do not call this yet.
    /// </summary>
    Task SendAsync(string jsonFrame, CancellationToken cancellationToken = default);

    Task StartAsync(CancellationToken cancellationToken = default);
}
