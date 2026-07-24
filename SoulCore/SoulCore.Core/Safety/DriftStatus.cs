namespace SoulCore.Core.Safety;

/// <summary>
/// Snapshot of the drift watcher state returned by <see cref="DriftWatcher.GetStatus"/>.
/// </summary>
/// <param name="LastDriftReport">Most recently recorded drift report, or null when none pending.</param>
/// <param name="UnackedReports">Count of unacknowledged drift reports currently held.</param>
/// <param name="SloExceeded">True when the oldest unacked report is older than the SLO window.</param>
/// <param name="OldestDriftReport">Earliest unacknowledged drift report, or null when none pending.</param>
public sealed record DriftStatus(
    DriftReport? LastDriftReport,
    int UnackedReports,
    bool SloExceeded,
    DriftReport? OldestDriftReport);
