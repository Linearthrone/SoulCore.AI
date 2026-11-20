using SoulCore.Memory;

namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-18.7: write inspectable session residue — activities-log Desc + topic journal.
/// Leisure / research / parked-work call this after a pass with real findings.
/// </summary>
public interface IActivityResidueWriter
{
    /// <summary>
    /// Append activities-log row + topic-scoped first-person journal when
    /// <paramref name="findings"/> has substance. Returns null when skipped
    /// (empty / too thin — no theater rows).
    /// </summary>
    Task<ActivityResidueWriteResult?> TryWriteAsync(
        string activity,
        string subject,
        string findings,
        string? occurredAt = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of one residue write (log + journal link).</summary>
public sealed record ActivityResidueWriteResult(
    long ActivityEntryNo,
    long JournalEntryId,
    string JournalBookId,
    string SessionDesc,
    string JournalBody,
    VictoriaActivityLogEntry LogEntry);
