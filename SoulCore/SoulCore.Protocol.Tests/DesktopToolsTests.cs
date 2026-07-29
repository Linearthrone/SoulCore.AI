using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Hermes;
using SoulCore.Inference;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// BED-135: desktop tools + session opt-in gate. Control tools must refuse
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
    [InlineData("desktop_type", """{"text":"hi"}""")]
    [InlineData("desktop_key", """{"key":"Enter"}""")]
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
        Assert.Empty(backend.TypeCalls);
        Assert.Empty(backend.KeyCalls);
    }

    [Fact]
    public async Task ControlTools_GateClosed_NoInjection_VerifiedViaMockCallCount()
    {
        var backend = new MockDesktopBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: false);

        await new DesktopClickTool(gate, backend).ExecuteAsync(
            JsonDocument.Parse("""{"x":1,"y":2}""").RootElement);
        await new DesktopTypeTool(gate, backend).ExecuteAsync(
            JsonDocument.Parse("""{"text":"x"}""").RootElement);
        await new DesktopKeyTool(gate, backend).ExecuteAsync(
            JsonDocument.Parse("""{"key":"Escape"}""").RootElement);

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
        Assert.Equal((100, 200, "right"), backend.ClickCalls[0]);
        Assert.Contains("clicked", result.Content, StringComparison.OrdinalIgnoreCase);
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
    // Definitions + DI registration
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("desktop_screenshot")]
    [InlineData("desktop_click")]
    [InlineData("desktop_type")]
    [InlineData("desktop_key")]
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
            "desktop_type" => new DesktopTypeTool(gate, backend),
            "desktop_key" => new DesktopKeyTool(gate, backend),
            "list_desktop_windows" => new ListDesktopWindowsTool(gate, backend),
            "focus_desktop_window" => new FocusDesktopWindowTool(gate, backend),
            _ => throw new InvalidOperationException(name),
        };
        Assert.Equal(name, tool.Definition.Name);
        Assert.Equal(JsonValueKind.Object, tool.Definition.Parameters.ValueKind);
    }

    [Fact]
    public void Registry_IncludesAllSixDesktopTools()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<ToolsOptions>>(Options.Create(new ToolsOptions
        {
            AllowDesktopCapture = true,
            AllowComputerControl = false,
            DesktopBackend = "native",
        }));
        services.AddSingleton<IComputerControlGate, ComputerControlGate>();
        services.AddSingleton<IDesktopControlBackend, MockDesktopBackend>();
        services.AddSingleton<ITool, DesktopScreenshotTool>();
        services.AddSingleton<ITool, DesktopClickTool>();
        services.AddSingleton<ITool, DesktopTypeTool>();
        services.AddSingleton<ITool, DesktopKeyTool>();
        services.AddSingleton<ITool, ListDesktopWindowsTool>();
        services.AddSingleton<ITool, FocusDesktopWindowTool>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        var names = registry.GetDefinitions().Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("desktop_screenshot", names);
        Assert.Contains("desktop_click", names);
        Assert.Contains("desktop_type", names);
        Assert.Contains("desktop_key", names);
        Assert.Contains("list_desktop_windows", names);
        Assert.Contains("focus_desktop_window", names);
        Assert.Equal(6, names.Count);
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
    public void ToolsOptions_Defaults_GateClosedCaptureOpen()
    {
        var opts = new ToolsOptions();
        Assert.True(opts.AllowDesktopCapture);
        Assert.False(opts.AllowComputerControl);
        Assert.Equal("native", opts.DesktopBackend);
    }

    private static ITool CreateControlTool(string name, IComputerControlGate gate, IDesktopControlBackend backend)
        => name switch
        {
            "desktop_click" => new DesktopClickTool(gate, backend),
            "desktop_type" => new DesktopTypeTool(gate, backend),
            "desktop_key" => new DesktopKeyTool(gate, backend),
            _ => throw new InvalidOperationException(name),
        };

    private sealed class MockDesktopBackend : IDesktopControlBackend
    {
        public List<(int x, int y, string button)> ClickCalls { get; } = new();
        public List<string> TypeCalls { get; } = new();
        public List<string> KeyCalls { get; } = new();
        public List<int> ScreenshotCalls { get; } = new();
        public List<string> FocusCalls { get; } = new();
        public int ListWindowsCalls { get; private set; }

        public int TotalCalls =>
            ClickCalls.Count + TypeCalls.Count + KeyCalls.Count
            + ScreenshotCalls.Count + FocusCalls.Count + ListWindowsCalls;

        public DesktopOpResult? ScreenshotResult { get; set; }
        public DesktopOpResult? ListWindowsResult { get; set; }

        public Task<DesktopOpResult> ScreenshotAsync(int monitor, CancellationToken ct = default)
        {
            ScreenshotCalls.Add(monitor);
            return Task.FromResult(ScreenshotResult ?? new DesktopOpResult(true, $"shot monitor={monitor}", null));
        }

        public Task<DesktopOpResult> ClickAsync(int x, int y, string button, CancellationToken ct = default)
        {
            ClickCalls.Add((x, y, button));
            return Task.FromResult(new DesktopOpResult(true, $"clicked {button} at ({x},{y})", null));
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

        public Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default)
        {
            ListWindowsCalls++;
            return Task.FromResult(ListWindowsResult ?? new DesktopOpResult(true, "open desktop windows:", Array.Empty<string>()));
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
            if (string.IsNullOrWhiteSpace(_health))
                return Task.FromResult(new ToolResult(false, IHermesMcpInvoker.UnavailableMessage, null));
            return Task.FromResult(new ToolResult(true, $"mcp:{mcpToolName}", null));
        }
    }
}
