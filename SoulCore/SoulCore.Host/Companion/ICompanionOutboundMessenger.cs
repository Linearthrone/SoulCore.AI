namespace SoulCore.Host.Companion;

/// <summary>
/// Fan-out unsolicited assistant messages to all Presence WS clients (Victoria Link push).
/// </summary>
public interface ICompanionOutboundMessenger
{
    /// <summary>
    /// Broadcast <c>chat.done</c> (and optional prior <c>chat.delta</c>) with
    /// <c>proactive=true</c>, persist a light episodic row, optional <paramref name="mediaId"/>.
    /// </summary>
    Task<CompanionOutboundResult> PushAsync(
        string text,
        string? contactId = null,
        string? mediaId = null,
        bool streamDelta = false,
        CancellationToken cancellationToken = default);
}

public sealed record CompanionOutboundResult(
    bool Ok,
    string FrameId,
    string ContactId,
    string? MediaId,
    string? Error);
