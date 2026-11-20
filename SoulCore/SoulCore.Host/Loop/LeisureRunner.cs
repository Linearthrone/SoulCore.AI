using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Presence;
using SoulCore.Inference.Tooling;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Memory;
using SoulCore.Protocol;

namespace SoulCore.Host.Loop;

/// <summary>
/// PROP-17.3 / PROP-20.6: one R&amp;R pass. Prefer local LeisureModel when it can
/// hold the mind block; otherwise the same pass runs on the desk chat model.
/// Self-started research (no task / no release). Action proposals → suspended.
/// Never appends work/R&amp;R continuations to <c>presence-local</c>.
/// Writes / speak-as-her only when the pass prompt carried floors + resting.
/// </summary>
public sealed class LeisureRunner
{
    public const string HttpClientName = "ollama-leisure";

    private readonly IHttpClientFactory _httpFactory;
    private readonly InferenceOptions _options;
    private readonly IToolRegistry _tools;
    private readonly IVictoriaTaskStore _tasks;
    private readonly IVictoriaJournalStore? _journals;
    private readonly IVictoriaResearchStore? _research;
    private readonly IActivityResidueWriter? _residue;
    private readonly IInferenceLease _lease;
    private readonly ILeisureSession _session;
    private readonly IPresenceActivityHub? _activity;
    private readonly PresenceWsHub _hub;
    private readonly IUeLiveSignal _ueLive;
    private readonly IBlockedAsideNotice _aside;
    private readonly ILogger<LeisureRunner> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IInferenceClient? _deskInference;

    public LeisureRunner(
        IHttpClientFactory httpFactory,
        IOptions<InferenceOptions> options,
        IToolRegistry tools,
        IVictoriaTaskStore tasks,
        IInferenceLease lease,
        ILeisureSession session,
        PresenceWsHub hub,
        IUeLiveSignal ueLive,
        ILogger<LeisureRunner> logger,
        ILoggerFactory loggerFactory,
        IBlockedAsideNotice aside,
        IPresenceActivityHub? activity = null,
        IVictoriaJournalStore? journals = null,
        IVictoriaResearchStore? research = null,
        IInferenceClient? deskInference = null,
        IActivityResidueWriter? residue = null)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _tools = tools;
        _tasks = tasks;
        _lease = lease;
        _session = session;
        _hub = hub;
        _ueLive = ueLive;
        _aside = aside;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _activity = activity;
        _journals = journals;
        _research = research;
        _deskInference = deskInference;
        _residue = residue;
    }

    /// <summary>
    /// Run one leisure pass under an already-held leisure lease.
    /// Returns idle delay. Quiet when model missing / VRAM contended / aborted.
    /// </summary>
    public async Task<TimeSpan> RunOnePassAsync(CancellationToken abortToken)
    {
        // PROP-18.19 / F2: B3 exemption must go through IsExemptKind — leisure never
        // injects or calls IIdleParkedWorkBudget (catalog drift fails loud).
        if (!FileIdleParkedWorkBudget.IsExemptKind("leisure"))
        {
            throw new InvalidOperationException(
                "B3 catalog drift: leisure must remain IsExemptKind — do not consume idle parked budget.");
        }

        var leisureModel = InferenceModelRouting.ResolveLeisureModel(_options);
        if (leisureModel is null)
        {
            _logger.LogDebug("LeisureModel empty — R&R stays quiet (no work-model fallback)");
            NoteQuiet();
            return TimeSpan.FromSeconds(45);
        }

        if (_ueLive.IsUeLive)
        {
            _logger.LogInformation("R&R quiet — Unreal live (VRAM)");
            NoteQuiet();
            return TimeSpan.FromSeconds(60);
        }

        var interest = _session.CurrentInterest;
        if (string.IsNullOrWhiteSpace(interest))
        {
            interest = LeisurePrompt.DefaultInterest;
            _session.BeginOrContinue(interest);
        }

        await EnsureResearchSubjectAsync(interest!, abortToken).ConfigureAwait(false);

        // PROP-20.6: mind block (floors + resting + interest reason) or do not run as her.
        var related = RelatedMemoriesForInterest(interest!);
        var mindBlock = LeisureMindBlock.Build(interest!, related);
        if (!LeisureMindBlock.MayWriteAsHer(mindBlock))
        {
            _logger.LogWarning("R&R quiet — mind block incomplete (floors/resting required)");
            NoteQuiet();
            return TimeSpan.FromSeconds(45);
        }

        var passModel = LeisureMindBlock.ResolvePassModel(_options, mindBlock);
        var useSmallLeisure = LeisureMindBlock.UsesLeisureSmallModel(_options, mindBlock);
        if (!useSmallLeisure)
        {
            _logger.LogInformation(
                "R&R mind block exceeds small leisure capacity ({Chars} chars) — pass on desk chat model {Model}",
                mindBlock.Length,
                passModel);
        }

        // Mark the pass live so Presence "On her mind" is honest only while R&R is actually running.
        _session.NotePassCompleted();
        var phrase = _session.ActivityLine();
        _activity?.NoteTool(phrase);
        await PushStatusAsync(phrase, "leisure", abortToken).ConfigureAwait(false);
        await AnnounceUnfinishedWorkOnceAsync(abortToken).ConfigureAwait(false);

        if (useSmallLeisure
            && !await ModelPresentLocalAsync(leisureModel!, abortToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Leisure model {Model} not pulled — R&R stays quiet", leisureModel);
            NoteQuiet();
            return TimeSpan.FromSeconds(90);
        }

        if (!useSmallLeisure && _deskInference is null)
        {
            _logger.LogInformation("R&R desk-model fallback needed but desk inference unavailable — quiet");
            NoteQuiet();
            return TimeSpan.FromSeconds(90);
        }

        _lease.Touch(InferenceLease.LeisureHolder);

        var system = mindBlock + "\n" + LeisurePrompt.SystemGuidance(interest!);
        if (!LeisureMindBlock.MayWriteAsHer(system))
        {
            _logger.LogWarning("R&R quiet — assembled system prompt lost mind block");
            NoteQuiet();
            return TimeSpan.FromSeconds(45);
        }

        var user = LeisurePrompt.UserTurn(interest!, _session.LastUntrustedSummary);
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = system },
            new() { Role = "user", Content = user }
        };

        var filtered = LeisureTools.FilterRegistry(_tools);
        var noting = new NotingRegistry(filtered, name =>
        {
            _activity?.NoteTool(WorkContinuation.PhraseForTool(name));
            _lease.Touch(InferenceLease.LeisureHolder);
        });

        IInferenceClient client = useSmallLeisure
            ? new OllamaInferenceClient(
                _httpFactory.CreateClient(HttpClientName),
                Options.Create(_options),
                _loggerFactory.CreateLogger<OllamaInferenceClient>(),
                toolRegistry: null,
                _ueLive)
            : _deskInference!;

        string reply;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(abortToken);
            reply = await client.CompleteWithToolsAsync(
                    messages,
                    filtered.GetDefinitions(),
                    noting,
                    linked.Token,
                    new ToolLoopOptions { ModelOverride = passModel })
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (abortToken.IsCancellationRequested)
        {
            _logger.LogInformation("R&R aborted by human/SMS");
            _session.Clear();
            return TimeSpan.FromSeconds(5);
        }
        catch (Exception ex) when (IsMissingModel(ex))
        {
            _logger.LogInformation(ex, "Leisure/desk model missing/contended — quiet");
            NoteQuiet();
            return TimeSpan.FromSeconds(90);
        }

        reply = (reply ?? "").Trim();
        if (reply.Length == 0)
        {
            NoteQuiet();
            return TimeSpan.FromSeconds(30);
        }

        // PROP-20.6: never write or speak as her unless this pass carried the mind block.
        var mayWriteAsHer = LeisureMindBlock.MayWriteAsHer(system);

        if (ActionProposalClassifier.TryClassify(reply, out var proposal))
        {
            if (!mayWriteAsHer)
            {
                _logger.LogInformation("R&R proposal suppressed — mind block absent from pass prompt");
            }
            else
            {
                try
                {
                    await _tasks.CreateAsync(
                            proposal.Title,
                            proposal.Description,
                            "medium",
                            surface: "local",
                            ready: false,
                            status: "suspended",
                            notes: "R&R action proposal (" + proposal.Kind + ")",
                            abortToken)
                        .ConfigureAwait(false);
                    _logger.LogInformation("R&R filed suspended proposal kind={Kind}", proposal.Kind);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "R&R suspended proposal insert failed");
                }
            }
        }

        if (mayWriteAsHer && noting.UntrustedChunks.Count > 0)
        {
            var raw = string.Join("\n", noting.UntrustedChunks);
            var summary = UntrustedObservation.KeepSummary(raw, interest);
            _session.NoteUntrustedSummary(summary);
            await WriteJournalNoteAsync(summary, abortToken).ConfigureAwait(false);
            // PROP-18.7: activities log + topic journal from this pass's findings.
            await WriteActivityResidueAsync(
                    interest!,
                    raw,
                    researchPass: noting.Names.Any(n =>
                        n.StartsWith("research_", StringComparison.OrdinalIgnoreCase)),
                    abortToken)
                .ConfigureAwait(false);
        }
        else if (mayWriteAsHer
                 && LooksLikeWorthReading(reply)
                 && !BlockedAside.IsWhySheIsntWorking(reply))
        {
            var summary = UntrustedObservation.KeepSummary(reply, interest);
            _session.NoteUntrustedSummary(summary);
            await WriteJournalNoteAsync(summary, abortToken).ConfigureAwait(false);
            await WriteActivityResidueAsync(
                    interest!,
                    reply,
                    researchPass: noting.Names.Any(n =>
                        n.StartsWith("research_", StringComparison.OrdinalIgnoreCase)),
                    abortToken)
                .ConfigureAwait(false);
            await BroadcastFindAsync(VisibleFind(reply), abortToken).ConfigureAwait(false);
        }
        else if (!mayWriteAsHer)
        {
            _logger.LogInformation("R&R journal/find suppressed — mind block absent from pass prompt");
        }

        _session.NotePassCompleted();
        _activity?.NoteTool(_session.ActivityLine());
        await PushStatusAsync(_session.ActivityLine(), "leisure", abortToken).ConfigureAwait(false);
        return TimeSpan.FromSeconds(20);
    }

    private static IReadOnlyList<string> RelatedMemoriesForInterest(string interest)
    {
        // Lightweight interest-tied stubs until PROP-20.4 shared recall lands.
        // Real episodic lines are preferred when the shared builder supplies them.
        var topic = interest.Trim();
        if (topic.Length == 0)
            return Array.Empty<string>();
        return new[]
        {
            "R&R thread: continuing curiosity about " + topic + "."
        };
    }

    private async Task EnsureResearchSubjectAsync(string interest, CancellationToken cancellationToken)
    {
        if (_research is null || string.IsNullOrWhiteSpace(interest))
            return;
        try
        {
            await _research.OpenOrCreateSubjectAsync(
                    interest.Trim(),
                    summary: "Self-started R&R thread",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "R&R research subject open failed");
        }
    }

    private async Task<bool> ModelPresentLocalAsync(string model, CancellationToken cancellationToken)
    {
        try
        {
            var http = _httpFactory.CreateClient(HttpClientName);
            using var resp = await http.GetAsync("api/tags", cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return false;
            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("models", out var models)
                || models.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var m in models.EnumerateArray())
            {
                if (!m.TryGetProperty("name", out var nameProp) || nameProp.GetString() is not { } name)
                    continue;
                if (string.Equals(name, model, StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith(model, StringComparison.OrdinalIgnoreCase)
                    || model.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Leisure api/tags probe failed — treating as quiet");
            return false;
        }
    }

    private static bool IsMissingModel(Exception ex)
    {
        var msg = ex.Message ?? "";
        return msg.Contains("not found", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("pull", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("404", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("model", StringComparison.OrdinalIgnoreCase)
                  && msg.Contains("load", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeWorthReading(string reply) =>
        reply.Length >= 80
        && !reply.Contains("quiet", StringComparison.OrdinalIgnoreCase);

    private static string VisibleFind(string reply) =>
        reply.Length > 400 ? reply[..397] + "…" : reply;

    private async Task AnnounceUnfinishedWorkOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<VictoriaTask> rows;
        try
        {
            rows = await _tasks.ListAsync(null, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "R&R aside skipped — task list failed");
            return;
        }

        if (!rows.Any(BlockedAside.CountsAsUnfinishedBlock))
            return;
        if (!_aside.TryClaim())
            return;

        await BroadcastFindAsync(BlockedAside.Line, cancellationToken).ConfigureAwait(false);
    }

    private void NoteQuiet()
    {
        _activity?.NoteTool("Between talks · quiet");
    }

    private async Task WriteJournalNoteAsync(string summary, CancellationToken cancellationToken)
    {
        if (_journals is null || string.IsNullOrWhiteSpace(summary))
            return;
        // PROP-18.19 / F2: journal kind is B3-exempt via IsExemptKind (no parallel kind list).
        if (!FileIdleParkedWorkBudget.IsExemptKind("journal"))
        {
            throw new InvalidOperationException(
                "B3 catalog drift: journal must remain IsExemptKind — do not consume idle parked budget.");
        }

        try
        {
            await _journals.WriteEntryAsync(
                    "life_hacks",
                    summary,
                    moodJson: null,
                    tagsJson: """["r&r","untrusted"]""",
                    source: "leisure",
                    occurredAt: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "R&R journal note failed");
        }
    }

    /// <summary>PROP-18.7: non-generic activities log + topic journal (Q1/Q4 depth law).</summary>
    private async Task WriteActivityResidueAsync(
        string interest,
        string findings,
        bool researchPass,
        CancellationToken cancellationToken)
    {
        if (_residue is null || string.IsNullOrWhiteSpace(findings))
            return;

        // PROP-18.19 / F2: leisure residue stays B3-exempt through IsExemptKind.
        // Research passes are not in the exempt catalog (distinct from leisure).
        var activityKind = researchPass ? "research" : "leisure";
        if (!researchPass && !FileIdleParkedWorkBudget.IsExemptKind(activityKind))
        {
            throw new InvalidOperationException(
                "B3 catalog drift: leisure activity-log residue must remain IsExemptKind.");
        }

        try
        {
            await _residue.TryWriteAsync(
                    activityKind,
                    interest,
                    findings,
                    occurredAt: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "R&R activity residue write failed");
        }
    }

    private async Task BroadcastFindAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        try
        {
            var done = SoulCoreFrame.Create(
                SoulCoreFrameTypes.ChatDone,
                new { text, stub = false, provider = "leisure", continuing = false },
                id: Guid.NewGuid().ToString("N"));
            await _hub.SendAsync(done.ToJson(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "R&R find broadcast failed");
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
            _logger.LogDebug(ex, "R&R status push failed");
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

        public List<string> UntrustedChunks { get; } = new();
        public List<string> Names { get; } = new();

        public IReadOnlyList<ToolDefinition> GetDefinitions() => _inner.GetDefinitions();

        public async Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default)
        {
            Names.Add(name);
            _onTool(name);
            var result = await _inner.ExecuteAsync(name, args, ct).ConfigureAwait(false);
            if (result.Content is { Length: > 0 }
                && (name.Contains("snapshot", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("screenshot", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("read", StringComparison.OrdinalIgnoreCase)))
            {
                var wrapped = UntrustedObservation.WrapForTurn(result.Content);
                UntrustedChunks.Add(wrapped);
                return result with { Content = wrapped };
            }

            return result;
        }
    }
}
