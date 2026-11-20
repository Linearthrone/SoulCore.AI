namespace SoulCore.Memory;

/// <summary>
/// PROP-18.6 activities log — inspectable session residue (Q1).
/// Distinct from private <c>victoria_journal_*</c> books and from
/// <c>victoria_research_*</c> papers. Optional journal book/entry links only.
/// </summary>
public interface IVictoriaActivitiesLogStore
{
    /// <summary>
    /// Append one activities-log row. Returns the new <c>entry_no</c>.
    /// <paramref name="sessionDesc"/> must be session-specific (not a recycled template).
    /// </summary>
    Task<long> AppendAsync(
        string activity,
        string subject,
        string sessionDesc,
        string? occurredAt = null,
        string? journalBookId = null,
        long? journalEntryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recent-first list. When <paramref name="subject"/> is set, filters by that
    /// subject/topic/focus (exact match, case-sensitive store value).
    /// </summary>
    Task<IReadOnlyList<VictoriaActivityLogEntry>> ListRecentAsync(
        string? subject = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Load one row by entry number, or <c>null</c> when missing.</summary>
    Task<VictoriaActivityLogEntry?> GetAsync(
        long entryNo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PROP-18.7 / FED-18.3: rows with <c>occurred_at &gt;= since</c>, recent-first.
    /// Used by while-you-were-gone (empty is honest).
    /// </summary>
    Task<IReadOnlyList<VictoriaActivityLogEntry>> ListSinceAsync(
        string sinceOccurredAt,
        int limit = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>One activities-log row (Q1 columns).</summary>
public sealed record VictoriaActivityLogEntry(
    long EntryNo,
    string OccurredAt,
    string Activity,
    string Subject,
    string SessionDesc,
    string? JournalBookId,
    long? JournalEntryId,
    string CreatedAt);
