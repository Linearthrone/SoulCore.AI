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
/// PROP-17.3: one R&amp;R pass on the local LeisureModel.
/// Self-started research (no task / no release). Action proposals → suspended.
/// Never appends work/R&amp;R continuations to <c>presence-local</c>.
/// </summary>
public sealed class LeisureRunner
{
    public const string HttpClientName = "ollama-leisure";

    private readonly IHttpClientFactory _httpFactory;
    private readonly InferenceOptions _options;
    private readonly IToolRegistry _tools;
    private readonly IVictoriaTaskStore _tasks;
    private readonly IVictoriaJournalStore? _journals;
    private readonly IInferenceLease _lease;
    private readonly ILeisureSession _session;
    private readonly IPresenceActivityHub? _activity;
    private readonly PresenceWsHub _hub;
    private readonly IUeLiveSignal _ueLive;
    private readonly ILogger<LeisureRunner> _logger;
    private readonly ILoggerFactory _loggerFactory;

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
        IPresenceActivityHub? activity = null,
        IVictoriaJournalStore? journals = null)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _tools = tools;
        _tasks = tasks;
        _lease = lease;
        _session = session;
        _hub = hub;
        _ueLive = ueLive;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _activity = activity;
        _journals = journals;
    }

    /// <summary>
    /// Run one leisure pass under an already-held leisure lease.
    /// Returns idle delay. Quiet when model missing / VRAM contended / aborted.
    /// </summary>
    public async Task<TimeSpan> RunOnePassAsync(CancellationToken abortToken)
    {
        var leisureModel = InferenceModelRouting.ResolveLeisureModel(_options);
        if (leisureModel is null)
        {
            _logger.LogDebug("LeisureModel empty — R&R stays quiet (no work-model fallback)");
            NoteQuiet();
            return TimeSpan.FromSeconds(45);
        }

        if (_ueLive.IsUeLive)
        {
            // Unreal/voice need the card — stay quiet; never borrow work model.
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

        var phrase = _session.ActivityLine();
        _activity?.NoteTool(phrase);
        await PushStatusAsync(phrase, "leisure", abortToken).ConfigureAwait(false);

        if (!await ModelPresentLocalAsync(leisureModel, abortToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Leisure model {Model} not pulled — R&R stays quiet", leisureModel);
            NoteQuiet();
            return TimeSpan.FromSeconds(90);
        }

        _lease.Touch(InferenceLease.LeisureHolder);

        var http = _httpFactory.CreateClient(HttpClientName);
        var client = new OllamaInferenceClient(
            http,
            Options.Create(_options),
            _loggerFactory.CreateLogger<OllamaInferenceClient>(),
            toolRegistry: null,
            _ueLive);

        var system = LeisurePrompt.SystemGuidance(interest!);
        var user = LeisurePrompt.UserTurn(interest!, _session.LastUntrustedSummary);
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = system },
            new() { Role = "user", Content = user }
        };

        // R&R uses its own turn buffer — never presence-local history.
        var filtered = LeisureTools.FilterRegistry(_tools);
        var noting = new NotingRegistry(filtered, name =>
        {
            _activity?.NoteTool(WorkContinuation.PhraseForTool(name));
            _lease.Touch(InferenceLease.LeisureHolder);
        });

        string reply;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(abortToken);
            reply = await client.CompleteWithToolsAsync(
                    messages,
                    filtered.GetDefinitions(),
                    noting,
                    linked.Token,
                    new ToolLoopOptions { ModelOverride = leisureModel })
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
            _logger.LogInformation(ex, "Leisure model missing/contended — quiet");
            NoteQuiet();
            return TimeSpan.FromSeconds(90);
        }

        reply = (reply ?? "").Trim();
        if (reply.Length == 0)
        {
            NoteQuiet();
            return TimeSpan.FromSeconds(30);
        }

        // Action proposals → suspended; never start-now.
        if (ActionProposalClassifier.TryClassify(reply, out var proposal))
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

        // Untrusted page/tool text → kept summary for books; not presence-local.
        if (noting.UntrustedChunks.Count > 0)
        {
            var raw = string.Join("\n", noting.UntrustedChunks);
            var summary = UntrustedObservation.KeepSummary(raw, interest);
            _session.NoteUntrustedSummary(summary);
            await WriteJournalNoteAsync(summary, abortToken).ConfigureAwait(false);
        }
        else if (LooksLikeWorthReading(reply))
        {
            var summary = UntrustedObservation.KeepSummary(reply, interest);
            _session.NoteUntrustedSummary(summary);
            await WriteJournalNoteAsync(summary, abortToken).ConfigureAwait(false);
            // One find worth reading = one message (not a continuation spam).
            await BroadcastFindAsync(VisibleFind(reply), abortToken).ConfigureAwait(false);
        }

        _activity?.NoteTool(_session.ActivityLine());
        await PushStatusAsync(_session.ActivityLine(), "leisure", abortToken).ConfigureAwait(false);
        // No wall-clock kill — living research thread continues next pass.
        return TimeSpan.FromSeconds(20);
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
                if (m.TryGetProperty("name", out var nameProp)
                    && nameProp.GetString() is { } name
                    && (string.Equals(name, model, StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith(model + ":", StringComparison.OrdinalIgnoreCase)
                        || model.StartsWith(name.Split(':')[0], StringComparison.OrdinalIgnoreCase)
                           && name.StartsWith(model.Split(':')[0], StringComparison.OrdinalIgnoreCase)))
                {
                    // Prefer exact or tag match.
                    if (string.Equals(name, model, StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith(model, StringComparison.OrdinalIgnoreCase)
                        || model.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // Exact scan
            foreach (var m in models.EnumerateArray())
            {
                if (m.TryGetProperty("name", out var nameProp)
                    && string.Equals(nameProp.GetString(), model, StringComparison.OrdinalIgnoreCase))
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

    private void NoteQuiet()
    {
        _activity?.NoteTool("Between talks · quiet");
    }

    private async Task WriteJournalNoteAsync(string summary, CancellationToken cancellationToken)
    {
        if (_journals is null || string.IsNullOrWhiteSpace(summary))
            return;
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

        public IReadOnlyList<ToolDefinition> GetDefinitions() => _inner.GetDefinitions();

        public async Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default)
        {
            _onTool(name);
            var result = await _inner.ExecuteAsync(name, args, ct).ConfigureAwait(false);
            // Page/screenshot text stays in this R&R turn as untrusted.
            if (result.Content is { Length: > 0 }
                && (name.Contains("snapshot", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("screenshot", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("read", StringComparison.OrdinalIgnoreCase)))
            {
                UntrustedChunks.Add(UntrustedObservation.WrapForTurn(result.Content));
                return result with { Content = UntrustedObservation.WrapForTurn(result.Content) };
            }

            return result;
        }
    }
}
