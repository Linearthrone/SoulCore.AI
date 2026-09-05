using Microsoft.Extensions.DependencyInjection;
using SoulCore.Adapters.Ws;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Abstractions;
using SoulCore.Inference.Tools.Body;
using SoulCore.Inference.Tools.Browser;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Inference.Tools.Email;
using SoulCore.Inference.Tools.FS;
using SoulCore.Inference.Tools.Meta;
using SoulCore.Inference.Tools.Trading;
using SoulCore.Inference.Tools.Workflow;
using SoulCore.Memory;

namespace SoulCore.Host.Hosting.ServiceCollectionExtensions;

internal static class ToolsServiceCollectionExtensions
{
    internal static IServiceCollection AddTools(this IServiceCollection services)
    {
        // Tool registry (agent-loop foundation, BED-125). Additive — independent of
        // inference enablement. Concrete tools (BED-131+) register as ITool
        // singletons elsewhere; ToolRegistry collects them via IEnumerable<ITool>.
        // Empty registry is valid → Host boots clean with zero tools.
        services.AddSingleton<IToolRegistry, ToolRegistry>();

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
        services.AddSingleton<ITool>(sp => new ListToolsTool(sp));
        services.AddSingleton<ITool, SystemInfoTool>();
        services.AddSingleton<ITool, ReadFileTool>();
        services.AddSingleton<ITool, WriteFileTool>();
        services.AddSingleton<ITool, ListDirTool>();

        // Body tools (BED-132): speak / play_animation / move_to / look_at / set_emotion
        // wrap IUnrealVerbClient so the model can choose body actions mid-loop.
        // Keyword detectors remain as Strategy A fallback (BED-128).
        services.AddSingleton<ITool, SpeakTool>();
        // Hub injected after IDesktopViewHub registration (factory resolves at first use).
        services.AddSingleton<ITool>(sp => new VictoriaEyeCaptureTool(
            sp.GetRequiredService<IUnrealVerbClient>(),
            sp.GetRequiredService<IDesktopViewHub>()));
        services.AddSingleton<ITool, PlayAnimationTool>();
        services.AddSingleton<ITool, LocoTool>();
        services.AddSingleton<ITool, MoveToTool>();
        services.AddSingleton<ITool, LookAtTool>();
        services.AddSingleton<ITool, SetEmotionTool>();

        // Task tools (BED-140): task_create / task_get / task_update_status / task_list
        // wrap IVictoriaTaskStore (SQLite victoria_tasks). Victoria's own work items —
        // not the PM ticket folder. Workflow tools (BED-141) are separate.
        services.AddSingleton<ITool, TaskCreateTool>();
        services.AddSingleton<ITool, TaskGetTool>();
        services.AddSingleton<ITool, TaskUpdateStatusTool>();
        services.AddSingleton<ITool, TaskListTool>();

        // Workflow tools (BED-141): workflow_create / workflow_execute / workflow_get.
        // workflow_execute resolves IToolRegistry lazily via IServiceProvider (ListToolsTool pattern).
        services.AddSingleton<ITool>(sp => new WorkflowExecuteTool(
            sp.GetRequiredService<IVictoriaWorkflowStore>(), sp));
        services.AddSingleton<ITool, WorkflowCreateTool>();
        services.AddSingleton<ITool, WorkflowGetTool>();

        RegisterDesktopTools(services);
        RegisterChiefArchitectTools(services);
        RegisterBrowserTools(services);
        RegisterMt4Tools(services);
        RegisterEmailTools(services);

        return services;
    }

    private static void RegisterDesktopTools(IServiceCollection services)
    {
        // Desktop tools (BED-135): capture + click/type/key with session gates.
        // AllowDesktopCapture / AllowBrowserCapture / AllowComputerControl default true (TASK-177).
        // Backend: Tools:DesktopBackend = "cua" | "native".
        // cua = local cua-driver agent cursor (LLMOD blue overlay; OS mouse untouched).
        // Optional Tools:DesktopTargetWindowTitle hard-scopes clicks to that window (BED-188).
        // Session gates are mutable via GET/POST /settings/tools (Settings → Tools & Access).
        services.AddSingleton<ComputerControlGate>();
        services.AddSingleton<IComputerControlGate>(sp => sp.GetRequiredService<ComputerControlGate>());
        services.AddSingleton<IToolsAccessSettings>(sp => sp.GetRequiredService<ComputerControlGate>());
        services.AddSingleton<IDesktopViewHub>(sp =>
            new DesktopViewHub(() => sp.GetRequiredService<IToolsAccessSettings>().SoftCursorRestore));
        // PROP-4: honest Presence activity (doing-now), not SoulLoop want slogans.
        services.AddSingleton<SoulCore.Inference.Presence.IPresenceActivityHub>(sp =>
            new SoulCore.Inference.Presence.PresenceActivityHub(sp.GetRequiredService<IDesktopViewHub>()));
        services.AddSingleton<GuestVmBrowserBridgeHolder>();
        services.AddSingleton<IVictoriaBrowserViewHub, VictoriaBrowserViewHub>();
        services.AddSingleton<IDesktopControlBackend>(sp =>
        {
            IDesktopControlBackend inner;
            var backendName = (sp.GetRequiredService<IToolsAccessSettings>().DesktopBackend ?? "cua").Trim();
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
        services.AddSingleton<ITool>(sp => new DesktopScreenshotTool(
            sp.GetRequiredService<IComputerControlGate>(),
            sp.GetRequiredService<IDesktopControlBackend>(),
            sp.GetRequiredService<IDesktopViewHub>()));
        services.AddSingleton<ITool, DesktopClickTool>();
        services.AddSingleton<ITool, DesktopDragTool>();
        services.AddSingleton<ITool, DesktopTypeTool>();
        services.AddSingleton<ITool, DesktopKeyTool>();
        services.AddSingleton<ITool, DesktopScrollTool>();
        services.AddSingleton<ITool, DesktopOpenAppTool>();
        services.AddSingleton<ITool, ListDesktopWindowsTool>();
        services.AddSingleton<ITool, FocusDesktopWindowTool>();
    }

    private static void RegisterChiefArchitectTools(IServiceCollection services)
    {
        // Chief Architect X17 playbook tools (plan → recipe → desktop_* execution).
        services.AddSingleton<SoulCore.Inference.Tools.ChiefArchitect.CaPlaybookLibrary>();
        services.AddSingleton<SoulCore.Inference.Tools.ChiefArchitect.CaSessionState>();
        services.AddSingleton<ITool, SoulCore.Inference.Tools.ChiefArchitect.CaCompileBriefTool>();
        services.AddSingleton<ITool, SoulCore.Inference.Tools.ChiefArchitect.CaPlanProjectTool>();
        services.AddSingleton<ITool, SoulCore.Inference.Tools.ChiefArchitect.CaGetRecipeTool>();
        services.AddSingleton<ITool, SoulCore.Inference.Tools.ChiefArchitect.CaNextStepTool>();
        services.AddSingleton<ITool, SoulCore.Inference.Tools.ChiefArchitect.CaWorldHintTool>();
        services.AddSingleton<ITool, SoulCore.Inference.Tools.ChiefArchitect.CaVerifyChecklistTool>();
    }

    private static void RegisterBrowserTools(IServiceCollection services)
    {
        // Browser tools (BED-136 / BED-182 / BED-195): browser_health / capture / click / type / key / scroll.
        // Read: Tools.AllowBrowserCapture (default true). Write: Tools.AllowComputerControl.
        // Backend: Tools.BrowserBackend=playwright (BED-195 Victoria Chromium) preferred even when
        // DesktopTargetWindowTitle is set (VM stays for desktop_*; web uses Playwright).
        // native → BrowserCaptureBridge :17891.
        services.AddHttpClient("browser-bridge", (sp, client) =>
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
        services.AddSingleton<IBrowserBridge>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<ToolsOptions>>().Value;
            var backend = (opts.BrowserBackend ?? ToolsOptions.BackendNative).Trim();

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
        services.AddSingleton<ITool, BrowserHealthTool>();
        services.AddSingleton<ITool>(sp => new BrowserCaptureTabTool(
            sp.GetRequiredService<IBrowserBridge>(),
            sp.GetRequiredService<IToolsAccessSettings>(),
            sp.GetRequiredService<IDesktopViewHub>()));
        services.AddSingleton<ITool, BrowserNavigateTool>();
        services.AddSingleton<ITool, BrowserSnapshotTool>();
        services.AddSingleton<ITool, BrowserClickTextTool>();
        services.AddSingleton<ITool, BrowserFillTool>();
        services.AddSingleton<ITool, BrowserBackTool>();
        services.AddSingleton<ITool, BrowserTabsTool>();
        services.AddSingleton<ITool, BrowserClickTool>();
        services.AddSingleton<ITool, BrowserTypeTool>();
        services.AddSingleton<ITool, BrowserKeyTool>();
        services.AddSingleton<ITool, BrowserScrollTool>();
    }

    private static void RegisterMt4Tools(IServiceCollection services)
    {
        // MT4 trading tools (BED-138): AllowMt4Read / AllowMt4Trade + confirmed=true gate.
        // Mt4Backend=llmod → LlmodHttpMt4Bridge (BED-169).
        services.AddHttpClient<LlmodHttpMt4Bridge>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IMt4Bridge>(sp =>
        {
            var tools = sp.GetRequiredService<IOptions<ToolsOptions>>().Value;
            var backend = (tools.Mt4Backend ?? ToolsOptions.BackendLlmod).Trim();
            if (string.Equals(backend, ToolsOptions.BackendLlmod, StringComparison.OrdinalIgnoreCase)
                || string.Equals(backend, ToolsOptions.BackendNative, StringComparison.OrdinalIgnoreCase))
                return sp.GetRequiredService<LlmodHttpMt4Bridge>();

            return new UnavailableMt4Bridge(
                $"mt4 backend '{backend}' not supported — use '{ToolsOptions.BackendLlmod}' or '{ToolsOptions.BackendNative}'");
        });
        services.AddSingleton<ITool, Mt4StatusTool>();
        services.AddSingleton<ITool, ListSymbolsTool>();
        services.AddSingleton<ITool, GetMarketDataTool>();
        services.AddSingleton<ITool, GetOpenPositionsTool>();
        services.AddSingleton<ITool, ExecuteTradeTool>();
        services.AddSingleton<ITool, ClosePositionTool>();
        services.AddSingleton<ITool, VerifyTicketTool>();
        services.AddSingleton<ITool, MarketWatchStatusTool>();
        services.AddSingleton<ITool, ExportHistoryTool>();
        services.AddSingleton<ITool, GetHistoricalBarsTool>();
        services.AddSingleton<ITool, RunBacktestTool>();
    }

    private static void RegisterEmailTools(IServiceCollection services)
    {
        // Email tools — IMAP/SMTP multi-account (victoria / personal / business).
        // AllowEmailRead / AllowEmailSend / AllowEmailDelete + confirmed=true on send/delete.
        services.AddSingleton<SoulCore.Inference.Tools.Email.IEmailAccountStore,
            SoulCore.Inference.Tools.Email.EmailAccountStore>();
        services.AddSingleton<IEmailBridge, MailKitEmailBridge>();
        services.AddSingleton<ITool, EmailAccountsTool>();
        services.AddSingleton<ITool, EmailInboxTool>();
        services.AddSingleton<ITool, EmailReadTool>();
        services.AddSingleton<ITool, EmailSearchTool>();
        services.AddSingleton<ITool, EmailFileTool>();
        services.AddSingleton<ITool, EmailMarkTool>();
        services.AddSingleton<ITool, EmailDeleteTool>();
        services.AddSingleton<ITool, EmailSendTool>();
    }
}
