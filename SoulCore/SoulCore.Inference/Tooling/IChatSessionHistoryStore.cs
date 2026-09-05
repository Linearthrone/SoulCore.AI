namespace SoulCore.Inference.Tooling;

/// <summary>
/// In-memory per-<c>sessionId</c> chat + tool transcript store (BED-158 / ISSUE-004).
/// Lets multi-turn pronouns ("that task", "run that workflow") resolve to prior
/// tool result IDs by replaying recent user / assistant / tool messages into the
/// next <c>CompleteWithToolsAsync</c> call.
/// </summary>
public interface IChatSessionHistoryStore
{
    /// <summary>
    /// Returns a snapshot of messages stored for <paramref name="sessionId"/>
    /// (empty when unknown). Never includes the system preamble — callers prepend that.
    /// </summary>
    IReadOnlyList<ChatMessage> GetMessages(string sessionId);

    /// <summary>
    /// Appends one completed turn's messages (user + optional tool trace + assistant)
    /// and trims to the configured max. No-op when <paramref name="sessionId"/> is blank.
    /// </summary>
    void AppendTurn(string sessionId, IReadOnlyList<ChatMessage> turnMessages);

    /// <summary>Drops all history for <paramref name="sessionId"/> (tests / reset).</summary>
    void Clear(string sessionId);
}
