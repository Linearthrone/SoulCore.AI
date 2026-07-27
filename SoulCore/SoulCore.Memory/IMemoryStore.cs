namespace SoulCore.Memory;

/// <summary>
/// Episodic / semantic / working memory backed by SQLite (DBD schema V1).
/// </summary>
public interface IMemoryStore
{
    /// <summary>True after migrations applied and connection opened.</summary>
    bool IsDatabaseOpen { get; }

    /// <summary>Resolved absolute path of the SQLite file.</summary>
    string DatabasePath { get; }

    /// <summary>
    /// Insert an episodic memory row. Returns the new row <c>id</c>.
    /// </summary>
    Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist (or replace) a float32 embedding blob for an episodic row.
    /// Best-effort from chat path — failures should not fail the chat.
    /// </summary>
    Task StoreEmbeddingAsync(
        long episodicId,
        float[] vector,
        string model,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Non-quarantined episodic rows with no <c>episodic_embedding_vectors</c> row
    /// (oldest id first). Used by embedding backfill CLI.
    /// </summary>
    Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cosine top-K over recent non-quarantined episodics that have stored vectors
    /// (scan cap ~500). Returns content strings, most similar first.
    /// </summary>
    Task<IReadOnlyList<string>> RecallSimilarAsync(
        float[] queryVector,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default);
}
