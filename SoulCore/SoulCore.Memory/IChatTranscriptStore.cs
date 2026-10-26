namespace SoulCore.Memory;

/// <summary>
/// One durable turn in the operator &lt;-&gt; Victoria conversation.
/// </summary>
/// <param name="Id">Monotonic row id, and the hydrate cursor clients page with.</param>
/// <param name="Role">"user" or "assistant".</param>
/// <param name="Channel">Where the turn came from — desk, sms, companion, proactive.</param>
/// <param name="FrameId">Originating WS frame id, so a client can dedupe a hydrated row
/// against one it already rendered live.</param>
public sealed record ChatTranscriptMessage(
    long Id,
    string ConversationId,
    string Role,
    string Content,
    string OccurredAt,
    string? Channel,
    string? FrameId,
    string? MediaId);

/// <summary>
/// PROP-3 Wave 1: the durable, replayable operator &lt;-&gt; Victoria transcript.
///
/// Deliberately separate from the two things that already existed and are not this:
/// <c>IMemoryStore</c> holds episodic summaries of what happened, and
/// <c>IChatSessionHistoryStore</c> is bounded in-RAM LLM context that dies with the process.
/// This store backfills a fresh client, and Host replays its tail into that RAM thread
/// on the first turn after a restart.
/// </summary>
public interface IChatTranscriptStore
{
    /// <summary>
    /// Append one turn and return its row id (the cursor value).
    /// </summary>
    /// <remarks>
    /// Idempotent on <paramref name="frameId"/>: re-appending a frame already stored returns
    /// the existing row id instead of duplicating the turn.
    /// </remarks>
    Task<long> AppendAsync(
        string conversationId,
        string role,
        string content,
        string? channel = null,
        string? frameId = null,
        string? mediaId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns after <paramref name="afterId"/>, oldest first — the hydrate read.
    /// Pass 0 to start from the beginning of the conversation.
    /// </summary>
    Task<IReadOnlyList<ChatTranscriptMessage>> GetSinceAsync(
        string conversationId,
        long afterId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The most recent <paramref name="limit"/> turns, oldest first. Used for a cold client
    /// that wants the tail of a long conversation rather than all of it.
    /// </summary>
    Task<IReadOnlyList<ChatTranscriptMessage>> GetRecentAsync(
        string conversationId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Highest row id in the conversation, or 0 when empty.</summary>
    Task<long> GetLatestCursorAsync(
        string conversationId,
        CancellationToken cancellationToken = default);
}
