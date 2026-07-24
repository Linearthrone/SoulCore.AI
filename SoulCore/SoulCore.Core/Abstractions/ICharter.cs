namespace SoulCore.Core.Abstractions;

/// <summary>
/// Identity / safety anchors outside episodic memory.
/// </summary>
public interface ICharter
{
    /// <summary>
    /// Returns all anchor body texts, ordered by priority (lower = higher priority).
    /// </summary>
    Task<IReadOnlyList<string>> GetAnchorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns anchor body texts filtered by <paramref name="kind"/>
    /// (identity / safety / value / boundary / ritual), optionally restricted to
    /// locked anchors only, ordered by priority.
    /// </summary>
    Task<IReadOnlyList<string>> GetAnchorsByKindAsync(
        string kind,
        bool? lockedOnly = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts initial charter anchors. Intended for test/staging seeding only —
    /// not wired to the live Host. Returns the number of rows inserted.
    /// </summary>
    Task<int> SeedAsync(
        IReadOnlyList<CharterAnchorSeed> seeds,
        CancellationToken cancellationToken = default);
}
