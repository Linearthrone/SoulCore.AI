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

    Task WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default);
}
