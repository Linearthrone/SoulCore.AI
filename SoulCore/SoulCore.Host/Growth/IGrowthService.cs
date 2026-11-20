using SoulCore.Core.Growth;
using SoulCore.Memory;

namespace SoulCore.Host.Growth;

/// <summary>PROP-20.37 Host growth surface for Identity API + mind-block injection.</summary>
public interface IGrowthService
{
    /// <summary>
    /// Starts soak clock if needed, returns Identity growth payload (counters, soak, traits, pending).
    /// Does not apply counter nudges while display-only.
    /// </summary>
    Task<GrowthIdentitySnapshot> GetIdentitySnapshotAsync(
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Growth traits for mind-block injection — null/empty when soak may not change prompt.
    /// </summary>
    Task<IReadOnlyList<GrowthTraitLine>?> GetPromptGrowthTraitsAsync(
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply a proposed counter nudge (or persist pending). Used by tests and post-soak evaluate.
    /// </summary>
    Task<GrowthDeltaEngine.Decision> ApplyProposedDeltaAsync(
        string traitId,
        double proposedWeight,
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default);

    Task<bool> ConfirmPendingAsync(long proposalId, CancellationToken cancellationToken = default);

    Task<bool> DismissPendingAsync(long proposalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// After soak opens: derive small nudges from bond counters (responsiveness/initiative/openness).
    /// </summary>
    Task EvaluateCounterNudgesAsync(
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default);
}

public sealed record GrowthIdentitySnapshot(
    BondCounterSnapshot BondCounters,
    GrowthSoakClock.SoakSnapshot Soak,
    IReadOnlyList<GrowthTraitLine> GrowthTraits,
    IReadOnlyList<GrowthPendingProposal> Pending,
    IReadOnlyList<GrowthLedgerEntry> RecentLedger);
