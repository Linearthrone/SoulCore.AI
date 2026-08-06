using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Charter;
using SoulCore.Core.Safety;
using SoulCore.Hermes;
using SoulCore.Host;
using SoulCore.Host.Companion;
using SoulCore.Host.Loop;
using SoulCore.Host.Voice;
using SoulCore.Host.Ws;
using SoulCore.Inference;
using SoulCore.Inference.Tools;
using SoulCore.Inference.Tools.Body;
using SoulCore.Inference.Tools.Browser;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Inference.Tools.FS;
using SoulCore.Inference.Tools.Meta;
using SoulCore.Inference.Tools.Trading;
using SoulCore.Inference.Tools.Workflow;
using SoulCore.Memory;
using System.Text.Json;

// Local SoulCore/.env → process env (SOULCORE_* only) before any config bind.
// Existing non-empty process env wins; never log secret values.
DotEnvLoader.TryLoad();

// Evidence mode: confirm SecretNames keys present (length/bool only — no values).
if (args.Any(a => string.Equals(a, "--secrets-presence", StringComparison.OrdinalIgnoreCase)))
{
    return ReportSecretsPresence();
}

// Evidence mode: write emotion → dispose → reopen → verify (no web host).
if (args.Any(a => string.Equals(a, "--emotion-roundtrip", StringComparison.OrdinalIgnoreCase)))
{
    var pathArg = args.SkipWhile(a => !string.Equals(a, "--emotion-roundtrip", StringComparison.OrdinalIgnoreCase))
        .Skip(1)
        .FirstOrDefault(a => !a.StartsWith('-'));
    return await EmotionRoundTrip.RunAsync(pathArg);
}

// Evidence mode: SoulLoop tick on/off (no web host).
if (args.Any(a => string.Equals(a, "--soul-loop-tick", StringComparison.OrdinalIgnoreCase)))
{
    var enabled = args.Any(a => string.Equals(a, "--enabled", StringComparison.OrdinalIgnoreCase));
    return await SoulLoopTickEvidence.RunAsync(enabled);
}

// CLI: backfill missing episodic embeddings via Ollama, then exit (no web host).
if (args.Any(a => string.Equals(a, "--backfill-embeddings", StringComparison.OrdinalIgnoreCase)))
{
    return await BackfillEmbeddings.RunAsync(args);
}

var builder = WebApplication.CreateBuilder(args);

// User-secrets / env for secrets — never App.config tokens from quarry.
builder.Configuration.AddEnvironmentVariables(prefix: "SOULCORE_");
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
}

builder.Services.Configure<HostBindOptions>(
    builder.Configuration.GetSection(HostBindOptions.SectionName));
builder.Services.Configure<MemoryOptions>(
    builder.Configuration.GetSection(MemoryOptions.SectionName));
builder.Services.Configure<InferenceOptions>(
    builder.Configuration.GetSection(InferenceOptions.SectionName));
builder.Services.Configure<HermesOptions>(
    builder.Configuration.GetSection(HermesOptions.SectionName));
builder.Services.Configure<UnrealBridgeOptions>(
    builder.Configuration.GetSection(UnrealBridgeOptions.SectionName));
builder.Services.Configure<VoiceOptions>(
    builder.Configuration.GetSection(VoiceOptions.SectionName));
builder.Services.Configure<ChatWsOptions>(
    builder.Configuration.GetSection(ChatWsOptions.SectionName));
builder.Services.Configure<SoulLoopOptions>(
    builder.Configuration.GetSection(SoulLoopOptions.SectionName));
builder.Services.Configure<CompanionOptions>(
    builder.Configuration.GetSection(CompanionOptions.SectionName));
builder.Services.Configure<SafetyOptions>(
    builder.Configuration.GetSection(SafetyOptions.SectionName));
builder.Services.Configure<ToolsOptions>(
    builder.Configuration.GetSection(ToolsOptions.SectionName));

var bindOptions = builder.Configuration
    .GetSection(HostBindOptions.SectionName)
    .Get<HostBindOptions>() ?? new HostBindOptions();

var inferenceOptions = builder.Configuration
    .GetSection(InferenceOptions.SectionName)
    .Get<InferenceOptions>() ?? new InferenceOptions();

var hermesOptions = builder.Configuration
    .GetSection(HermesOptions.SectionName)
    .Get<HermesOptions>() ?? new HermesOptions();

var unrealOptions = builder.Configuration
    .GetSection(UnrealBridgeOptions.SectionName)
    .Get<UnrealBridgeOptions>() ?? new UnrealBridgeOptions();

var voiceOptions = builder.Configuration
    .GetSection(VoiceOptions.SectionName)
    .Get<VoiceOptions>() ?? new VoiceOptions();

var chatWsOptions = builder.Configuration
    .GetSection(ChatWsOptions.SectionName)
    .Get<ChatWsOptions>() ?? new ChatWsOptions();

var soulLoopOptions = builder.Configuration
    .GetSection(SoulLoopOptions.SectionName)
    .Get<SoulLoopOptions>() ?? new SoulLoopOptions();

var memoryOptions = builder.Configuration
    .GetSection(MemoryOptions.SectionName)
    .Get<MemoryOptions>() ?? new MemoryOptions();

var safetyOptions = builder.Configuration
    .GetSection(SafetyOptions.SectionName)
    .Get<SafetyOptions>() ?? new SafetyOptions();

var toolsOptions = builder.Configuration
    .GetSection(ToolsOptions.SectionName)
    .Get<ToolsOptions>() ?? new ToolsOptions();

// BED-161 / ISSUE-009: Hermes API key preflight — never read secrets from git.
// Warn (do not crash) when Hermes is enabled or PreferHermes is set but the key
// is missing; chat/MCP will fail-fast at call time with a clear message.
{
    var hermesKeyPresent = !string.IsNullOrWhiteSpace(
        Environment.GetEnvironmentVariable(SecretNames.HermesApiKey))
        || !string.IsNullOrWhiteSpace(hermesOptions.ApiKey);
    if ((hermesOptions.Enabled || chatWsOptions.PreferHermes) && !hermesKeyPresent)
    {
        Console.Error.WriteLine(
            $"[SoulCore] WARNING: Hermes.Enabled={hermesOptions.Enabled} PreferHermes={chatWsOptions.PreferHermes} " +
            $"but {SecretNames.HermesApiKey} is not set (env/user-secrets). " +
            "PreferHermes MCP preflight and hermes-backend MCP tools will fail-fast until the key is provided. " +
            "Do not put ApiKey values in appsettings.json.");
    }

    if (!string.IsNullOrWhiteSpace(hermesOptions.ApiKey))
    {
        Console.Error.WriteLine(
            $"[SoulCore] WARNING: Hermes:ApiKey is set in configuration. Prefer env " +
            $"{SecretNames.HermesApiKey} / user-secrets — never commit API keys.");
    }
}

// SEC-004: V1 bind = 127.0.0.1 only. Refuse non-loopback without explicit future SEC gate.
if (!IsLoopback(bindOptions.BindAddress))
{
    throw new InvalidOperationException(
        $"SoulCore V1 refuses non-loopback bind '{bindOptions.BindAddress}'. " +
        "Use 127.0.0.1 only (SEC-004).");
}

builder.WebHost.UseUrls($"http://{bindOptions.BindAddress}:{bindOptions.Port}");

builder.Services.AddSingleton<SqliteMemoryStore>();
builder.Services.AddSingleton<IMemoryStore>(sp => sp.GetRequiredService<SqliteMemoryStore>());
builder.Services.AddSingleton<IEmotionState>(sp => sp.GetRequiredService<SqliteMemoryStore>());
// BED-140: Victoria's own task store (victoria_tasks table). Separate from
// PM tickets under docs/agents/tasks/ — those are human-authored orchestration
// artifacts; this store is model-managed via task_* tools.
builder.Services.AddSingleton<IVictoriaTaskStore>(sp => sp.GetRequiredService<SqliteMemoryStore>());
// BED-141: Victoria's workflow store (victoria_workflows) — ordered steps via workflow_* tools.
builder.Services.AddSingleton<IVictoriaWorkflowStore>(sp => sp.GetRequiredService<SqliteMemoryStore>());
// Victoria journals: feeling / animation / environment notebooks.
builder.Services.AddSingleton<IVictoriaJournalStore>(sp => sp.GetRequiredService<SqliteMemoryStore>());

// Safety / spend layer (BED-080 libs wired by BED-082; TASK-102 hard gate on CapExceeded).
builder.Services.AddSingleton<CharterService>(_ => new CharterService(memoryOptions.ResolveDbPath()));
builder.Services.AddSingleton<ICharter>(sp => sp.GetRequiredService<CharterService>());
builder.Services.AddSingleton<DriftWatcher>(_ => new DriftWatcher(safetyOptions.DriftSloMinutes));
builder.Services.AddSingleton<SpendMeter>(_ => new SpendMeter(
    safetyOptions.InputTokenRatePer1K,
    safetyOptions.OutputTokenRatePer1K,
    safetyOptions.MonthlyCapUsd,
    safetyOptions.MonthlyTokenCap));

if (inferenceOptions.Enabled)
{
    builder.Services.AddHttpClient<OllamaInferenceClient>((sp, client) =>
    {
        var opts = sp.GetRequiredService<IOptions<InferenceOptions>>().Value;
        client.BaseAddress = NormalizeBaseUri(opts.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds));
    });
    // BED-126: expose the typed client as IInferenceClient. The 3-arg ctor
    // (http + options + logger) is what HttpClientFactory builds; the
    // tool-loop CompleteWithToolsAsync accepts an IToolRegistry at call time
    // (resolved from the container by the caller, e.g. ChatWebSocketHandler),
    // so the client itself does not need the registry injected to function.
    builder.Services.AddTransient<IInferenceClient>(sp => sp.GetRequiredService<OllamaInferenceClient>());
}
else
{
    builder.Services.AddSingleton<IInferenceClient, NullInferenceClient>();
}

// Tool registry (agent-loop foundation, BED-125). Additive — independent of
// inference/Hermes enablement. Concrete tools (BED-131+) register as ITool
// singletons elsewhere; ToolRegistry collects them via IEnumerable<ITool>.
// Empty registry is valid → Host boots clean with zero tools.
builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();

// BED-133: expose the memory count/stats surface (implemented additively by
// SqliteMemoryStore; does NOT extend IMemoryStore, so existing stubs stay green).
builder.Services.AddSingleton<IMemoryStats>(sp => sp.GetRequiredService<SqliteMemoryStore>());

// BED-133: system + filesystem tools. list_tools + system_info have no security
// gate (local, no secrets). Filesystem tools enforce ToolsOptions whitelist.
//
// ListToolsTool takes IServiceProvider (not IEnumerable<ITool>) and resolves
// the tool enumerable LAZILY inside ExecuteAsync. This breaks what would
// otherwise be a singleton-construction cycle: ToolRegistry is built from
// IEnumerable<ITool>, and ListToolsTool is one of those ITool instances —
// taking IEnumerable<ITool> in ListToolsTool's ctor would make building the
// registry build ListToolsTool, which needs the same enumerable being built.
// The lazy resolve defers past registry construction (by then the singleton is
// fully built), and the manifest correctly includes list_tools itself.
//
// Only the IServiceProvider ctor is public (tests use CreateForTests). Factory
// registration is belt-and-suspenders so MS.DI cannot pick a cycle-forming
// overload even if a second public ctor is reintroduced later.
builder.Services.AddSingleton<ITool>(sp => new ListToolsTool(sp));
builder.Services.AddSingleton<ITool, SystemInfoTool>();
builder.Services.AddSingleton<ITool, ReadFileTool>();
builder.Services.AddSingleton<ITool, WriteFileTool>();
builder.Services.AddSingleton<ITool, ListDirTool>();

// Memory tools (BED-131): recall_memory + store_memory wrap IMemoryStore so
// the model can decide to recall a specific memory or store a new one within
// a turn (in addition to the preamble-injected baseline recall). Registered as
// ITool singletons — ToolRegistry collects them via IEnumerable<ITool>.
builder.Services.AddSingleton<ITool, RecallMemoryTool>();
builder.Services.AddSingleton<ITool, StoreMemoryTool>();

// Body tools (BED-132): speak / play_animation / move_to / look_at / set_emotion
// wrap IUnrealVerbClient so the model can choose body actions mid-loop.
// Keyword detectors remain as Strategy A fallback (BED-128).
builder.Services.AddSingleton<ITool, SpeakTool>();
// Hub injected after IDesktopViewHub registration (factory resolves at first use).
builder.Services.AddSingleton<ITool>(sp => new VictoriaEyeCaptureTool(
    sp.GetRequiredService<IUnrealVerbClient>(),
    sp.GetRequiredService<IDesktopViewHub>()));
builder.Services.AddSingleton<ITool, PlayAnimationTool>();
builder.Services.AddSingleton<ITool, LocoTool>();
builder.Services.AddSingleton<ITool, MoveToTool>();
builder.Services.AddSingleton<ITool, LookAtTool>();
builder.Services.AddSingleton<ITool, SetEmotionTool>();

// Task tools (BED-140): task_create / task_get / task_update_status / task_list
// wrap IVictoriaTaskStore (SQLite victoria_tasks). Victoria's own work items —
// not the PM ticket folder. Workflow tools (BED-141) are separate.
builder.Services.AddSingleton<ITool, TaskCreateTool>();
builder.Services.AddSingleton<ITool, TaskGetTool>();
builder.Services.AddSingleton<ITool, TaskUpdateStatusTool>();
builder.Services.AddSingleton<ITool, TaskListTool>();

// Workflow tools (BED-141): workflow_create / workflow_execute / workflow_get.
// workflow_execute resolves IToolRegistry lazily via IServiceProvider (ListToolsTool pattern).
builder.Services.AddSingleton<ITool>(sp => new WorkflowExecuteTool(
    sp.GetRequiredService<IVictoriaWorkflowStore>(), sp));
builder.Services.AddSingleton<ITool, WorkflowCreateTool>();
builder.Services.AddSingleton<ITool, WorkflowGetTool>();

var embeddingsOn = inferenceOptions.Enabled && inferenceOptions.EmbeddingsEnabled;
if (embeddingsOn)
{
    builder.Services.AddHttpClient<OllamaEmbeddingClient>((sp, client) =>
    {
        var opts = sp.GetRequiredService<IOptions<InferenceOptions>>().Value;
        client.BaseAddress = NormalizeBaseUri(opts.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds));
    });
    builder.Services.AddTransient<IEmbeddingClient>(sp => sp.GetRequiredService<OllamaEmbeddingClient>());
}
else
{
    builder.Services.AddSingleton<IEmbeddingClient, NullEmbeddingClient>();
}

if (hermesOptions.Enabled)
{
    builder.Services.AddHttpClient<HermesHttpClient>((sp, client) =>
    {
        var opts = sp.GetRequiredService<IOptions<HermesOptions>>().Value;
        client.BaseAddress = NormalizeBaseUri(opts.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds));
    });
    builder.Services.AddTransient<IHermesClient>(sp => sp.GetRequiredService<HermesHttpClient>());
}
else
{
    builder.Services.AddSingleton<IHermesClient, NullHermesClient>();
}

// BED-158: in-memory per-sessionId chat/tool history for multi-turn pronouns.
builder.Services.AddSingleton<IChatSessionHistoryStore>(sp =>
{
    var max = sp.GetRequiredService<IOptions<ChatWsOptions>>().Value.MaxSessionHistoryMessages;
    if (max < 2) max = 40;
    return new ChatSessionHistoryStore(max);
});

// Desktop tools (BED-135): capture + click/type/key with session gates.
// AllowDesktopCapture / AllowBrowserCapture / AllowComputerControl default true (TASK-177).
// Backend: Tools:DesktopBackend = "cua" | "native" | "hermes".
// cua = local cua-driver agent cursor (LLMOD blue overlay; OS mouse untouched).
// Session gates are mutable via GET/POST /settings/tools (Settings → Tools & Access).
builder.Services.AddSingleton<ComputerControlGate>();
builder.Services.AddSingleton<IComputerControlGate>(sp => sp.GetRequiredService<ComputerControlGate>());
builder.Services.AddSingleton<IToolsAccessSettings>(sp => sp.GetRequiredService<ComputerControlGate>());
builder.Services.AddSingleton<IDesktopViewHub>(sp =>
    new DesktopViewHub(() => sp.GetRequiredService<IToolsAccessSettings>().SoftCursorRestore));
var desktopBackend = (toolsOptions.DesktopBackend ?? "cua").Trim();
if (string.Equals(desktopBackend, "hermes", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IDesktopControlBackend>(sp =>
        new HermesDesktopControlBackend(
            sp.GetRequiredService<IHermesClient>(),
            sp.GetRequiredService<IDesktopViewHub>()));
}
else if (string.Equals(desktopBackend, "cua", StringComparison.OrdinalIgnoreCase)
         || string.Equals(desktopBackend, "auto", StringComparison.OrdinalIgnoreCase))
{
    var cuaExe = CuaDriverCli.TryFindExe();
    if (cuaExe is not null)
    {
        builder.Services.AddSingleton(_ => new CuaDriverCli(cuaExe));
        builder.Services.AddSingleton<IDesktopControlBackend>(sp =>
            new CuaDriverDesktopBackend(
                sp.GetRequiredService<CuaDriverCli>(),
                sp.GetRequiredService<IDesktopViewHub>(),
                sp.GetRequiredService<IToolsAccessSettings>()));
    }
    else
    {
        builder.Services.AddSingleton<IDesktopControlBackend>(sp =>
            new NativeDesktopControlBackend(
                sp.GetRequiredService<IDesktopViewHub>(),
                sp.GetRequiredService<IToolsAccessSettings>()));
    }
}
else
{
    builder.Services.AddSingleton<IDesktopControlBackend>(sp =>
        new NativeDesktopControlBackend(
            sp.GetRequiredService<IDesktopViewHub>(),
            sp.GetRequiredService<IToolsAccessSettings>()));
}
builder.Services.AddSingleton<ITool>(sp => new DesktopScreenshotTool(
    sp.GetRequiredService<IComputerControlGate>(),
    sp.GetRequiredService<IDesktopControlBackend>(),
    sp.GetRequiredService<IDesktopViewHub>()));
builder.Services.AddSingleton<ITool, DesktopClickTool>();
builder.Services.AddSingleton<ITool, DesktopDragTool>();
builder.Services.AddSingleton<ITool, DesktopTypeTool>();
builder.Services.AddSingleton<ITool, DesktopKeyTool>();
builder.Services.AddSingleton<ITool, DesktopScrollTool>();
builder.Services.AddSingleton<ITool, DesktopOpenAppTool>();
builder.Services.AddSingleton<ITool, ListDesktopWindowsTool>();
builder.Services.AddSingleton<ITool, FocusDesktopWindowTool>();

// Chief Architect X17 playbook tools (plan → recipe → desktop_* execution).
builder.Services.AddSingleton<SoulCore.Inference.Tools.ChiefArchitect.CaPlaybookLibrary>();
builder.Services.AddSingleton<SoulCore.Inference.Tools.ChiefArchitect.CaSessionState>();
builder.Services.AddSingleton<ITool, SoulCore.Inference.Tools.ChiefArchitect.CaCompileBriefTool>();
builder.Services.AddSingleton<ITool, SoulCore.Inference.Tools.ChiefArchitect.CaPlanProjectTool>();
builder.Services.AddSingleton<ITool, SoulCore.Inference.Tools.ChiefArchitect.CaGetRecipeTool>();
builder.Services.AddSingleton<ITool, SoulCore.Inference.Tools.ChiefArchitect.CaNextStepTool>();
builder.Services.AddSingleton<ITool, SoulCore.Inference.Tools.ChiefArchitect.CaWorldHintTool>();
builder.Services.AddSingleton<ITool, SoulCore.Inference.Tools.ChiefArchitect.CaVerifyChecklistTool>();

// Browser tools (BED-136): browser_health / capture_tab / click / type / key / scroll.
// Read: Tools.AllowBrowserCapture (default true). Write: Tools.AllowComputerControl.
// Backend: Tools.BrowserBackend=hermes → HermesBrowserBridge (BED-144 CallMcpToolAsync).
builder.Services.AddSingleton<IBrowserBridge>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<ToolsOptions>>().Value;
    var backend = (opts.BrowserBackend ?? ToolsOptions.BackendHermes).Trim();
    if (!string.Equals(backend, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase))
        return new UnsupportedBrowserBridge(backend);

    return new HermesBrowserBridge(sp.GetRequiredService<IHermesClient>());
});
builder.Services.AddSingleton<ITool, BrowserHealthTool>();
builder.Services.AddSingleton<ITool>(sp => new BrowserCaptureTabTool(
    sp.GetRequiredService<IBrowserBridge>(),
    sp.GetRequiredService<IToolsAccessSettings>(),
    sp.GetRequiredService<IDesktopViewHub>()));
builder.Services.AddSingleton<ITool, BrowserClickTool>();
builder.Services.AddSingleton<ITool, BrowserTypeTool>();
builder.Services.AddSingleton<ITool, BrowserKeyTool>();
builder.Services.AddSingleton<ITool, BrowserScrollTool>();

// MT4 trading tools (BED-138): AllowMt4Read / AllowMt4Trade + confirmed=true gate.
// Mt4Backend=llmod → LlmodHttpMt4Bridge (BED-169, shadow housevictoria:8080).
// Mt4Backend=hermes → HermesMt4Bridge via CallMcpToolAsync (BED-144).
builder.Services.AddHttpClient<LlmodHttpMt4Bridge>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<IMt4Bridge>(sp =>
{
    var tools = sp.GetRequiredService<IOptions<ToolsOptions>>().Value;
    var backend = (tools.Mt4Backend ?? ToolsOptions.BackendLlmod).Trim();

    if (HermesToolRouting.IsHermesBackend(backend))
        return new HermesMt4Bridge(sp.GetRequiredService<IHermesClient>());

    if (HermesToolRouting.IsLlmodBackend(backend))
        return sp.GetRequiredService<LlmodHttpMt4Bridge>();

    return new UnavailableMt4Bridge(
        $"mt4 backend '{backend}' not supported — use '{ToolsOptions.BackendLlmod}', '{ToolsOptions.BackendNative}', or '{ToolsOptions.BackendHermes}'");
});
builder.Services.AddSingleton<ITool, Mt4StatusTool>();
builder.Services.AddSingleton<ITool, ListSymbolsTool>();
builder.Services.AddSingleton<ITool, GetMarketDataTool>();
builder.Services.AddSingleton<ITool, GetOpenPositionsTool>();
builder.Services.AddSingleton<ITool, ExecuteTradeTool>();
builder.Services.AddSingleton<ITool, ClosePositionTool>();
builder.Services.AddSingleton<ITool, VerifyTicketTool>();
builder.Services.AddSingleton<ITool, MarketWatchStatusTool>();
builder.Services.AddSingleton<ITool, ExportHistoryTool>();
builder.Services.AddSingleton<ITool, GetHistoricalBarsTool>();
builder.Services.AddSingleton<ITool, RunBacktestTool>();

builder.Services.AddSingleton<PresenceWsHub>();
builder.Services.AddSingleton<IWsFrameAdapter>(sp => sp.GetRequiredService<PresenceWsHub>());
builder.Services.AddSingleton<ICompanionOutboundMessenger, CompanionOutboundMessenger>();
builder.Services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(5) });
builder.Services.AddSingleton<ComfyUiClient>();
builder.Services.AddSingleton<ICompanionMediaService, CompanionMediaService>();
builder.Services.AddSingleton<SoulLoopScaffold>();
builder.Services.AddSingleton<ISoulLoop>(sp => sp.GetRequiredService<SoulLoopScaffold>());
builder.Services.AddHostedService<SoulLoopHostedService>();
builder.Services.AddSingleton<ChatWebSocketHandler>();

if (unrealOptions.Enabled)
{
    builder.Services.AddSingleton<UnrealVerbClientStub>();
    builder.Services.AddSingleton<IUnrealVerbClient>(sp => sp.GetRequiredService<UnrealVerbClientStub>());
}
else
{
    builder.Services.AddSingleton<IUnrealVerbClient, NullUnrealVerbClient>();
}

// Voice: local Whisper STT + Chatterbox TTS (House.Voice satellites).
builder.Services.AddHttpClient("voice-stt", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<VoiceOptions>>().Value;
    client.BaseAddress = new Uri(opts.SttUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds));
});
builder.Services.AddHttpClient("voice-tts", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<VoiceOptions>>().Value;
    client.BaseAddress = new Uri(opts.TtsUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds));
});
builder.Services.AddSingleton<ISttClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new WhisperSttClient(
        factory.CreateClient("voice-stt"),
        sp.GetRequiredService<IOptions<VoiceOptions>>(),
        sp.GetRequiredService<ILogger<WhisperSttClient>>());
});
builder.Services.AddSingleton<ITtsClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new ChatterboxTtsClient(
        factory.CreateClient("voice-tts"),
        sp.GetRequiredService<IOptions<VoiceOptions>>(),
        sp.GetRequiredService<ILogger<ChatterboxTtsClient>>());
});
if (voiceOptions.Enabled)
{
    builder.Services.AddSingleton<IVoiceSpeakService, VoiceSpeakService>();
}
else
{
    builder.Services.AddSingleton<IVoiceSpeakService, PassthroughVoiceSpeakService>();
}

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

var wsPath = string.IsNullOrWhiteSpace(chatWsOptions.Path) ? "/ws" : chatWsOptions.Path;
if (!wsPath.StartsWith('/'))
    wsPath = "/" + wsPath;

app.Map(wsPath, async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Expected WebSocket upgrade. Use ws://127.0.0.1:7700/ws");
        return;
    }

    // BED-155 / SEC-152: fail-closed companion token when SOULCORE_COMPANION_API_TOKEN is set.
    // Accept Authorization: Bearer <token> or X-Api-Key: <token>. Never log secret values.
    var companionToken = CompanionWsAuth.ResolveConfiguredToken(context.RequestServices.GetService<IConfiguration>());
    var authOutcome = CompanionWsAuth.Evaluate(context.Request, companionToken);
    if (authOutcome is CompanionWsAuth.AuthOutcome.Missing or CompanionWsAuth.AuthOutcome.Invalid)
    {
        var wsAuthLogger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SoulCore.Host.Ws.CompanionAuth");
        var headerSource = CompanionWsAuth.DescribeHeaderSource(context.Request);
        wsAuthLogger.LogWarning(
            "WS upgrade rejected: companion auth failed ({Safe})",
            CompanionWsAuth.FormatLogSafe(authOutcome, headerSource));
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var handler = context.RequestServices.GetRequiredService<ChatWebSocketHandler>();
    await handler.RunAsync(socket, context.RequestAborted);
});

app.MapCompanionApi();
app.MapVoiceApi();

app.MapGet("/health", async (
    IOptions<HostBindOptions> opts,
    IMemoryStore memory,
    IUnrealVerbClient unreal,
    IOptions<UnrealBridgeOptions> unrealOpts,
    IOptions<ChatWsOptions> chatOpts,
    IOptions<SoulLoopOptions> loopOpts,
    IToolsAccessSettings access,
    DriftWatcher driftWatcher,
    SpendMeter spendMeter,
    CharterService charter,
    CancellationToken cancellationToken) =>
{
    var memoryOk = memory.IsDatabaseOpen;

    DriftStatus driftStatus;
    try
    {
        driftStatus = driftWatcher.GetStatus();
    }
    catch (Exception)
    {
        driftStatus = new DriftStatus(null, 0, false, null);
    }

    var oldestDriftMinutes = driftStatus.OldestDriftReport is null
        ? 0
        : Math.Max(0, (int)Math.Round((DateTimeOffset.UtcNow - driftStatus.OldestDriftReport.ObservedAt).TotalMinutes));

    SpendSummary spendSummary;
    try
    {
        spendSummary = spendMeter.GetSummary();
    }
    catch (Exception)
    {
        spendSummary = new SpendSummary(0, 0, 0m, 0m, false);
    }

    int charterTotal = 0, charterLocked = 0;
    try
    {
        (charterTotal, charterLocked) = await charter.GetLockCountsAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (Exception)
    {
        // health stays up even if charter query fails
    }

    var charterFullyLocked = charterTotal > 0 && charterLocked == charterTotal;

    return Results.Json(new
    {
        status = memoryOk ? "ok" : "degraded",
        service = "SoulCore.Host",
        bind = opts.Value.BindAddress,
        port = opts.Value.Port,
        phase = 1,
        ws = new
        {
            path = chatOpts.Value.Path,
            url = $"ws://{opts.Value.BindAddress}:{opts.Value.Port}{NormalizePath(chatOpts.Value.Path)}"
        },
        memory = new
        {
            open = memoryOk,
            path = memory.DatabasePath
        },
        inference = new
        {
            enabled = inferenceOptions.Enabled,
            provider = inferenceOptions.Enabled ? "ollama" : "null",
            // BED-01 / TASK-157: expose configured chat model for QA/ops (no secrets).
            model = inferenceOptions.Model,
            embeddingsEnabled = embeddingsOn,
            embeddingModel = inferenceOptions.EmbeddingModel
        },
        hermes = new
        {
            enabled = hermesOptions.Enabled,
            provider = hermesOptions.Enabled ? "http" : "null"
        },
        soulLoop = new
        {
            enabled = loopOpts.Value.Enabled,
            tickIntervalSeconds = loopOpts.Value.TickIntervalSeconds
        },
        unreal = new
        {
            enabled = unrealOpts.Value.Enabled,
            target = unreal.TargetUrl,
            connected = unreal.IsConnected
        },
        tools = ToolsSettingsDto(access),
        charter = new
        {
            anchors = charterTotal,
            locked = charterLocked,
            fullyLocked = charterFullyLocked,
            mode = charterFullyLocked ? "locked" : (charterTotal == 0 ? "empty" : "calibration")
        },
        safety = new
        {
            drift = new
            {
                activeDriftCount = driftStatus.UnackedReports,
                sloExceeded = driftStatus.SloExceeded,
                oldestDriftMinutes
            },
            spend = new
            {
                totalTokensIn = spendSummary.TotalTokensIn,
                totalTokensOut = spendSummary.TotalTokensOut,
                estimatedCostUsd = spendSummary.EstimatedCost,
                monthlyCapUsd = spendSummary.MonthlyCap,
                capExceeded = spendSummary.CapExceeded
            }
        }
    });
});

app.MapPost("/health/drift/ack", (DriftWatcher driftWatcher) =>
{
    var acked = driftWatcher.AcknowledgeAll();
    return Results.Json(new { acked });
});

static object ToolsSettingsDto(IToolsAccessSettings access)
{
    var cuaPath = CuaDriverCli.TryFindExe();
    return new
    {
        allowDesktopCapture = access.AllowDesktopCapture,
        allowBrowserCapture = access.AllowBrowserCapture,
        allowComputerControl = access.AllowComputerControl,
        softCursorRestore = access.SoftCursorRestore,
        allowMt4Read = access.AllowMt4Read,
        allowMt4Trade = access.AllowMt4Trade,
        desktopBackend = access.DesktopBackend,
        browserBackend = access.BrowserBackend,
        mt4Backend = access.Mt4Backend,
        cuaDriverAvailable = cuaPath is not null,
        cuaDriverPath = cuaPath,
        scope = "session",
        note = "Session gates until Host restart. Seeded from Tools in appsettings.json (desktop/browser capture + computer control default on). SoftCursorRestore + DesktopBackend=cua = LLMOD-style agent cursor (blue overlay; your mouse stays put)."
    };
}

app.MapGet("/settings/tools", (IToolsAccessSettings access) => Results.Json(ToolsSettingsDto(access)));

app.MapPost("/settings/tools", async (HttpRequest request, IToolsAccessSettings access) =>
{
    using var doc = await JsonDocument.ParseAsync(request.Body).ConfigureAwait(false);
    var root = doc.RootElement;
    if (root.ValueKind != JsonValueKind.Object)
        return Results.BadRequest(new { error = "expected JSON object" });

    static bool? ReadBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    if (ReadBool(root, "allowDesktopCapture") is { } deskCap)
        access.SetAllowDesktopCapture(deskCap);
    if (ReadBool(root, "allowBrowserCapture") is { } browserCap)
        access.SetAllowBrowserCapture(browserCap);
    if (ReadBool(root, "allowComputerControl") is { } control)
        access.SetAllowComputerControl(control);
    if (ReadBool(root, "softCursorRestore") is { } soft)
        access.SetSoftCursorRestore(soft);
    if (ReadBool(root, "allowMt4Read") is { } mt4Read)
        access.SetAllowMt4Read(mt4Read);
    if (ReadBool(root, "allowMt4Trade") is { } mt4Trade)
        access.SetAllowMt4Trade(mt4Trade);

    return Results.Json(ToolsSettingsDto(access));
});

// TASK-177: Identity tab payload — Companion display name + charter anchor details
// (read-only from CharterService; no fabricated biography).
app.MapGet("/settings/identity", async (
    IOptions<CompanionOptions> companionOpts,
    CharterService charter,
    CancellationToken cancellationToken) =>
{
    var companion = companionOpts.Value ?? new CompanionOptions();
    int charterTotal = 0, charterLocked = 0;
    IReadOnlyList<CharterAnchorInfo> identityAnchors = Array.Empty<CharterAnchorInfo>();
    IReadOnlyList<CharterAnchorInfo> allAnchors = Array.Empty<CharterAnchorInfo>();
    try
    {
        (charterTotal, charterLocked) = await charter.GetLockCountsAsync(cancellationToken).ConfigureAwait(false);
        identityAnchors = await charter.ListAnchorDetailsAsync("identity", cancellationToken).ConfigureAwait(false);
        allAnchors = await charter.ListAnchorDetailsAsync(kind: null, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception)
    {
        // Return name + empty anchors rather than failing the Settings tab.
    }

    var fullyLocked = charterTotal > 0 && charterLocked == charterTotal;
    static object AnchorDto(CharterAnchorInfo a) => new
    {
        id = a.Id,
        kind = a.Kind,
        title = a.Title,
        body = a.Body,
        priority = a.Priority,
        isLocked = a.IsLocked,
        source = a.Source
    };

    return Results.Json(new
    {
        displayName = companion.DefaultContactName,
        contactId = companion.DefaultContactId,
        charter = new
        {
            anchors = charterTotal,
            locked = charterLocked,
            fullyLocked,
            mode = fullyLocked ? "locked" : (charterTotal == 0 ? "empty" : "calibration")
        },
        identityAnchors = identityAnchors.Select(AnchorDto).ToArray(),
        anchors = allAnchors.Select(AnchorDto).ToArray(),
        note = "Read-only charter/identity anchors from SoulCore SQLite. Display name from Companion options (Victoria)."
    });
});

app.MapGet("/desktop/view", (IDesktopViewHub view) =>
{
    var snap = view.GetSnapshot();
    return Results.Json(new
    {
        hasImage = snap.HasImage,
        imagePath = "/desktop/view/image",
        format = snap.Format,
        width = snap.Width,
        height = snap.Height,
        cursorX = snap.CursorX,
        cursorY = snap.CursorY,
        lastAction = snap.LastAction,
        updatedAt = snap.UpdatedAt,
        softCursorRestore = snap.SoftCursorRestore,
        source = snap.Source,
        note = "Last image Victoria actually captured (source=desktop|eyes|browser). Cursor overlay applies to desktop captures. Stale frames mean she has not captured again yet."
    });
});

app.MapGet("/desktop/view/image", (IDesktopViewHub view) =>
{
    var bytes = view.TryGetImageBytes();
    if (bytes is null || bytes.Length == 0)
        return Results.NotFound();

    var snap = view.GetSnapshot();
    var contentType = string.Equals(snap.Format, "png", StringComparison.OrdinalIgnoreCase)
        ? "image/png"
        : "image/bmp";
    return Results.File(bytes, contentType);
});

app.MapGet("/", () => Results.Redirect("/health"));

var logger = app.Logger;
logger.LogInformation(
    "SoulCore.Host listening on http://{Address}:{Port} (health: /health, ws: {WsPath}); memory={MemoryPath}; inference={Inference}; hermes={Hermes}; soulLoop={SoulLoop}; unreal={Unreal}",
    bindOptions.BindAddress,
    bindOptions.Port,
    wsPath,
    app.Services.GetRequiredService<IMemoryStore>().DatabasePath,
    inferenceOptions.Enabled ? "ollama" : "null",
    hermesOptions.Enabled ? "http" : "null",
    soulLoopOptions.Enabled ? "enabled" : "disabled",
    unrealOptions.Enabled ? unrealOptions.WsUrl : "disabled");

await app.Services.GetRequiredService<IWsFrameAdapter>()
    .StartAsync()
    .ConfigureAwait(false);

// Optional Unreal connect — must not crash Host if :8888 is down.
if (unrealOptions.Enabled && unrealOptions.ConnectOnStartup)
{
    try
    {
        await app.Services.GetRequiredService<IUnrealVerbClient>()
            .EnsureConnectedAsync()
            .ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Unreal startup connect failed (ignored)");
    }
}

await app.RunAsync();
return 0;

static int ReportSecretsPresence()
{
    var keys = new[]
    {
        SecretNames.A2eApiToken,
        SecretNames.HermesApiKey,
        SecretNames.HuggingFaceToken,
        SecretNames.CompanionApiToken
    };

    var envPath = DotEnvLoader.ResolveEnvFilePath();
    Console.WriteLine($"env_file_found={!string.IsNullOrEmpty(envPath)}");

    var allPresent = true;
    foreach (var key in keys)
    {
        var value = Environment.GetEnvironmentVariable(key);
        var present = !string.IsNullOrEmpty(value);
        var length = present ? value!.Length : 0;
        allPresent &= present;
        Console.WriteLine($"{key}: present={present} length={length}");
    }

    Console.WriteLine($"all_present={allPresent}");
    return allPresent ? 0 : 1;
}

static bool IsLoopback(string address) =>
    string.Equals(address, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
    || string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase)
    || string.Equals(address, "::1", StringComparison.OrdinalIgnoreCase);

static Uri NormalizeBaseUri(string baseUrl)
{
    var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/') + "/";
    return new Uri(trimmed, UriKind.Absolute);
}

static string NormalizePath(string path)
{
    if (string.IsNullOrWhiteSpace(path))
        return "/ws";
    return path.StartsWith('/') ? path : "/" + path;
}

// Expose for integration tests later
public partial class Program;
