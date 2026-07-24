namespace SoulCore.Core.Safety;

/// <summary>
/// Report-only drift watcher for Phase 3. Tracks drift reports and computes SLO
/// status. Does NOT block acts yet — that is a future gate. Pure in-memory logic;
/// no external dependencies.
/// </summary>
public sealed class DriftWatcher
{
    private readonly TimeSpan _sloWindow;
    private readonly List<DriftReport> _unacked = new();
    private readonly object _gate = new();

    /// <summary>
    /// Creates a watcher with the given SLO window. If the oldest unacknowledged
    /// report is older than this window relative to "now", <c>SloExceeded</c> is true.
    /// </summary>
    /// <param name="sloWindow">How old an unacked report must be before SLO is exceeded.</param>
    public DriftWatcher(TimeSpan sloWindow)
    {
        if (sloWindow <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(sloWindow), "SLO window must be positive.");
        _sloWindow = sloWindow;
    }

    /// <summary>
    /// Creates a watcher with an SLO window expressed in minutes.
    /// </summary>
    public DriftWatcher(int sloMinutes)
        : this(TimeSpan.FromMinutes(sloMinutes))
    {
    }

    /// <summary>
    /// Records a drift report and adds it to the unacknowledged list.
    /// </summary>
    public void RecordDrift(DriftReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        lock (_gate)
        {
            _unacked.Add(report);
        }
    }

    /// <summary>
    /// Convenience overload for the SoulLoop tick: builds a <see cref="DriftReport"/> from the
    /// current emotion label, the dominant emotion component (valence magnitude as the drift
    /// score), and the proposed want string. The threshold is a fixed conservative constant
    /// (0.5) — Phase 3 is report-only, so the exact score/threshold only affects
    /// <see cref="DriftReport.ExceedsThreshold"/>, not act gating.
    /// </summary>
    /// <param name="emotionLabel">Current emotion label (e.g. "calm", "excited").</param>
    /// <param name="fields">Emotion fields (valence/arousal/dominance) for the current tick.</param>
    /// <param name="want">The want string proposed this tick.</param>
    public void RecordDrift(
        string emotionLabel,
        EmotionInfluencePrompt.EmotionFields fields,
        string want)
    {
        ArgumentNullException.ThrowIfNull(emotionLabel);
        ArgumentNullException.ThrowIfNull(want);

        // Drift "score" = distance of the emotional state from neutral (0,0).
        // Threshold 0.5 keeps the report informational for Phase 3 (report-only).
        var score = Math.Sqrt(fields.Valence * fields.Valence + fields.Arousal * fields.Arousal);
        const double threshold = 0.5;

        var note = $"label={emotionLabel}; v={fields.Valence:0.00} a={fields.Arousal:0.00} " +
                   $"d={fields.Dominance:0.00}; want={Truncate(want, 120)}";

        RecordDrift(new DriftReport(
            Dimension: "emotion",
            Score: score,
            Threshold: threshold,
            Note: note,
            ObservedAt: DateTimeOffset.UtcNow));
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value ?? string.Empty;
        return value[..max] + "…";
    }

    /// <summary>
    /// Acknowledges all current reports, clearing the unacked list.
    /// Returns the number of reports that were acknowledged.
    /// </summary>
    public int AcknowledgeAll()
    {
        lock (_gate)
        {
            var count = _unacked.Count;
            _unacked.Clear();
            return count;
        }
    }

    /// <summary>
    /// Acknowledges the oldest report (FIFO). Returns true if a report was acked.
    /// </summary>
    public bool AcknowledgeOldest()
    {
        lock (_gate)
        {
            if (_unacked.Count == 0)
                return false;
            _unacked.RemoveAt(0);
            return true;
        }
    }

    /// <summary>
    /// Returns the current drift status. <c>SloExceeded</c> is computed by comparing
    /// the oldest unacked report's <c>ObservedAt</c> against the provided <paramref name="now"/>.
    /// </summary>
    /// <param name="now">The reference time for SLO computation. Defaults to UTC now.</param>
    public DriftStatus GetStatus(DateTimeOffset? now = null)
    {
        var reference = now ?? DateTimeOffset.UtcNow;

        lock (_gate)
        {
            var last = _unacked.Count > 0 ? _unacked[^1] : null;
            var oldest = _unacked.Count > 0 ? _unacked[0] : null;
            var sloExceeded = false;

            if (_unacked.Count > 0)
            {
                sloExceeded = reference - oldest!.ObservedAt > _sloWindow;
            }

            return new DriftStatus(last, _unacked.Count, sloExceeded, oldest);
        }
    }
}
