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
using SoulCore.Host.Inference;
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
using SoulCore.Inference.Tools.Email;
using SoulCore.Inference.Tools.Trading;
using SoulCore.Inference.Tools.Workflow;
using SoulCore.Memory;
using SoulCore.Memory.Repositories;
using System.Text.Json;

// Local SoulCore/.env → process env (SOULCORE_* only) before any config bind.
// .env overwrites stale Process/User-inherited tokens; never log secret values.
DotEnvLoader.TryLoad();

// Evidence mode: confirm SecretNames keys present (length/bool only — no values).
if (args.Any(a => string.Equals(a, "--secrets-presence", StringComparison.OrdinalIgnoreCase)))
{
    return ReportSecretsPresence();
}

// Evidence mode: VirtualBox guestcontrol logon probe (SOULCORE_VBOX_GUEST_*).
if (args.Any(a => string.Equals(a, "--guestcontrol-probe", StringComparison.OrdinalIgnoreCase)))
{
    return await GuestControlProbe.RunAsync(args);
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
builder.Services.Configure<SmsOptions>(
    builder.Configuration.GetSection(SmsOptions.SectionName));
builder.Services.Configure<SafetyOptions>(
    builder.Configuration.GetSection(SafetyOptions.SectionName));
builder.Services.Configure<ToolsOptions>(
    builder.Configuration.GetSection(ToolsOptions.SectionName));
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));

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

// BED-185: Hermes retired — hard-disable regardless of appsettings / SOULCORE_* env.
// Open Chrome + URLs via desktop_open_app (Ollama tool-loop), never Hermes gateway.
if (hermesOptions.Enabled || chatWsOptions.PreferHermes
    || string.Equals(toolsOptions.BrowserBackend, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase)
    || string.Equals(toolsOptions.DesktopBackend, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase)
    || string.Equals(toolsOptions.Mt4Backend, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine(
        "[SoulCore] BED-185: Hermes retired — forcing Hermes.Enabled=false PreferHermes=false; "
        + "hermes tool backends remapped (desktop=cua, browser=none, mt4=llmod).");
}

hermesOptions.Enabled = false;
chatWsOptions.PreferHermes = false;
if (string.Equals(toolsOptions.BrowserBackend, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase)
    || string.IsNullOrWhiteSpace(toolsOptions.BrowserBackend))
    toolsOptions.BrowserBackend = "none";
if (string.Equals(toolsOptions.DesktopBackend, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase))
    toolsOptions.DesktopBackend = ToolsOptions.BackendCua;
if (string.Equals(toolsOptions.Mt4Backend, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase))
    toolsOptions.Mt4Backend = ToolsOptions.BackendLlmod;

builder.Services.PostConfigure<HermesOptions>(o => o.Enabled = false);
builder.Services.PostConfigure<ChatWsOptions>(o => o.PreferHermes = false);
builder.Services.PostConfigure<ToolsOptions>(o =>
{
    if (string.Equals(o.BrowserBackend, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(o.BrowserBackend))
        o.BrowserBackend = "none";
    if (string.Equals(o.DesktopBackend, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase))
        o.DesktopBackend = ToolsOptions.BackendCua;
    if (string.Equals(o.Mt4Backend, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase))
        o.Mt4Backend = ToolsOptions.BackendLlmod;
});

// SEC-004: V1 bind = 127.0.0.1 only. Refuse non-loopback without explicit future SEC gate.
if (!IsLoopback(bindOptions.BindAddress))
{
    throw new InvalidOperationException(
        $"SoulCore V1 refuses non-loopback bind '{bindOptions.BindAddress}'. " +
        "Use 127.0.0.1 only (SEC-004).");
}

builder.WebHost.UseUrls($"http://{bindOptions.BindAddress}:{bindOptions.Port}");

// PROP-11.1: one SQLite session + focused repos behind existing interfaces.
builder.Services.AddSingleton<SqliteMemorySession>();
builder.Services.AddSingleton<SqliteEpisodicMemoryRepository>();
builder.Services.AddSingleton<IMemoryStore>(sp => sp.GetRequiredService<SqliteEpisodicMemoryRepository>());
builder.Services.AddSingleton<SqliteEmotionRepository>();
builder.Services.AddSingleton<IEmotionState>(sp => sp.GetRequiredService<SqliteEmotionRepository>());
builder.Services.AddSingleton<SqliteVictoriaTaskRepository>();
builder.Services.AddSingleton<IVictoriaTaskStore>(sp => sp.GetRequiredService<SqliteVictoriaTaskRepository>());
builder.Services.AddSingleton<SqliteVictoriaWorkflowRepository>();
builder.Services.AddSingleton<IVictoriaWorkflowStore>(sp => sp.GetRequiredService<SqliteVictoriaWorkflowRepository>());
builder.Services.AddSingleton<SqliteVictoriaJournalRepository>();
builder.Services.AddSingleton<IVictoriaJournalStore>(sp => sp.GetRequiredService<SqliteVictoriaJournalRepository>());
builder.Services.AddSingleton<SqliteMemoryStore>(sp => new SqliteMemoryStore(sp.GetRequiredService<SqliteMemorySession>()));

// Safety / spend layer (BED-080 libs wired by BED-082; TASK-102 hard gate on CapExceeded).
// PROP-5.3: Charter shares the memory DB file; both serialize via SqlitePathGate on ResolveDbPath().
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
    if (inferenceOptions.IsCloudEndpoint && string.IsNullOrWhiteSpace(inferenceOptions.ResolveApiKey()))
    {
        Console.WriteLine(
            "[SoulCore] BED-187: Inference BaseUrl is Ollama Cloud but SOULCORE_OLLAMA_API_KEY is missing — chat will 401 until set.");
    }

    builder.Services.AddHttpClient<OllamaInferenceClient>((sp, client) =>
    {
        var opts = sp.GetRequiredService<IOptions<InferenceOptions>>().Value;
        ConfigureOllamaHttpClient(client, opts.BaseUrl, opts.TimeoutSeconds, InferenceOptions.IsOllamaCloudUrl(opts.BaseUrl) ? opts.ResolveApiKey() : null);
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
builder.Services.AddSingleton<IMemoryStats>(sp => sp.GetRequiredService<SqliteEpisodicMemoryRepository>());

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
        // BED-187: embeddings stay local by default when chat is on Ollama Cloud.
        var embedBase = opts.ResolveEmbeddingBaseUrl();
        var embedKey = InferenceOptions.IsOllamaCloudUrl(embedBase) ? opts.ResolveApiKey() : null;
        ConfigureOllamaHttpClient(client, embedBase, opts.TimeoutSeconds, embedKey);
    });
    builder.Services.AddTransient<IEmbeddingClient>(sp => sp.GetRequiredService<OllamaEmbeddingClient>());
}
else
{
    builder.Services.AddSingleton<IEmbeddingClient, NullEmbeddingClient>();
}

// BED-185: never wire HermesHttpClient — NullHermesClient only.
builder.Services.AddSingleton<IHermesClient, NullHermesClient>();

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
// Optional Tools:DesktopTargetWindowTitle hard-scopes clicks to that window (BED-188).
// Session gates are mutable via GET/POST /settings/tools (Settings → Tools & Access).
builder.Services.AddSingleton<ComputerControlGate>();
builder.Services.AddSingleton<IComputerControlGate>(sp => sp.GetRequiredService<ComputerControlGate>());
builder.Services.AddSingleton<IToolsAccessSettings>(sp => sp.GetRequiredService<ComputerControlGate>());
builder.Services.AddSingleton<IDesktopViewHub>(sp =>
    new DesktopViewHub(() => sp.GetRequiredService<IToolsAccessSettings>().SoftCursorRestore));
// PROP-4: honest Presence activity (doing-now), not SoulLoop want slogans.
builder.Services.AddSingleton<SoulCore.Inference.Presence.IPresenceActivityHub>(sp =>
    new SoulCore.Inference.Presence.PresenceActivityHub(sp.GetRequiredService<IDesktopViewHub>()));
builder.Services.AddSingleton<GuestVmBrowserBridgeHolder>();
builder.Services.AddSingleton<IVictoriaBrowserViewHub, VictoriaBrowserViewHub>();
builder.Services.AddSingleton<IDesktopControlBackend>(sp =>
{
    IDesktopControlBackend inner;
    var backendName = (sp.GetRequiredService<IToolsAccessSettings>().DesktopBackend ?? "cua").Trim();
    if (string.Equals(backendName, "hermes", StringComparison.OrdinalIgnoreCase))
        backendName = "cua";
    if (string.Equals(backendName, "cua", StringComparison.OrdinalIgnoreCase)
        || string.Equals(backendName, "auto", StringComparison.OrdinalIgnoreCase))
    {
        var cuaExe = CuaDriverCli.TryFindExe();
        if (cuaExe is not null)
        {
            inner = new CuaDriverDesktopBackend(
                new CuaDriverCli(cuaExe),
                sp.GetRequiredService<IDesktopViewHub>(),
                sp.GetRequiredService<IToolsAccessSettings>());
        }
        else
        {
            inner = new NativeDesktopControlBackend(
                sp.GetRequiredService<IDesktopViewHub>(),
                sp.GetRequiredService<IToolsAccessSettings>());
        }
    }
    else
    {
        inner = new NativeDesktopControlBackend(
            sp.GetRequiredService<IDesktopViewHub>(),
            sp.GetRequiredService<IToolsAccessSettings>());
    }

    var scopeTitle = sp.GetRequiredService<IToolsAccessSettings>().DesktopTargetWindowTitle;
    if (string.IsNullOrWhiteSpace(scopeTitle))
        return inner;
    var guest = new VirtualBoxGuestAppLauncher(scopeTitle);
    sp.GetRequiredService<GuestVmBrowserBridgeHolder>().Set(guest, guest);
    return new ScopedDesktopControlBackend(
        inner,
        scopeTitle,
        guest,
        new NativeDesktopControlBackend());
});
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

// Browser tools (BED-136 / BED-182 / BED-195): browser_health / capture / click / type / key / scroll.
// Read: Tools.AllowBrowserCapture (default true). Write: Tools.AllowComputerControl.
// Backend: Tools.BrowserBackend=playwright (BED-195 Victoria Chromium) preferred even when
// DesktopTargetWindowTitle is set (VM stays for desktop_*; web uses Playwright).
// native → BrowserCaptureBridge :17891. Hermes browser backend retired (BED-185).
builder.Services.AddHttpClient("browser-bridge", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<ToolsOptions>>().Value;
    var configured = (opts.BrowserBridgeUrl ?? "").Trim();
    var baseUrl = string.IsNullOrWhiteSpace(configured)
        ? NativeBrowserBridge.DefaultBaseUrl
        : configured.TrimEnd('/');

    if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || !uri.IsLoopback)
        uri = new Uri(NativeBrowserBridge.DefaultBaseUrl + "/");
    else
        uri = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");

    client.BaseAddress = uri;
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddSingleton<IBrowserBridge>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<ToolsOptions>>().Value;
    var backend = (opts.BrowserBackend ?? ToolsOptions.BackendNative).Trim();
    if (string.Equals(backend, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase))
        backend = "none";

    // BED-195: Playwright wins over GuestVm even when DesktopTargetWindowTitle is set.
    if (string.Equals(backend, ToolsOptions.BackendPlaywright, StringComparison.OrdinalIgnoreCase))
    {
        return new PlaywrightBrowserBridge(
            sp.GetRequiredService<IOptions<ToolsOptions>>(),
            sp.GetService<ILogger<PlaywrightBrowserBridge>>(),
            sp.GetRequiredService<IVictoriaBrowserViewHub>());
    }

    var scopeTitle = (opts.DesktopTargetWindowTitle ?? "").Trim();
    if (!string.IsNullOrWhiteSpace(scopeTitle))
    {
        var holder = sp.GetRequiredService<GuestVmBrowserBridgeHolder>();
        if (holder.TryGet(out var bridge))
            return bridge;
    }

    if (string.Equals(backend, ToolsOptions.BackendNative, StringComparison.OrdinalIgnoreCase)
        || string.Equals(backend, "llmod", StringComparison.OrdinalIgnoreCase)
        || string.Equals(backend, "auto", StringComparison.OrdinalIgnoreCase))
    {
        var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("browser-bridge");
        return new NativeBrowserBridge(
            http,
            sp.GetRequiredService<IOptions<ToolsOptions>>(),
            sp.GetService<ILogger<NativeBrowserBridge>>());
    }
    return new UnsupportedBrowserBridge(backend);
});
builder.Services.AddSingleton<ITool, BrowserHealthTool>();
builder.Services.AddSingleton<ITool>(sp => new BrowserCaptureTabTool(
    sp.GetRequiredService<IBrowserBridge>(),
    sp.GetRequiredService<IToolsAccessSettings>(),
    sp.GetRequiredService<IDesktopViewHub>()));
builder.Services.AddSingleton<ITool, BrowserNavigateTool>();
builder.Services.AddSingleton<ITool, BrowserSnapshotTool>();
builder.Services.AddSingleton<ITool, BrowserClickTextTool>();
builder.Services.AddSingleton<ITool, BrowserFillTool>();
builder.Services.AddSingleton<ITool, BrowserBackTool>();
builder.Services.AddSingleton<ITool, BrowserTabsTool>();
builder.Services.AddSingleton<ITool, BrowserClickTool>();
builder.Services.AddSingleton<ITool, BrowserTypeTool>();
builder.Services.AddSingleton<ITool, BrowserKeyTool>();
builder.Services.AddSingleton<ITool, BrowserScrollTool>();

// MT4 trading tools (BED-138): AllowMt4Read / AllowMt4Trade + confirmed=true gate.
// Mt4Backend=llmod → LlmodHttpMt4Bridge (BED-169). Hermes MT4 bridge retired (BED-185).
builder.Services.AddHttpClient<LlmodHttpMt4Bridge>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<IMt4Bridge>(sp =>
{
    var tools = sp.GetRequiredService<IOptions<ToolsOptions>>().Value;
    var backend = (tools.Mt4Backend ?? ToolsOptions.BackendLlmod).Trim();
    if (HermesToolRouting.IsHermesBackend(backend))
        backend = ToolsOptions.BackendLlmod;

    if (HermesToolRouting.IsLlmodBackend(backend))
        return sp.GetRequiredService<LlmodHttpMt4Bridge>();

    return new UnavailableMt4Bridge(
        $"mt4 backend '{backend}' not supported — use '{ToolsOptions.BackendLlmod}' or '{ToolsOptions.BackendNative}'");
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

// Email tools — IMAP/SMTP multi-account (victoria / personal / business).
// AllowEmailRead / AllowEmailSend / AllowEmailDelete + confirmed=true on send/delete.
builder.Services.AddSingleton<SoulCore.Inference.Tools.Email.IEmailAccountStore,
    SoulCore.Inference.Tools.Email.EmailAccountStore>();
builder.Services.AddSingleton<IEmailBridge, MailKitEmailBridge>();
builder.Services.AddSingleton<ITool, EmailAccountsTool>();
builder.Services.AddSingleton<ITool, EmailInboxTool>();
builder.Services.AddSingleton<ITool, EmailReadTool>();
builder.Services.AddSingleton<ITool, EmailSearchTool>();
builder.Services.AddSingleton<ITool, EmailFileTool>();
builder.Services.AddSingleton<ITool, EmailMarkTool>();
builder.Services.AddSingleton<ITool, EmailDeleteTool>();
builder.Services.AddSingleton<ITool, EmailSendTool>();

builder.Services.AddSingleton<PresenceWsHub>();
builder.Services.AddSingleton<IWsFrameAdapter>(sp => sp.GetRequiredService<PresenceWsHub>());
builder.Services.AddSingleton<ICompanionOutboundMessenger, CompanionOutboundMessenger>();
builder.Services.AddSingleton<CompanionCallSessionStore>();
builder.Services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(5) });
builder.Services.AddSingleton<ComfyUiClient>();
builder.Services.AddSingleton<ICompanionMediaService, CompanionMediaService>();
builder.Services.AddSingleton<ISmsOutboundService>(sp => new SmsOutboundService(
    sp.GetRequiredService<IOptions<SmsOptions>>(),
    sp.GetRequiredService<ILogger<SmsOutboundService>>(),
    sp.GetService<IVictoriaBrowserViewHub>(),
    sp.GetService<IDesktopViewHub>(),
    sp.GetService<IHttpClientFactory>()));
builder.Services.AddSingleton<ISmsInboundService, SmsInboundService>();
builder.Services.AddSingleton<ITool, SendScreenshotMmsTool>();
builder.Services.AddHttpClient("sms-outbound-webhook");
builder.Services.AddSingleton<SoulLoopScaffold>();
builder.Services.AddSingleton<ISoulLoop>(sp => sp.GetRequiredService<SoulLoopScaffold>());
builder.Services.AddHostedService<SoulLoopHostedService>();
builder.Services.AddSingleton<ChatWebSocketHandler>();

if (unrealOptions.Enabled)
{
    builder.Services.AddSingleton<UnrealVerbClientStub>();
    builder.Services.AddSingleton<IUnrealVerbClient>(sp => sp.GetRequiredService<UnrealVerbClientStub>());
    builder.Services.AddSingleton<IUnrealEyeCaptureClient>(sp => sp.GetRequiredService<UnrealVerbClientStub>());
    builder.Services.AddSingleton<IUnrealCallCameraClient>(sp => sp.GetRequiredService<UnrealVerbClientStub>());
}
else
{
    builder.Services.AddSingleton<IUnrealVerbClient, NullUnrealVerbClient>();
    builder.Services.AddSingleton<IUnrealEyeCaptureClient, NullUnrealCaptureClient>();
    builder.Services.AddSingleton<IUnrealCallCameraClient, NullUnrealCaptureClient>();
}

// OllamaInferenceClient's DI ctor requires IUeLiveSignal. Without this, every
// chat/WS turn throws and Victoria appears "dead" while /health still 200s.
builder.Services.AddSingleton<IUeLiveSignal, UnrealUeLiveSignal>();

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
    SoulCore.Inference.Presence.IPresenceActivityHub presenceActivity,
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
            provider = inferenceOptions.Enabled
                ? (inferenceOptions.IsCloudEndpoint ? "ollama-cloud" : "ollama")
                : "null",
            // BED-01 / TASK-157: expose configured chat model for QA/ops (no secrets).
            model = inferenceOptions.Model,
            cloud = inferenceOptions.IsCloudEndpoint,
            baseUrl = inferenceOptions.IsCloudEndpoint ? InferenceOptions.CloudBaseUrl : "loopback",
            embeddingsEnabled = embeddingsOn,
            embeddingModel = inferenceOptions.EmbeddingModel,
            embeddingBaseUrl = embeddingsOn
                ? (InferenceOptions.IsOllamaCloudUrl(inferenceOptions.ResolveEmbeddingBaseUrl())
                    ? InferenceOptions.CloudBaseUrl
                    : "loopback")
                : null,
            apiKeyConfigured = !string.IsNullOrWhiteSpace(inferenceOptions.ResolveApiKey())
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
        // PROP-4 BED: Presence HUD activity — short doing-now line (never loop.want slogans).
        presence = PresenceDto(presenceActivity.GetSnapshot()),
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

static object PresenceDto(SoulCore.Inference.Presence.PresenceActivitySnapshot snap) => new
{
    currentActivity = snap.Phrase,
    activitySource = snap.Source,
    activityUpdatedAt = snap.UpdatedAt
};

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
        allowEmailRead = access.AllowEmailRead,
        allowEmailSend = access.AllowEmailSend,
        allowEmailDelete = access.AllowEmailDelete,
        desktopBackend = access.DesktopBackend,
        browserBackend = access.BrowserBackend,
        mt4Backend = access.Mt4Backend,
        desktopTargetWindowTitle = access.DesktopTargetWindowTitle,
        cuaDriverAvailable = cuaPath is not null,
        cuaDriverPath = cuaPath,
        scope = "session",
        note = "Session gates until Host restart. Seeded from Tools in appsettings.json (desktop/browser capture + computer control default on; email read/send/delete default off). SoftCursorRestore + DesktopBackend=cua = LLMOD-style agent cursor (blue overlay; your mouse stays put). Non-empty DesktopTargetWindowTitle hard-scopes desktop_* to that VM/window title substring. Email accounts bind from Email:Accounts (env passwords only)."
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
    if (ReadBool(root, "allowEmailRead") is { } emailRead)
        access.SetAllowEmailRead(emailRead);
    if (ReadBool(root, "allowEmailSend") is { } emailSend)
        access.SetAllowEmailSend(emailSend);
    if (ReadBool(root, "allowEmailDelete") is { } emailDelete)
        access.SetAllowEmailDelete(emailDelete);

    return Results.Json(ToolsSettingsDto(access));
});

// Email account credentials (Presence Settings + companion). Passwords never echoed.
// Auth mirrors companion API when SOULCORE_COMPANION_API_TOKEN is set.
app.MapGet("/settings/email", (SoulCore.Inference.Tools.Email.IEmailAccountStore store) =>
{
    var accounts = store.ListAccounts().Select(store.ToPublicDto).ToArray();
    return Results.Json(new
    {
        accounts,
        note = "Passwords are write-only. Leave password blank to keep the current secret. Runtime overrides live under %LOCALAPPDATA%/SoulCore/email-accounts.runtime.json."
    });
}).AddEndpointFilter(CompanionEmailAuthFilter);

app.MapPost("/settings/email", async (
    HttpRequest request,
    SoulCore.Inference.Tools.Email.IEmailAccountStore store) =>
{
    using var doc = await JsonDocument.ParseAsync(request.Body).ConfigureAwait(false);
    var root = doc.RootElement;
    if (root.ValueKind != JsonValueKind.Object)
        return Results.BadRequest(new { error = "expected JSON object" });

    static string? ReadString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String)
            return null;
        return p.GetString();
    }

    static int? ReadInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n))
            return n;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var s))
            return s;
        return null;
    }

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

    var id = ReadString(root, "id");
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new { error = "id required (victoria | personal | business)" });

    try
    {
        var updated = store.Upsert(new SoulCore.Inference.Tools.Email.EmailAccountWriteRequest
        {
            Id = id,
            Role = ReadString(root, "role"),
            DisplayName = ReadString(root, "displayName"),
            Address = ReadString(root, "address"),
            ImapHost = ReadString(root, "imapHost"),
            ImapPort = ReadInt(root, "imapPort"),
            ImapUseSsl = ReadBool(root, "imapUseSsl"),
            SmtpHost = ReadString(root, "smtpHost"),
            SmtpPort = ReadInt(root, "smtpPort"),
            SmtpUseSsl = ReadBool(root, "smtpUseSsl"),
            Username = ReadString(root, "username"),
            Password = ReadString(root, "password"),
            Enabled = ReadBool(root, "enabled")
        });

        return Results.Json(new
        {
            ok = true,
            account = store.ToPublicDto(updated)
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).AddEndpointFilter(CompanionEmailAuthFilter);

static async ValueTask<object?> CompanionEmailAuthFilter(
    EndpointFilterInvocationContext context,
    EndpointFilterDelegate next)
{
    var http = context.HttpContext;
    var config = http.RequestServices.GetService<IConfiguration>();
    var token = CompanionWsAuth.ResolveConfiguredToken(config);
    var outcome = CompanionWsAuth.Evaluate(http.Request, token);
    if (outcome is CompanionWsAuth.AuthOutcome.Missing or CompanionWsAuth.AuthOutcome.Invalid)
    {
        var logger = http.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SoulCore.Host.Settings.Email.Auth");
        logger.LogWarning(
            "Email settings rejected ({Safe})",
            CompanionWsAuth.FormatLogSafe(outcome, CompanionWsAuth.DescribeHeaderSource(http.Request)));
        return Results.Unauthorized();
    }

    return await next(context).ConfigureAwait(false);
}

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
    var recent = (snap.Recent ?? Array.Empty<DesktopViewGalleryEntry>())
        .Select(r => new
        {
            fileName = r.FileName,
            path = r.Path,
            source = r.Source,
            format = r.Format,
            width = r.Width,
            height = r.Height,
            capturedAt = r.CapturedAt,
            action = r.Action,
            imageUrl = "/desktop/view/gallery/" + Uri.EscapeDataString(r.FileName)
        })
        .ToArray();
    return Results.Json(new
    {
        hasImage = snap.HasImage,
        imagePath = "/desktop/view/image",
        diskPath = snap.ImagePath,
        galleryDir = snap.GalleryDir ?? view.GalleryDirectory,
        format = snap.Format,
        width = snap.Width,
        height = snap.Height,
        cursorX = snap.CursorX,
        cursorY = snap.CursorY,
        lastAction = snap.LastAction,
        updatedAt = snap.UpdatedAt,
        softCursorRestore = snap.SoftCursorRestore,
        source = snap.Source,
        recent,
        note = "Last image Victoria actually captured (source=desktop|eyes|browser). Every capture is also written under galleryDir (temp ring buffer). Open diskPath / recent[].path on this machine."
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

// BED-186: serve a gallery frame by basename (loopback Presence UI).
app.MapGet("/desktop/view/gallery/{fileName}", (string fileName, IDesktopViewHub view) =>
{
    var bytes = view.TryGetGalleryImageBytes(fileName);
    if (bytes is null || bytes.Length == 0)
        return Results.NotFound();

    var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
    var contentType = ext switch
    {
        "png" => "image/png",
        "jpg" or "jpeg" => "image/jpeg",
        "webp" => "image/webp",
        _ => "image/bmp"
    };
    return Results.File(bytes, contentType);
});

// FED-196 / BED-195: near-live Victoria Playwright browser (in-memory; not gallery).
app.MapGet("/browser/view", (IVictoriaBrowserViewHub view) =>
{
    var snap = view.GetSnapshot();
    return Results.Json(new
    {
        hasImage = snap.HasImage,
        imagePath = "/browser/view/image",
        url = snap.Url,
        title = snap.Title,
        lastAction = snap.LastAction,
        waitingOnYou = snap.WaitingOnYou,
        backend = snap.Backend,
        updatedAt = snap.UpdatedUtc,
        note = "Victoria's dedicated Playwright Chromium (not Kurt's Chrome). In-memory stream only — not written to desktop screenshot gallery."
    });
});

app.MapGet("/browser/view/image", (IVictoriaBrowserViewHub view) =>
{
    if (!view.TryGetImageBytes(out var bytes, out var contentType) || bytes is null || bytes.Length == 0)
        return Results.NotFound();
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
        SecretNames.CompanionApiToken,
        SecretNames.OllamaApiKey,
        SecretNames.VboxGuestPass
    };

    // Load .env the same way Host startup does so this report matches live auth.
    DotEnvLoader.TryLoad();

    var envPath = DotEnvLoader.ResolveEnvFilePath();
    Console.WriteLine($"env_file_found={!string.IsNullOrEmpty(envPath)}");
    if (!string.IsNullOrEmpty(envPath))
        Console.WriteLine($"env_file_path={envPath}");

    var allPresent = true;
    foreach (var key in keys)
    {
        var value = Environment.GetEnvironmentVariable(key);
        var present = !string.IsNullOrEmpty(value);
        var length = present ? value!.Length : 0;
        allPresent &= present;
        // Fingerprint only — never print the secret. Lets Kurt compare .env vs Host.
        var fp = present ? ShortFingerprint(value!) : "-";
        Console.WriteLine($"{key}: present={present} length={length} fp={fp}");
    }

    Console.WriteLine($"all_present={allPresent}");
    return allPresent ? 0 : 1;
}

static string ShortFingerprint(string secret)
{
    var hash = System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(secret));
    return Convert.ToHexString(hash.AsSpan(0, 4)).ToLowerInvariant();
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

static void ConfigureOllamaHttpClient(
    HttpClient client,
    string baseUrl,
    int timeoutSeconds,
    string? apiKey)
{
    client.BaseAddress = NormalizeBaseUri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds));
    client.DefaultRequestHeaders.Remove("Authorization");
    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
}

static string NormalizePath(string path)
{
    if (string.IsNullOrWhiteSpace(path))
        return "/ws";
    return path.StartsWith('/') ? path : "/" + path;
}

// Expose for integration tests later
public partial class Program;
