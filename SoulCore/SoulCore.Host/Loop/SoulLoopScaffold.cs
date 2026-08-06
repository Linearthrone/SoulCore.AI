using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Safety;
using SoulCore.Host.Companion;
using SoulCore.Memory;
using SoulCore.Protocol;
using System.Globalization;

namespace SoulCore.Host.Loop;

/// <summary>
/// Safe want→act scaffold: proposes a want string only. Never triggers browser/MT4/email/file acts.
/// Unreal verbs are not called from this loop (optional no-op path remains elsewhere if UE down).
/// Also appends light journal notes on reflection ticks (feeling always; animation/environment by want).
/// </summary>
public sealed class SoulLoopScaffold : ISoulLoop
{
    private readonly IEmotionState _emotion;
    private readonly IMemoryStore _memory;
    private readonly IVictoriaJournalStore? _journals;
    private readonly ICompanionOutboundMessenger? _outbound;
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
        ILogger<SoulLoopScaffold> logger,
        IVictoriaJournalStore? journals = null,
        ICompanionOutboundMessenger? outbound = null)
    {
        _emotion = emotion ?? throw new ArgumentNullException(nameof(emotion));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _driftWatcher = driftWatcher ?? throw new ArgumentNullException(nameof(driftWatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _journals = journals;
        _outbound = outbound;
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
        var tick = ++_tickCount;
        var interval = _options.ReflectionIntervalTicks;
        if (interval > 0 && tick % interval == 0)
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

            if (_journals is not null)
            {
                try
                {
                    await WriteJournalNotesAsync(
                            category, label, fields, want, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "SoulLoop journal write failed (loop continues)");
                }
            }
        }

        // Victoria Link: unsolicited chat.done so Kurt gets a phone ding without chat.send.
        await MaybePushProactiveChatAsync(tick, category, label, want, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task MaybePushProactiveChatAsync(
        int tick,
        string category,
        string label,
        string want,
        CancellationToken cancellationToken)
    {
        if (_outbound is null || !_options.ProactiveChatEnabled)
            return;

        var pushEvery = _options.ProactiveChatIntervalTicks;
        if (pushEvery <= 0 || tick % pushEvery != 0)
            return;

        // Skip quiet reflection-only ticks unless reconnect/engage-family.
        if (category is SoulLoopWantProposal.CategoryReflect or SoulLoopWantProposal.CategorySettle
            && tick % (pushEvery * 2) != 0)
            return;

        // Natural SMS only — never push Inner-focus / want scaffold phrases into chat.done.
        var text = CompanionOutboundMessenger.ComposeProactiveText(category, label, want);
        if (string.IsNullOrWhiteSpace(text)
            || CompanionOutboundMessenger.ContainsScaffoldLeak(text))
        {
            _logger.LogDebug(
                "SoulLoop proactive chat skipped (no natural line) category={Category}",
                category);
            return;
        }

        try
        {
            var result = await _outbound
                .PushAsync(text, contactId: null, mediaId: null, streamDelta: false, cancellationToken)
                .ConfigureAwait(false);
            if (result.Ok)
            {
                _logger.LogInformation(
                    "SoulLoop proactive chat pushed frame={FrameId} category={Category}",
                    result.FrameId,
                    category);
            }
            else
            {
                _logger.LogDebug("SoulLoop proactive chat skipped: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SoulLoop proactive chat failed (loop continues)");
        }
    }

    private async Task WriteJournalNotesAsync(
        string category,
        string label,
        EmotionInfluencePrompt.EmotionFields fields,
        string want,
        CancellationToken cancellationToken)
    {
        var moodJson = string.Format(
            CultureInfo.InvariantCulture,
            "{{\"valence\":{0:F3},\"arousal\":{1:F3},\"dominance\":{2:F3},\"focus\":{3:F3},\"label\":\"{4}\"}}",
            fields.Valence,
            fields.Arousal,
            fields.Dominance,
            fields.Focus,
            label.Replace("\"", "", StringComparison.Ordinal));

        var feelingBody = string.Format(
            CultureInfo.InvariantCulture,
            "In this moment I feel {0} (v={1:F2}, a={2:F2}). {3}",
            label,
            fields.Valence,
            fields.Arousal,
            want);
        await _journals!
            .WriteEntryAsync(
                "feeling",
                feelingBody,
                moodJson: moodJson,
                tagsJson: "[\"soul-loop\",\"reflection\"]",
                source: "self",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (category is SoulLoopWantProposal.CategoryExplore or SoulLoopWantProposal.CategoryNotice)
        {
            await _journals
                .WriteEntryAsync(
                    "environment",
                    "I want to notice Home more carefully — rooms, light, paths, and where future modules or a workstation could live.",
                    moodJson: moodJson,
                    tagsJson: "[\"soul-loop\",\"home\",\"explore\"]",
                    source: "self",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        if (category is SoulLoopWantProposal.CategoryEngage or SoulLoopWantProposal.CategoryExplore)
        {
            await _journals
                .WriteEntryAsync(
                    "animation",
                    "I want my body to match curiosity — upright stance, a walking rhythm when I move, and a face that shows I am present.",
                    moodJson: moodJson,
                    tagsJson: "[\"soul-loop\",\"locomotion\",\"expression\"]",
                    source: "self",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogInformation("SoulLoop journal notes written (feeling + category={Category})", category);
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
