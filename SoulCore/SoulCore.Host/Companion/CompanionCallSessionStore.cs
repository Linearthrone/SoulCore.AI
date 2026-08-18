using System.Collections.Concurrent;

namespace SoulCore.Host.Companion;

/// <summary>
/// In-process video-call sessions for Victoria Link.
/// MVP mode = polled waist-up frames from Unreal <c>call_capture</c>.
/// WebRTC signaling slots are reserved for a follow-up (mode stays <c>frames</c> until enabled).
/// </summary>
public sealed class CompanionCallSessionStore
{
    private readonly ConcurrentDictionary<string, CallSession> _sessions = new(StringComparer.Ordinal);

    public CallSession Start(string? contactId)
    {
        // Best-effort pruning to avoid unbounded growth if clients never end a session.
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
        foreach (var (key, value) in _sessions)
        {
            if (value.CreatedUtc < cutoff)
                _sessions.TryRemove(key, out _);
        }

        var id = "call_" + Guid.NewGuid().ToString("N")[..12];
        var session = new CallSession(
            SessionId: id,
            ContactId: string.IsNullOrWhiteSpace(contactId) ? "victoria" : contactId.Trim(),
            Mode: "frames",
            CreatedUtc: DateTimeOffset.UtcNow,
            WebrtcAvailable: false);
        _sessions[id] = session;
        return session;
    }

    public bool TryGet(string sessionId, out CallSession session) =>
        _sessions.TryGetValue(sessionId, out session!);

    public bool End(string sessionId) =>
        _sessions.TryRemove(sessionId, out _);

    public sealed record CallSession(
        string SessionId,
        string ContactId,
        string Mode,
        DateTimeOffset CreatedUtc,
        bool WebrtcAvailable);
}
