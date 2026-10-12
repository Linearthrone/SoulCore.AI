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
/// One tool-loop pass of the open assignment. The chat request can return;
/// this is what keeps the lesson moving after Kayleigh goes quiet.
/// </summary>
public sealed class AssignedWorkRunner
{
    private readonly IAssignedWorkStore _store;
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
    private string? _streakFor;

    public AssignedWorkRunner(
        IAssignedWorkStore store,
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
        var work = _store.Current;
        if (work is not { Status: "active" })
            return TimeSpan.FromSeconds(5);

        if (_spend.GetSummary().CapExceeded)
        {
            _store.MarkWaiting("Monthly spend cap. The assignment is saved.");
            Note("Waiting on you — spend cap");
            await PushStatusAsync("Waiting on you — spend cap", "waiting", cancellationToken).ConfigureAwait(false);
            return TimeSpan.FromSeconds(30);
        }

        if (!string.Equals(_streakFor, work.Text, StringComparison.Ordinal))
        {
            _streakFor = work.Text;
            _noToolStreak = 0;
        }

        var phrase = WorkContinuation.ActivityFor(work.Text);
        Note(phrase);
        await PushStatusAsync(phrase, "working", cancellationToken).ConfigureAwait(false);

        var prompt = WorkContinuation.AssignmentPrompt(work.Text, _noToolStreak);
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

        _logger.LogInformation("Assigned work pass starting. streak={Streak}", _noToolStreak);
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
        var usedWork = noting.Names.Any(WorkContinuation.IsWorkTool);
        if (usedWork)
            _noToolStreak = 0;
        else
            _noToolStreak++;

        _history.AppendTurn(WorkContinuation.PresenceSessionId, new[]
        {
            new ChatMessage { Role = "user", Content = prompt },
            new ChatMessage { Role = "assistant", Content = visible }
        });

        if (WorkContinuation.IsCheckIn(reply) || WorkContinuation.NeedsKayleigh(reply))
        {
            _store.MarkWaiting(visible);
            Note(WorkContinuation.WaitingLine);
            await BroadcastAsync(visible, continuing: false, cancellationToken).ConfigureAwait(false);
            await PushStatusAsync(WorkContinuation.WaitingLine, "waiting", cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Assigned work checked in and is waiting");
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
            "Assigned work pass finished. tools={Tools} streak={Streak}",
            string.Join(',', noting.Names),
            _noToolStreak);
        return DelayForStreak();
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

        public IReadOnlyList<ToolDefinition> GetDefinitions() => _inner.GetDefinitions();

        public async Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default)
        {
            Names.Add(name);
            _onTool(name);
            return await _inner.ExecuteAsync(name, args, ct).ConfigureAwait(false);
        }
    }
}
