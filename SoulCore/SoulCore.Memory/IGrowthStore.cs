using SoulCore.Core.Growth;

namespace SoulCore.Memory;

/// <summary>PROP-20.37: soak clock, growth trait weights, ledger, pending band-cross.</summary>
public interface IGrowthStore
{
    /// <summary>
    /// Ensures soak clock row exists (starts on first Identity display). Returns started_at.
    /// </summary>
    Task<DateTimeOffset> EnsureSoakStartedAsync(
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetSoakStartedAtAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GrowthTraitLine>> ListTraitWeightsAsync(CancellationToken cancellationToken = default);

    Task SetTraitWeightAsync(
        string traitId,
        double weight,
        string band,
        string ledgerReason,
        CancellationToken cancellationToken = default);

    Task<long> AppendLedgerAsync(
        string traitId,
        double fromWeight,
        double toWeight,
        string fromBand,
        string toBand,
        string reason,
        string source,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GrowthLedgerEntry>> ListLedgerAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<long> CreatePendingAsync(
        string traitId,
        double fromWeight,
        double toWeight,
        string fromBand,
        string toBand,
        string reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GrowthPendingProposal>> ListPendingAsync(
        CancellationToken cancellationToken = default);

    Task<GrowthPendingProposal?> GetPendingAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<bool> ResolvePendingAsync(
        long id,
        string status,
        CancellationToken cancellationToken = default);

    /// <summary>Bond counters computed from Host facts (chat, tasks, corrections, journals).</summary>
    Task<BondCounterSnapshot> ComputeBondCountersAsync(
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken = default);
}

public readonly record struct BondCounterSnapshot(
    double ContactTimeMinutes,
    int DaysWithContact,
    int EntrustedTasks,
    int OperatorCorrections,
    int HerInitiatedReaches,
    int JournalOpens);

public readonly record struct GrowthLedgerEntry(
    long Id,
    string TraitId,
    double FromWeight,
    double ToWeight,
    string FromBand,
    string ToBand,
    string Reason,
    string Source,
    string RecordedAt);

public readonly record struct GrowthPendingProposal(
    long Id,
    string TraitId,
    double FromWeight,
    double ToWeight,
    string FromBand,
    string ToBand,
    string Reason,
    string Status,
    string CreatedAt,
    string? ResolvedAt);
