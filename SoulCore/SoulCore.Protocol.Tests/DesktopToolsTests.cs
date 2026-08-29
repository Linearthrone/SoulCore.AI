using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Hermes;
using SoulCore.Inference;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// BED-135 / BED-174: desktop tools + session opt-in gate. Control tools must refuse
/// when the gate is closed and must not call the backend; capture tools work
/// when AllowDesktopCapture is true. Tests use a mock backend — no real input.
/// </summary>
public class DesktopToolsTests
{
    private const string AuthMsg = DesktopToolGate.ControlRequiresAuthorization;

    // ─────────────────────────────────────────────────────────────────────
    // Gate closed — control tools refuse, backend untouched
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("desktop_click", """{"x":10,"y":20}""")]
    [InlineData("desktop_drag", """{"x1":10,"y1":20,"x2":30,"y2":40}""")]
    [InlineData("desktop_type", """{"text":"hi"}""")]
    [InlineData("desktop_key", """{"key":"Enter"}""")]
    [InlineData("desktop_scroll", """{"x":10,"y":20,"deltaY":-120}""")]
    [InlineData("desktop_open_app", """{"app":"chrome"}""")]
    public async Task ControlTools_GateClosed_RefuseAndDoNotDispatch(string toolName, string argsJson)
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: false);
        var tool = CreateControlTool(toolName, gate, backend);
        var args = JsonDocument.Parse(argsJson).RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Equal(AuthMsg, result.Content);
        Assert.Equal(0, backend.TotalCalls);
        Assert.Empty(backend.ClickCalls);
        Assert.Empty(backend.DragCalls);
        Assert.Empty(backend.TypeCalls);
        Assert.Empty(backend.KeyCalls);
        Assert.Empty(backend.ScrollCalls);
        Assert.Empty(backend.OpenAppCalls);
    }

    [Fact]
    public async Task ControlTools_GateClosed_NoInjection_VerifiedViaMockCallCount()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: false);

        await new DesktopClickTool(gate, backend).ExecuteAsync(
            JsonDocument.Parse("""{"x":1,"y":2}""").RootElement);
        await new DesktopDragTool(gate, backend).ExecuteAsync(
            JsonDocument.Parse("""{"x1":1,"y1":2,"x2":3,"y2":4}""").RootElement);
        await new DesktopTypeTool(gate, backend).ExecuteAsync(
            JsonDocument.Parse("""{"text":"x"}""").RootElement);
        await new DesktopKeyTool(gate, backend).ExecuteAsync(
            JsonDocument.Parse("""{"key":"Escape"}""").RootElement);
        await new DesktopScrollTool(gate, backend).ExecuteAsync(
            JsonDocument.Parse("""{"x":1,"y":2,"deltaY":-120}""").RootElement);
        await new DesktopOpenAppTool(gate, backend).ExecuteAsync(
            JsonDocument.Parse("""{"app":"chrome"}""").RootElement);

        Assert.Equal(0, backend.TotalCalls);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Gate open — control tools dispatch to backend
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DesktopClick_GateOpen_DispatchesToBackend()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true);
        var tool = new DesktopClickTool(gate, backend);
        var args = JsonDocument.Parse("""{"x":100,"y":200,"button":"right"}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Single(backend.ClickCalls);
        Assert.Equal((100, 200, "right", 1), backend.ClickCalls[0]);
        Assert.Contains("clicked", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DesktopClick_DoubleClick_DispatchesClicks2()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true);
        var tool = new DesktopClickTool(gate, backend);
        var args = JsonDocument.Parse("""{"x":50,"y":60,"clicks":2}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Single(backend.ClickCalls);
        Assert.Equal((50, 60, "left", 2), backend.ClickCalls[0]);
        Assert.Contains("double-clicked", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DesktopDrag_GateOpen_DispatchesToBackend()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true);
        var tool = new DesktopDragTool(gate, backend);
        var args = JsonDocument.Parse("""{"x1":10,"y1":20,"x2":110,"y2":20,"button":"left"}""").RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Single(backend.DragCalls);
        Assert.Equal((10, 20, 110, 20, "left"), backend.DragCalls[0]);
        Assert.Contains("dragged", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DesktopType_GateOpen_DispatchesToBackend()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true);
        var tool = new DesktopTypeTool(gate, backend);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{"text":"hello"}""").RootElement);

        Assert.True(result.Success);
        Assert.Equal(new[] { "hello" }, backend.TypeCalls);
    }

    [Fact]
    public async Task DesktopKey_GateOpen_DispatchesToBackend()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true);
        var tool = new DesktopKeyTool(gate, backend);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{"key":"Enter"}""").RootElement);

        Assert.True(result.Success);
        Assert.Equal(new[] { "Enter" }, backend.KeyCalls);
    }

    [Fact]
    public async Task DesktopKey_Chord_DispatchesCtrlL()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true);
        var tool = new DesktopKeyTool(gate, backend);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{"key":"Ctrl+L"}""").RootElement);

        Assert.True(result.Success);
        Assert.Equal(new[] { "Ctrl+L" }, backend.KeyCalls);
    }

    [Fact]
    public async Task DesktopScroll_GateOpen_DispatchesToBackend()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true);
        var tool = new DesktopScrollTool(gate, backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"x":100,"y":200,"deltaY":-120,"deltaX":0}""").RootElement);

        Assert.True(result.Success);
        Assert.Single(backend.ScrollCalls);
        Assert.Equal((100, 200, -120, 0), backend.ScrollCalls[0]);
        Assert.Contains("scrolled", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DesktopOpenApp_GateOpen_DispatchesToBackend()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true);
        var tool = new DesktopOpenAppTool(gate, backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"app":"chrome","args":"https://example.com"}""").RootElement);

        Assert.True(result.Success);
        Assert.Single(backend.OpenAppCalls);
        Assert.Equal(("chrome", "https://example.com"), backend.OpenAppCalls[0]);
        Assert.Contains("opened", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DesktopOpenApp_GateClosed_RefusesWithSettingsGuidance()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: false);
        var tool = new DesktopOpenAppTool(gate, backend);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{"app":"chrome"}""").RootElement);

        Assert.False(result.Success);
        Assert.Equal(AuthMsg, result.Content);
        Assert.Contains("Settings → Tools & Access", result.Content, StringComparison.Ordinal);
        Assert.Empty(backend.OpenAppCalls);
    }

    [Fact]
    public async Task DesktopScreenshot_StillWorks_WhenControlGateOff()
    {
        var backend = new MockDesktopBackend
        {
            ScreenshotResult = new DesktopOpResult(
                true, "captured desktop screenshot", new { path = "/tmp/shot.bmp" }),
        };
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: false);
        var tool = new DesktopScreenshotTool(gate, backend);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{"monitor":0}""").RootElement);

        Assert.True(result.Success);
        Assert.Single(backend.ScreenshotCalls);
        Assert.Empty(backend.OpenAppCalls);
    }

    [Fact]
    public async Task SessionOptIn_SetAllowComputerControl_OpensGate()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: false);
        var tool = new DesktopClickTool(gate, backend);
        var args = JsonDocument.Parse("""{"x":5,"y":6}""").RootElement.Clone();

        var closed = await tool.ExecuteAsync(args);
        Assert.False(closed.Success);
        Assert.Equal(0, backend.TotalCalls);

        gate.SetAllowComputerControl(true);
        var open = await tool.ExecuteAsync(args);
        Assert.True(open.Success);
        Assert.Single(backend.ClickCalls);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Capture tools — AllowDesktopCapture
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DesktopScreenshot_CaptureAllowed_ReturnsImagePathAndBytes()
    {
        var backend = new MockDesktopBackend
        {
            ScreenshotResult = new DesktopOpResult(
                true,
                "captured desktop screenshot (monitor=0) saved to /tmp/shot.bmp (12 bytes BMP)",
                new { path = "/tmp/shot.bmp", bytes = new byte[] { 1, 2, 3 }, monitor = 0, format = "bmp" }),
        };
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: false);
        var tool = new DesktopScreenshotTool(gate, backend);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{"monitor":0}""").RootElement);

        Assert.True(result.Success);
        Assert.Contains("captured", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/tmp/shot.bmp", result.Content);
        Assert.Single(backend.ScreenshotCalls);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task DesktopScreenshot_CaptureDisabled_Refuses()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: false, allowComputerControl: false);
        var tool = new DesktopScreenshotTool(gate, backend);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{}""").RootElement);

        Assert.False(result.Success);
        Assert.Equal(DesktopToolGate.CaptureDisabled, result.Content);
        Assert.Equal(0, backend.TotalCalls);
    }

    [Fact]
    public async Task ListDesktopWindows_CaptureAllowed_Dispatches()
    {
        var backend = new MockDesktopBackend
        {
            ListWindowsResult = new DesktopOpResult(true, "open desktop windows:\n[0] Notepad", new[] { "Notepad" }),
        };
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: false);
        var tool = new ListDesktopWindowsTool(gate, backend);

        var result = await tool.ExecuteAsync(default);

        Assert.True(result.Success);
        Assert.Equal(1, backend.ListWindowsCalls);
        Assert.Contains("Notepad", result.Content);
    }

    [Fact]
    public async Task FocusDesktopWindow_CaptureAllowed_Dispatches()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: false);
        var tool = new FocusDesktopWindowTool(gate, backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"title":"Notepad"}""").RootElement);

        Assert.True(result.Success);
        Assert.Equal(new[] { "Notepad" }, backend.FocusCalls);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Allowlist resolve (no Process.Start)
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("chrome", true)]
    [InlineData("Google Chrome", true)]
    [InlineData("edge", true)]
    [InlineData("msedge", true)]
    [InlineData("notepad", true)]
    [InlineData("file explorer", true)]
    [InlineData("explorer", true)]
    [InlineData("firefox", true)]
    [InlineData("cmd", true)]
    [InlineData("powershell", true)]
    [InlineData("browser", true)]
    [InlineData("calc", false)]
    [InlineData("bash", false)]
    [InlineData("", false)]
    public void DesktopAppLauncher_Allowlist(string app, bool expected)
    {
        Assert.Equal(expected, DesktopAppLauncher.IsAllowlisted(app));
    }

    [Fact]
    public void DesktopAppLauncher_BrowserUrl_PassedAsArgs()
    {
        Assert.True(DesktopAppLauncher.TryResolve(
            "chrome", "https://google.com", out var resolved, out var error));
        Assert.Equal("", error);
        Assert.Equal("chrome", resolved.Alias);
        Assert.Contains("https://google.com", resolved.Arguments, StringComparison.Ordinal);
        Assert.True(resolved.FileName.EndsWith("chrome.exe", StringComparison.OrdinalIgnoreCase)
                    || resolved.FileName.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DesktopAppLauncher_UnknownApp_FailsClearly()
    {
        Assert.False(DesktopAppLauncher.TryResolve("evil.exe", null, out _, out var error));
        Assert.Contains("allowlist", error, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Definitions + DI registration
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("desktop_screenshot")]
    [InlineData("desktop_click")]
    [InlineData("desktop_drag")]
    [InlineData("desktop_type")]
    [InlineData("desktop_key")]
    [InlineData("desktop_scroll")]
    [InlineData("desktop_open_app")]
    [InlineData("list_desktop_windows")]
    [InlineData("focus_desktop_window")]
    public void ToolDefinitions_MatchExpectedNames(string name)
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(true, false);
        ITool tool = name switch
        {
            "desktop_screenshot" => new DesktopScreenshotTool(gate, backend),
            "desktop_click" => new DesktopClickTool(gate, backend),
            "desktop_drag" => new DesktopDragTool(gate, backend),
            "desktop_type" => new DesktopTypeTool(gate, backend),
            "desktop_key" => new DesktopKeyTool(gate, backend),
            "desktop_scroll" => new DesktopScrollTool(gate, backend),
            "desktop_open_app" => new DesktopOpenAppTool(gate, backend),
            "list_desktop_windows" => new ListDesktopWindowsTool(gate, backend),
            "focus_desktop_window" => new FocusDesktopWindowTool(gate, backend),
            _ => throw new InvalidOperationException(name),
        };
        Assert.Equal(name, tool.Definition.Name);
        Assert.Equal(JsonValueKind.Object, tool.Definition.Parameters.ValueKind);
    }

    [Fact]
    public void Registry_IncludesAllNineDesktopTools()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<ToolsOptions>>(Options.Create(new ToolsOptions
        {
            AllowDesktopCapture = true,
            AllowComputerControl = false,
            DesktopBackend = "native",
        }));
        services.AddSingleton<ComputerControlGate>();
        services.AddSingleton<IComputerControlGate>(sp => sp.GetRequiredService<ComputerControlGate>());
        services.AddSingleton<IToolsAccessSettings>(sp => sp.GetRequiredService<ComputerControlGate>());
        services.AddSingleton<IDesktopControlBackend, MockDesktopBackend>();
        services.AddSingleton<ITool, DesktopScreenshotTool>();
        services.AddSingleton<ITool, DesktopClickTool>();
        services.AddSingleton<ITool, DesktopDragTool>();
        services.AddSingleton<ITool, DesktopTypeTool>();
        services.AddSingleton<ITool, DesktopKeyTool>();
        services.AddSingleton<ITool, DesktopScrollTool>();
        services.AddSingleton<ITool, DesktopOpenAppTool>();
        services.AddSingleton<ITool, ListDesktopWindowsTool>();
        services.AddSingleton<ITool, FocusDesktopWindowTool>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        var names = registry.GetDefinitions().Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("desktop_screenshot", names);
        Assert.Contains("desktop_click", names);
        Assert.Contains("desktop_drag", names);
        Assert.Contains("desktop_type", names);
        Assert.Contains("desktop_key", names);
        Assert.Contains("desktop_scroll", names);
        Assert.Contains("desktop_open_app", names);
        Assert.Contains("list_desktop_windows", names);
        Assert.Contains("focus_desktop_window", names);
        Assert.Equal(9, names.Count);
    }

    [Fact]
    public async Task HermesBackend_WhenGatewayDown_ReturnsUnavailable()
    {
        var hermes = new StubHermesClient(health: "");
        var backend = new HermesDesktopControlBackend(hermes);

        var result = await backend.ScreenshotAsync(0);

        Assert.False(result.Success);
        Assert.Equal(HermesDesktopControlBackend.GatewayUnavailable, result.Content);
    }

    [Fact]
    public async Task HermesBackend_WhenGatewayUp_RoutesViaCallMcpToolAsync()
    {
        var hermes = new StubHermesClient(health: "{\"status\":\"ok\"}");
        var backend = new HermesDesktopControlBackend(hermes);

        var result = await backend.ClickAsync(1, 2, "left");

        Assert.True(result.Success);
        Assert.Contains("computer_use", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HermesBackend_OpenApp_ReturnsUseNativeDirective()
    {
        var hermes = new StubHermesClient(health: "{\"status\":\"ok\"}");
        var backend = new HermesDesktopControlBackend(hermes);

        var result = await backend.OpenAppAsync("chrome");

        Assert.False(result.Success);
        Assert.Equal(HermesDesktopControlBackend.OpenAppUseNativeMessage, result.Content);
        Assert.Empty(hermes.McpCalls);
    }

    [Fact]
    public void ToolsOptions_Defaults_DesktopBrowserGatesOpen()
    {
        var opts = new ToolsOptions();
        Assert.True(opts.AllowDesktopCapture);
        Assert.True(opts.AllowBrowserCapture);
        Assert.True(opts.AllowComputerControl);
        Assert.Equal("cua", opts.DesktopBackend);
        Assert.Equal("native", opts.BrowserBackend);
        Assert.Equal("http://127.0.0.1:17891", opts.BrowserBridgeUrl);
        Assert.Equal("", opts.DesktopTargetWindowTitle);
        Assert.False(opts.AllowEmailRead);
        Assert.False(opts.AllowEmailSend);
        Assert.False(opts.AllowEmailDelete);
    }

    private static ITool CreateControlTool(string name, IComputerControlGate gate, IDesktopControlBackend backend)
        => name switch
        {
            "desktop_click" => new DesktopClickTool(gate, backend),
            "desktop_drag" => new DesktopDragTool(gate, backend),
            "desktop_type" => new DesktopTypeTool(gate, backend),
            "desktop_key" => new DesktopKeyTool(gate, backend),
            "desktop_scroll" => new DesktopScrollTool(gate, backend),
            "desktop_open_app" => new DesktopOpenAppTool(gate, backend),
            _ => throw new InvalidOperationException(name),
        };

    private sealed class MockDesktopBackend : IDesktopControlBackend
    {
        public List<(int x, int y, string button, int clicks)> ClickCalls { get; } = new();
        public List<(int x1, int y1, int x2, int y2, string button)> DragCalls { get; } = new();
        public List<string> TypeCalls { get; } = new();
        public List<string> KeyCalls { get; } = new();
        public List<(int x, int y, int deltaY, int deltaX)> ScrollCalls { get; } = new();
        public List<(string app, string? args)> OpenAppCalls { get; } = new();
        public List<int> ScreenshotCalls { get; } = new();
        public List<string> FocusCalls { get; } = new();
        public int ListWindowsCalls { get; private set; }

        public int TotalCalls =>
            ClickCalls.Count + DragCalls.Count + TypeCalls.Count + KeyCalls.Count
            + ScrollCalls.Count + OpenAppCalls.Count
            + ScreenshotCalls.Count + FocusCalls.Count + ListWindowsCalls;

        public DesktopOpResult? ScreenshotResult { get; set; }
        public DesktopOpResult? ListWindowsResult { get; set; }

        public Task<DesktopOpResult> ScreenshotAsync(int monitor, CancellationToken ct = default)
        {
            ScreenshotCalls.Add(monitor);
            return Task.FromResult(ScreenshotResult ?? new DesktopOpResult(true, $"shot monitor={monitor}", null));
        }

        public Task<DesktopOpResult> ClickAsync(
            int x, int y, string button, int clicks = 1, CancellationToken ct = default)
        {
            ClickCalls.Add((x, y, button, clicks));
            var label = clicks == 2 ? $"double-clicked {button}" : $"clicked {button}";
            return Task.FromResult(new DesktopOpResult(true, $"{label} at ({x},{y})", null));
        }

        public Task<DesktopOpResult> DragAsync(
            int x1, int y1, int x2, int y2, string button, CancellationToken ct = default)
        {
            DragCalls.Add((x1, y1, x2, y2, button));
            return Task.FromResult(new DesktopOpResult(
                true, $"dragged {button} from ({x1},{y1}) to ({x2},{y2})", null));
        }

        public Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default)
        {
            TypeCalls.Add(text);
            return Task.FromResult(new DesktopOpResult(true, $"typed {text.Length} character(s)", null));
        }

        public Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default)
        {
            KeyCalls.Add(key);
            return Task.FromResult(new DesktopOpResult(true, $"pressed key '{key}'", null));
        }

        public Task<DesktopOpResult> ScrollAsync(
            int x, int y, int deltaY, int deltaX = 0, CancellationToken ct = default)
        {
            ScrollCalls.Add((x, y, deltaY, deltaX));
            return Task.FromResult(new DesktopOpResult(
                true, $"scrolled at ({x},{y}) deltaY={deltaY} deltaX={deltaX}", null));
        }

        public Task<DesktopOpResult> OpenAppAsync(
            string app, string? args = null, CancellationToken ct = default)
        {
            OpenAppCalls.Add((app, args));
            var note = args is null ? $"opened app '{app}'" : $"opened app '{app}' args={args}";
            return Task.FromResult(new DesktopOpResult(true, note, null));
        }

        public Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default)
        {
            ListWindowsCalls++;
            return Task.FromResult(ListWindowsResult ?? new DesktopOpResult(true, "open desktop windows:", Array.Empty<object>()));
        }

        public Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default)
        {
            FocusCalls.Add(title);
            return Task.FromResult(new DesktopOpResult(true, $"focused window '{title}'", null));
        }
    }

    private sealed class StubHermesClient : IHermesClient
    {
        private readonly string _health;

        public List<string> McpCalls { get; } = new();

        public StubHermesClient(string health) => _health = health;

        public Task<string> ChatAsync(
            string message,
            string? systemPreamble = null,
            CancellationToken cancellationToken = default,
            int? maxTokens = null)
            => Task.FromResult(string.Empty);

        public Task<string> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            IToolRegistry registry,
            CancellationToken cancellationToken = default,
            ToolLoopOptions? loopOptions = null)
            => Task.FromResult(string.Empty);

        public Task<string> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_health);

        public Task EnsureMcpReadyAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_health))
                return Task.FromException(new InvalidOperationException(IHermesMcpInvoker.UnavailableMessage));
            return Task.CompletedTask;
        }

        public Task<ToolResult> CallMcpToolAsync(
            string mcpToolName,
            JsonElement arguments,
            CancellationToken cancellationToken = default)
        {
            McpCalls.Add(mcpToolName);
            if (string.IsNullOrWhiteSpace(_health))
                return Task.FromResult(new ToolResult(false, IHermesMcpInvoker.UnavailableMessage, null));
            return Task.FromResult(new ToolResult(true, $"mcp:{mcpToolName}", null));
        }
    }
}
