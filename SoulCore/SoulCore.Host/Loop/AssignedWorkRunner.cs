using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SoulCore.Adapters.Ws;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Safety;
using SoulCore.Host.Ws;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Presence;
using SoulCore.Inference.Tooling;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Memory;
using SoulCore.Protocol;

namespace SoulCore.Host.Loop;

/// <summary>
/// One tool-loop pass of the open assignment (PROP-17.1).
/// Reads the victoria_tasks row pointed by <see cref="IAssignedWorkStore.RunningId"/>.
/// Never schedules parked/suspended. Parks after identical surface failures.
/// A tool name in the transcript is not progress.
/// </summary>
public sealed class AssignedWorkRunner
{
    private readonly IAssignedWorkStore _store;
    private readonly IVictoriaTaskStore _tasks;
    private readonly IInferenceClient _inference;
    private readonly IChatContextBuilder _contextBuilder;
    private readonly IChatSessionHistoryStore _history;
    private readonly IToolRegistry _tools;
    private readonly IToolsAccessSettings _access;
    private readonly IPresenceActivityHub? _activity;
    private readonly PresenceWsHub _hub;
    private readonly IChatTranscriptStore? _transcript;
    private readonly IEmotionState? _emotion;
    private readonly SpendMeter _spend;
    private readonly ILogger<AssignedWorkRunner> _logger;
    private int _noToolStreak;
    private long? _streakFor;
    private bool _probePass;

    public AssignedWorkRunner(
        IAssignedWorkStore store,
        IVictoriaTaskStore tasks,
        IInferenceClient inference,
        IChatContextBuilder contextBuilder,
        IChatSessionHistoryStore history,
        IToolRegistry tools,
        IToolsAccessSettings access,
        PresenceWsHub hub,
        SpendMeter spend,
        ILogger<AssignedWorkRunner> logger,
        IPresenceActivityHub? activity = null,
        IChatTranscriptStore? transcript = null,
        IEmotionState? emotion = null)
    {
        _store = store;
        _tasks = tasks;
        _inference = inference;
        _contextBuilder = contextBuilder;
        _history = history;
        _tools = tools;
        _access = access;
        _hub = hub;
        _spend = spend;
        _logger = logger;
        _activity = activity;
        _transcript = transcript;
        _emotion = emotion;
    }

    public async Task<TimeSpan> RunOnePassAsync(CancellationToken cancellationToken)
    {
        var runningId = _store.RunningId;
        if (runningId is null or <= 0)
            return TimeSpan.FromSeconds(5);

        var task = await _tasks.GetAsync(runningId.Value, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            _store.Clear();
            return TimeSpan.FromSeconds(5);
        }

        // Never schedule parked / suspended.
        if (VictoriaTaskRules.IsNeverStarted(task.Status))
        {
            if (string.Equals(task.Status, "parked", StringComparison.OrdinalIgnoreCase)
                && await TryProbeParkedAsync(task, cancellationToken).ConfigureAwait(false) is { } probeDelay)
            {
                return probeDelay;
            }

            _store.Clear();
            Note($"Parked — {task.ParkReason}".TrimEnd(' ', '—'));
            return TimeSpan.FromSeconds(30);
        }

        if (!string.Equals(task.Status, "in_progress", StringComparison.OrdinalIgnoreCase))
        {
            _store.Clear();
            return TimeSpan.FromSeconds(5);
        }

        _store.Project(task);

        if (_spend.GetSummary().CapExceeded)
        {
            await _tasks.SuspendAsync(task.Id, "Monthly spend cap. The assignment is saved.", cancellationToken)
                .ConfigureAwait(false);
            _store.Clear();
            Note("Waiting on you — spend cap");
            await PushStatusAsync("Waiting on you — spend cap", "waiting", cancellationToken).ConfigureAwait(false);
            return TimeSpan.FromSeconds(30);
        }

        if (_streakFor != task.Id)
        {
            _streakFor = task.Id;
            _noToolStreak = 0;
            _probePass = false;
        }

        var phrase = WorkContinuation.ActivityFor(task.Title);
        Note(phrase);
        await PushStatusAsync(phrase, "working", cancellationToken).ConfigureAwait(false);

        var assignmentText = string.IsNullOrWhiteSpace(task.Description) ? task.Title : task.Description;
        var prompt = WorkContinuation.AssignmentPrompt(assignmentText, _noToolStreak);
        var context = await _contextBuilder
            .BuildAsync(prompt, useToolLoop: true, _access.DesktopTargetWindowTitle, cancellationToken)
            .ConfigureAwait(false);

        var prior = _history.GetMessages(WorkContinuation.PresenceSessionId);
        var messages = new List<ChatMessage>(2 + prior.Count);
        if (!string.IsNullOrWhiteSpace(context.Preamble))
            messages.Add(new ChatMessage { Role = "system", Content = context.Preamble.Trim() });
        messages.AddRange(prior);
        messages.Add(new ChatMessage { Role = "user", Content = prompt });

        var noting = new NotingRegistry(_tools, name =>
        {
            var toolPhrase = WorkContinuation.PhraseForTool(name);
            Note(toolPhrase);
        });

        _logger.LogInformation("Assigned work pass starting. id={Id} streak={Streak}", task.Id, _noToolStreak);
        var reply = await _inference
            .CompleteWithToolsAsync(messages, _tools.GetDefinitions(), noting, cancellationToken)
            .ConfigureAwait(false);

        reply = (reply ?? "").Trim();
        if (reply.Length == 0)
        {
            _noToolStreak++;
            return DelayForStreak();
        }

        try
        {
            var inputChars = prompt.Length + (context.Preamble?.Length ?? 0);
            _spend.RecordUsage("ollama", EstimateTokens(inputChars), EstimateTokens(reply.Length));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Assigned work spend record failed");
        }

        var visible = WorkContinuation.VisibleText(reply, continuing: true);
        await ReflectEmotionAsync(visible, cancellationToken).ConfigureAwait(false);

        // PROP-17.1: a tool name is not progress. Only a successful work-tool result resets the streak.
        var workResults = noting.Results
            .Where(r => WorkContinuation.IsWorkTool(r.Name))
            .ToList();
        var anyWorkSuccess = workResults.Any(r => r.Success);
        var surfaceFailures = workResults
            .Where(r => !r.Success)
            .Select(r => SurfaceFailureKey(r.Name, r.Content, task.Surface))
            .Where(k => k is not null)
            .Cast<string>()
            .ToList();

        if (anyWorkSuccess)
        {
            _noToolStreak = 0;
            await _tasks.ClearFailureCounterAsync(task.Id, cancellationToken).ConfigureAwait(false);
            _probePass = false;
        }
        else
        {
            _noToolStreak++;
        }

        if (surfaceFailures.Count > 0)
        {
            var key = surfaceFailures[0];
            var count = await _tasks.RecordIdenticalFailureAsync(task.Id, key, cancellationToken)
                .ConfigureAwait(false);
            if (count >= VictoriaTaskRules.IdenticalFailureThreshold)
            {
                var cooldown = _probePass
                    ? VictoriaTaskRules.ReparkCooldown
                    : VictoriaTaskRules.ParkCooldown;
                var reason = OneLineParkReason(key, count);
                await _tasks.ParkAsync(task.Id, reason, DateTimeOffset.UtcNow.Add(cooldown), cancellationToken)
                    .ConfigureAwait(false);
                _store.Clear();
                _probePass = false;
                Note("Parked — " + reason);
                await PushStatusAsync("Parked — " + reason, "parked", cancellationToken).ConfigureAwait(false);
                await BroadcastAsync(visible, continuing: false, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Assigned work parked. id={Id} reason={Reason} cooldownMin={Min}",
                    task.Id, reason, cooldown.TotalMinutes);
                return TimeSpan.FromSeconds(30);
            }
        }

        // PROP-17.3: work continuation lines do not append to presence-local
        // (continuation spam buried the human thread). Band + ChatDone carry status.

        if (WorkContinuation.NeedsKayleigh(reply))
        {
            await _tasks.UpdateStatusAsync(task.Id, "blocked", cancellationToken).ConfigureAwait(false);
            _store.Clear();
            Note(WorkContinuation.WaitingLine);
            await BroadcastAsync(visible, continuing: false, cancellationToken).ConfigureAwait(false);
            await PushStatusAsync(WorkContinuation.WaitingLine, "waiting", cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Assigned work blocked waiting on Kayleigh");
            return TimeSpan.FromSeconds(15);
        }

        // Done is a real status — model must call task_update_status; no regex completion.
        var refreshed = await _tasks.GetAsync(task.Id, cancellationToken).ConfigureAwait(false);
        if (refreshed is not null
            && string.Equals(refreshed.Status, "done", StringComparison.OrdinalIgnoreCase))
        {
            _store.Clear();
            Note("Assignment done");
            await BroadcastAsync(visible, continuing: false, cancellationToken).ConfigureAwait(false);
            await PushStatusAsync("Assignment done", "done", cancellationToken).ConfigureAwait(false);
            return TimeSpan.FromSeconds(15);
        }

        var stuck = _noToolStreak >= 3;
        var activity = stuck
            ? "Stuck on the assignment — still trying"
            : phrase;
        Note(activity);
        await BroadcastAsync(visible, continuing: true, cancellationToken).ConfigureAwait(false);
        await PushStatusAsync(activity, stuck ? "waiting" : "working", cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Assigned work pass finished. id={Id} tools={Tools} streak={Streak}",
            task.Id,
            string.Join(',', noting.Names),
            _noToolStreak);
        return DelayForStreak();
    }

    /// <summary>
    /// After park_until: one probe. Still down → repark 120m. Up → leave as parked for operator release
    /// unless we set in_progress for the single probe attempt via running pointer.
    /// </summary>
    private async Task<TimeSpan?> TryProbeParkedAsync(VictoriaTask task, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(task.ParkUntil)
            || !DateTimeOffset.TryParse(
                task.ParkUntil,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var until)
            || until > DateTimeOffset.UtcNow)
        {
            return null;
        }

        // One probe: briefly resume in_progress, clear failures, set running pointer.
        await _tasks.UpdateStatusAsync(task.Id, "in_progress", cancellationToken).ConfigureAwait(false);
        await _tasks.ClearFailureCounterAsync(task.Id, cancellationToken).ConfigureAwait(false);
        var resumed = await _tasks.GetAsync(task.Id, cancellationToken).ConfigureAwait(false);
        if (resumed is null)
            return TimeSpan.FromSeconds(5);
        _probePass = true;
        _store.Project(resumed);
        Note("Probe after park — " + task.Title);
        await PushStatusAsync("Probe after park", "working", cancellationToken).ConfigureAwait(false);
        // Fall through by returning null is wrong — caller already branched. Re-enter via next loop.
        // Instead run one pass inline by returning a short delay after setting pointer; HostedService will call again.
        return TimeSpan.FromSeconds(1);
    }

    private static string? SurfaceFailureKey(string name, string? content, string surface)
    {
        var c = content ?? "";
        if (c.Contains("park", StringComparison.OrdinalIgnoreCase)
            && c.Contains("site down", StringComparison.OrdinalIgnoreCase))
            return "browser:site_down";
        if (name.StartsWith("browser_", StringComparison.Ordinal))
        {
            if (c.Contains("refused", StringComparison.OrdinalIgnoreCase)
                || c.Contains("unreachable", StringComparison.OrdinalIgnoreCase)
                || c.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                || c.Contains("ERR_", StringComparison.OrdinalIgnoreCase)
                || c.Contains("net::", StringComparison.OrdinalIgnoreCase)
                || c.Contains("HTTP 5", StringComparison.OrdinalIgnoreCase)
                || c.Contains("no response", StringComparison.OrdinalIgnoreCase)
                || c.Contains("health", StringComparison.OrdinalIgnoreCase) && c.Contains("fail", StringComparison.OrdinalIgnoreCase))
            {
                return "browser:" + ExtractUrlToken(c);
            }
        }

        if (name.StartsWith("desktop_", StringComparison.Ordinal) || name.StartsWith("guest_", StringComparison.Ordinal))
        {
            if (c.Contains("Maestro", StringComparison.OrdinalIgnoreCase)
                && (c.Contains("unreachable", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("not running", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("failed", StringComparison.OrdinalIgnoreCase)))
                return "maestro:unreachable";
            if (c.Contains("VM", StringComparison.OrdinalIgnoreCase)
                || c.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase)
                || c.Contains("guestcontrol", StringComparison.OrdinalIgnoreCase)
                || c.Contains("not running", StringComparison.OrdinalIgnoreCase))
            {
                if (c.Contains("fail", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("not running", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("offline", StringComparison.OrdinalIgnoreCase)
                    || c.Contains("unreachable", StringComparison.OrdinalIgnoreCase))
                    return "vm:off";
            }
        }

        if (!string.IsNullOrWhiteSpace(surface)
            && (c.Contains("fail", StringComparison.OrdinalIgnoreCase)
                || c.Contains("error", StringComparison.OrdinalIgnoreCase)
                || c.Contains("unreachable", StringComparison.OrdinalIgnoreCase)))
        {
            return surface.Trim().ToLowerInvariant() + ":" + name;
        }

        return null;
    }

    private static string ExtractUrlToken(string content)
    {
        var idx = content.IndexOf("http", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return "health";
        var end = idx;
        while (end < content.Length && !char.IsWhiteSpace(content[end]) && content[end] != '"' && content[end] != '\'')
            end++;
        var url = content[idx..end];
        return url.Length > 120 ? url[..120] : url;
    }

    private static string OneLineParkReason(string key, int count)
    {
        if (key.StartsWith("maestro:", StringComparison.Ordinal))
            return $"Maestro unreachable ({count} identical failures)";
        if (key.StartsWith("vm:", StringComparison.Ordinal))
            return $"VM surface down ({count} identical failures)";
        if (key.StartsWith("browser:", StringComparison.Ordinal))
            return $"Browser/site down ({count} identical failures)";
        return $"Surface failure ({count} identical): {key}";
    }

    private async Task ReflectEmotionAsync(string reply, CancellationToken cancellationToken)
    {
        if (_emotion is null || string.IsNullOrWhiteSpace(reply))
            return;
        try
        {
            var current = await _emotion.GetAsync(cancellationToken).ConfigureAwait(false);
            var next = EmotionInfluencePrompt.ReflectSpeech(reply, current);
            var before = EmotionInfluencePrompt.ReadFields(current);
            var after = EmotionInfluencePrompt.ReadFields(next);
            if (Math.Abs(before.Valence - after.Valence) < 0.0001
                && Math.Abs(before.Arousal - after.Arousal) < 0.0001
                && Math.Abs(before.Focus - after.Focus) < 0.0001)
                return;
            await _emotion.SetAsync(next, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Assigned work emotion update failed");
        }
    }

    private static long EstimateTokens(int charCount) =>
        charCount <= 0 ? 0 : (long)Math.Ceiling(charCount / 4.0);

    private TimeSpan DelayForStreak()
    {
        if (_noToolStreak <= 0)
            return TimeSpan.FromSeconds(4);
        var seconds = Math.Min(120, 15 * _noToolStreak);
        return TimeSpan.FromSeconds(seconds);
    }

    private void Note(string phrase) => _activity?.NoteTool(phrase);

    private async Task BroadcastAsync(string text, bool continuing, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        var frameId = Guid.NewGuid().ToString("N");
        try
        {
            var done = SoulCoreFrame.Create(
                SoulCoreFrameTypes.ChatDone,
                new { text, stub = false, provider = "assigned-work", continuing },
                id: frameId);
            await _hub.SendAsync(done.ToJson(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Assigned work broadcast failed");
        }

        if (_transcript is null)
            return;
        try
        {
            await _transcript.AppendAsync(
                WorkContinuation.PresenceSessionId,
                "assistant",
                text,
                "desk",
                frameId,
                null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Assigned work transcript append failed");
        }
    }

    private async Task PushStatusAsync(string phrase, string state, CancellationToken cancellationToken)
    {
        try
        {
            var frame = SoulCoreFrame.Create(
                SoulCoreFrameTypes.PresenceStatus,
                new { alive = true, warm = true, phase = 1, activity = phrase, state });
            await _hub.SendAsync(frame.ToJson(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Assigned work status push failed");
        }
    }

    private sealed class NotingRegistry : IToolRegistry
    {
        private readonly IToolRegistry _inner;
        private readonly Action<string> _onTool;

        public NotingRegistry(IToolRegistry inner, Action<string> onTool)
        {
            _inner = inner;
            _onTool = onTool;
        }

        public HashSet<string> Names { get; } = new(StringComparer.Ordinal);
        public List<(string Name, bool Success, string Content)> Results { get; } = new();

        public IReadOnlyList<ToolDefinition> GetDefinitions() => _inner.GetDefinitions();

        public async Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default)
        {
            Names.Add(name);
            _onTool(name);
            var result = await _inner.ExecuteAsync(name, args, ct).ConfigureAwait(false);
            Results.Add((name, result.Success, result.Content ?? ""));
            return result;
        }
    }
}
