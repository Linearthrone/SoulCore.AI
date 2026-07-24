using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Safety;
using SoulCore.Memory;
using SoulCore.Protocol;
using System.Globalization;

namespace SoulCore.Host.Loop;

/// <summary>
/// Safe want→act scaffold: proposes a want string only. Never triggers browser/MT4/email/file acts.
/// Unreal verbs are not called from this loop (optional no-op path remains elsewhere if UE down).
/// </summary>
public sealed class SoulLoopScaffold : ISoulLoop
{
    private readonly IEmotionState _emotion;
    private readonly IMemoryStore _memory;
    private readonly PresenceWsHub _hub;
    private readonly SoulLoopOptions _options;
    private readonly DriftWatcher _driftWatcher;
    private readonly ILogger<SoulLoopScaffold> _logger;
    private readonly object _gate = new();
    private string? _lastWant;
    private int _tickCount;

    public SoulLoopScaffold(
        IEmotionState emotion,
        IMemoryStore memory,
        PresenceWsHub hub,
        IOptions<SoulLoopOptions> options,
        DriftWatcher driftWatcher,
        ILogger<SoulLoopScaffold> logger)
    {
        _emotion = emotion ?? throw new ArgumentNullException(nameof(emotion));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _driftWatcher = driftWatcher ?? throw new ArgumentNullException(nameof(driftWatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsEnabled => _options.Enabled;

    public string? LastWant
    {
        get
        {
            lock (_gate)
                return _lastWant;
        }
    }

    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("SoulLoop tick skipped (SoulLoop:Enabled=false)");
            return;
        }

        IReadOnlyDictionary<string, double> emotion;
        try
        {
            emotion = await _emotion.GetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SoulLoop tick: emotion read failed");
            return;
        }

        var fields = EmotionInfluencePrompt.ReadFields(emotion);
        var label = EmotionInfluencePrompt.DescribeLabel(fields.Valence, fields.Arousal);

        IReadOnlyList<string> recent;
        try
        {
            var limit = Math.Clamp(_options.EpisodicRecallLimit, 0, 20);
            recent = limit == 0
                ? Array.Empty<string>()
                : await _memory.RecallRecentAsync(limit, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SoulLoop tick: episodic recall failed; continuing with emotion-only want");
            recent = Array.Empty<string>();
        }

        var category = SoulLoopWantProposal.Classify(label, fields, recent);
        var want = SoulLoopWantProposal.Propose(label, fields, recent);
        lock (_gate)
            _lastWant = want;

        _logger.LogInformation("SoulLoop want[{Category}]: {Want}", category, want);

        // Safety: record drift each tick (report-only; never blocks the loop).
        // On SLO-exceeded (oldest unacked drift beyond the window), flag the want frame.
        bool driftAlert = false;
        try
        {
            _driftWatcher.RecordDrift(label, fields, want);
            var status = _driftWatcher.GetStatus();
            if (status.SloExceeded)
            {
                driftAlert = true;
                _logger.LogWarning(
                    "SoulLoop drift SLO exceeded: {Unacked} unacked, oldest={OldestMinutes:F1} min ago",
                    status.UnackedReports,
                    status.OldestDriftReport is null
                        ? 0
                        : (DateTimeOffset.UtcNow - status.OldestDriftReport.ObservedAt).TotalMinutes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SoulLoop drift record failed (loop continues)");
        }

        // Optional session notify — best-effort; no clients is fine.
        var frame = SoulCoreFrame.Create(
            SoulCoreFrameTypes.LoopWant,
            new
            {
                want,
                category,
                emotionLabel = label,
                valence = fields.Valence,
                arousal = fields.Arousal,
                episodicCount = recent.Count,
                driftAlert
            });

        try
        {
            await _hub.SendAsync(frame.ToJson(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SoulLoop loop.want broadcast skipped");
        }

        // Episodic self-reflection: write a first-person memory every Nth tick.
        // Throttled to avoid memory bloat (default every 5th tick). Never breaks the loop.
        var interval = _options.ReflectionIntervalTicks;
        if (interval > 0)
        {
            var tick = ++_tickCount;
            if (tick % interval == 0)
            {
                var reflection = string.Format(
                    CultureInfo.InvariantCulture,
                    "[Reflection] I am feeling {0} (v={1:F2}, a={2:F2}). {3}",
                    label,
                    fields.Valence,
                    fields.Arousal,
                    want);
                try
                {
                    await _memory
                        .WriteEpisodicAsync(reflection, "self", cancellationToken)
                        .ConfigureAwait(false);
                    _logger.LogInformation(
                        "SoulLoop episodic reflection written: {Reflection}",
                        reflection.Length > 80 ? reflection[..80] + "..." : reflection);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "SoulLoop episodic reflection write failed (loop continues)");
                }
            }
        }
    }

    /// <summary>
    /// Deterministic, low-agency want from emotion + episodic categories. Never requests external tools.
    /// </summary>
    internal static string ProposeWant(
        string label,
        EmotionInfluencePrompt.EmotionFields fields,
        IReadOnlyList<string> recent)
        => SoulLoopWantProposal.Propose(label, fields, recent);
}
