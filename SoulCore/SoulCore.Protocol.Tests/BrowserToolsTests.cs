using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Hermes;
using SoulCore.Inference;
using SoulCore.Inference.Tools.Browser;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// BED-136: browser tools + AllowBrowserCapture / AllowComputerControl gates.
/// Mock <see cref="IBrowserBridge"/> — no real Hermes / input injection.
/// </summary>
public class BrowserToolsTests
{
    private static readonly string[] BrowserToolNames =
    {
        "browser_health",
        "browser_capture_tab",
        "browser_click",
        "browser_type",
        "browser_key",
        "browser_scroll",
    };

    // ─────────────────────────────────────────────────────────────────────
    // Gate closed — control tools refuse without touching the bridge
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("browser_click", """{"x":10,"y":20}""")]
    [InlineData("browser_type", """{"text":"hi"}""")]
    [InlineData("browser_key", """{"key":"Enter"}""")]
    [InlineData("browser_scroll", """{"dx":0,"dy":100}""")]
    public async Task ControlTools_GateClosed_RefuseWithoutCallingBridge(string toolName, string argsJson)
    {
        var bridge = new RecordingBrowserBridge();
        var opts = Options.Create(new ToolsOptions
        {
            AllowBrowserCapture = true,
            AllowComputerControl = false,
            BrowserBackend = "hermes",
        });

        var tool = CreateControlTool(toolName, bridge, opts);
        var args = JsonDocument.Parse(argsJson).RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Equal(BrowserToolGate.ControlDenied, result.Content);
        Assert.Equal(0, bridge.TotalCalls);
    }

    [Fact]
    public async Task ControlTools_GateClosed_NoClickDispatched_VerifiedByCallCount()
    {
        var bridge = new RecordingBrowserBridge();
        var tool = new BrowserClickTool(
            bridge,
            Options.Create(new ToolsOptions { AllowComputerControl = false }));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"x":1,"y":2}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Empty(bridge.ClickCalls);
        Assert.DoesNotContain("clicked", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Gate open — control tools dispatch to mock backend
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BrowserClick_GateOpen_DispatchesToBridge()
    {
        var bridge = new RecordingBrowserBridge();
        var tool = new BrowserClickTool(
            bridge,
            Options.Create(new ToolsOptions { AllowComputerControl = true }));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"x":100,"y":200}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Single(bridge.ClickCalls);
        Assert.Equal((100, 200), bridge.ClickCalls[0]);
        Assert.Contains("clicked", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BrowserType_GateOpen_DispatchesToBridge()
    {
        var bridge = new RecordingBrowserBridge();
        var tool = new BrowserTypeTool(
            bridge,
            Options.Create(new ToolsOptions { AllowComputerControl = true }));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"text":"hello"}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Single(bridge.TypeCalls);
        Assert.Equal("hello", bridge.TypeCalls[0]);
    }

    [Fact]
    public async Task BrowserKey_GateOpen_DispatchesToBridge()
    {
        var bridge = new RecordingBrowserBridge();
        var tool = new BrowserKeyTool(
            bridge,
            Options.Create(new ToolsOptions { AllowComputerControl = true }));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"key":"Escape"}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Single(bridge.KeyCalls);
        Assert.Equal("Escape", bridge.KeyCalls[0]);
    }

    [Fact]
    public async Task BrowserScroll_GateOpen_DispatchesToBridge()
    {
        var bridge = new RecordingBrowserBridge();
        var tool = new BrowserScrollTool(
            bridge,
            Options.Create(new ToolsOptions { AllowComputerControl = true }));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"dx":5,"dy":-40}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Single(bridge.ScrollCalls);
        Assert.Equal((5, -40), bridge.ScrollCalls[0]);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Read tools — capture gate
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BrowserHealth_CaptureAllowed_CallsBridge()
    {
        var bridge = new RecordingBrowserBridge();
        var tool = new BrowserHealthTool(
            bridge,
            Options.Create(new ToolsOptions { AllowBrowserCapture = true }));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("{}").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Equal(1, bridge.HealthCalls);
        Assert.Contains("ok", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BrowserHealth_CaptureDisabled_RefusesWithoutCallingBridge()
    {
        var bridge = new RecordingBrowserBridge();
        var tool = new BrowserHealthTool(
            bridge,
            Options.Create(new ToolsOptions { AllowBrowserCapture = false }));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("{}").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal(BrowserToolGate.CaptureDenied, result.Content);
        Assert.Equal(0, bridge.HealthCalls);
    }

    [Fact]
    public async Task BrowserCaptureTab_CaptureAllowed_ReturnsScreenshotPathAndDom()
    {
        var bridge = new RecordingBrowserBridge
        {
            CapturePath = "/tmp/tab-0.png",
            CaptureDom = "<html><body>hi</body></html>",
        };
        var tool = new BrowserCaptureTabTool(
            bridge,
            Options.Create(new ToolsOptions { AllowBrowserCapture = true }));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"tab":0}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Contains("/tmp/tab-0.png", result.Content);
        Assert.Contains("<html>", result.Content);
        Assert.Single(bridge.CaptureCalls);
        Assert.Equal(0, bridge.CaptureCalls[0]);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task BrowserCaptureTab_CaptureDisabled_Refuses()
    {
        var bridge = new RecordingBrowserBridge();
        var tool = new BrowserCaptureTabTool(
            bridge,
            Options.Create(new ToolsOptions { AllowBrowserCapture = false }));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("{}").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal(BrowserToolGate.CaptureDenied, result.Content);
        Assert.Empty(bridge.CaptureCalls);
    }

    [Fact]
    public async Task BrowserCaptureTab_ControlGateClosed_StillWorksWhenCaptureAllowed()
    {
        // Read must not require AllowComputerControl.
        var bridge = new RecordingBrowserBridge();
        var tool = new BrowserCaptureTabTool(
            bridge,
            Options.Create(new ToolsOptions
            {
                AllowBrowserCapture = true,
                AllowComputerControl = false,
            }));

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("{}").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Single(bridge.CaptureCalls);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Definitions + registry
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void AllSixTools_Definitions_MatchExpectedNames()
    {
        var bridge = new RecordingBrowserBridge();
        var opts = Options.Create(new ToolsOptions());
        var tools = CreateAllTools(bridge, opts);

        Assert.Equal(BrowserToolNames, tools.Select(t => t.Definition.Name).ToArray());
        foreach (var tool in tools)
        {
            Assert.Equal(JsonValueKind.Object, tool.Definition.Parameters.ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(tool.Definition.Description));
        }
    }

    [Fact]
    public void Registry_IncludesAllSixBrowserTools()
    {
        var bridge = new RecordingBrowserBridge();
        var opts = Options.Create(new ToolsOptions());
        var services = new ServiceCollection();
        foreach (var tool in CreateAllTools(bridge, opts))
            services.AddSingleton<ITool>(tool);
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        var registry = services.BuildServiceProvider().GetRequiredService<IToolRegistry>();
        var names = registry.GetDefinitions().Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var expected in BrowserToolNames)
            Assert.Contains(expected, names);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Hermes bridge — unavailable when health empty; parse capture payload
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HermesBrowserBridge_WhenHealthEmpty_ReturnsUnavailable()
    {
        var hermes = new StubHermesClient(health: string.Empty);
        var bridge = new HermesBrowserBridge(hermes);

        var result = await bridge.ClickAsync(1, 2);

        Assert.False(result.Success);
        Assert.Equal(HermesBrowserBridge.UnavailableContent, result.Content);
    }

    [Fact]
    public async Task HermesBrowserBridge_WhenHealthy_RoutesViaCallMcpToolAsync()
    {
        var hermes = new StubHermesClient(health: "{\"status\":\"ok\"}");
        var bridge = new HermesBrowserBridge(hermes);

        var result = await bridge.CaptureTabAsync(0);

        Assert.True(result.Success);
        Assert.Contains("browser_bridge_capture_tab", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnsupportedBrowserBridge_ReturnsClearError()
    {
        var bridge = new UnsupportedBrowserBridge("native");
        var result = await bridge.HealthAsync();

        Assert.False(result.Success);
        Assert.Contains("native", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hermes", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static ITool CreateControlTool(string name, IBrowserBridge bridge, IOptions<ToolsOptions> opts)
        => name switch
        {
            "browser_click" => new BrowserClickTool(bridge, opts),
            "browser_type" => new BrowserTypeTool(bridge, opts),
            "browser_key" => new BrowserKeyTool(bridge, opts),
            "browser_scroll" => new BrowserScrollTool(bridge, opts),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };

    private static IReadOnlyList<ITool> CreateAllTools(IBrowserBridge bridge, IOptions<ToolsOptions> opts)
        => new ITool[]
        {
            new BrowserHealthTool(bridge, opts),
            new BrowserCaptureTabTool(bridge, opts),
            new BrowserClickTool(bridge, opts),
            new BrowserTypeTool(bridge, opts),
            new BrowserKeyTool(bridge, opts),
            new BrowserScrollTool(bridge, opts),
        };

    private sealed class RecordingBrowserBridge : IBrowserBridge
    {
        public string BackendName => "mock";
        public string CapturePath { get; set; } = "/tmp/mock-tab.png";
        public string CaptureDom { get; set; } = "<html><body>mock</body></html>";

        public int HealthCalls { get; private set; }
        public List<int> CaptureCalls { get; } = new();
        public List<(int X, int Y)> ClickCalls { get; } = new();
        public List<string> TypeCalls { get; } = new();
        public List<string> KeyCalls { get; } = new();
        public List<(int Dx, int Dy)> ScrollCalls { get; } = new();

        public int TotalCalls =>
            HealthCalls + CaptureCalls.Count + ClickCalls.Count
            + TypeCalls.Count + KeyCalls.Count + ScrollCalls.Count;

        public Task<BrowserBridgeResult> HealthAsync(CancellationToken ct = default)
        {
            HealthCalls++;
            return Task.FromResult(new BrowserBridgeResult(true, "browser bridge ok (mock)", null));
        }

        public Task<BrowserBridgeResult> CaptureTabAsync(int tab, CancellationToken ct = default)
        {
            CaptureCalls.Add(tab);
            var content = $"captured tab screenshot: {CapturePath}\n\nDOM:\n{CaptureDom}";
            return Task.FromResult(new BrowserBridgeResult(
                true,
                content,
                new { path = CapturePath, dom = CaptureDom }));
        }

        public Task<BrowserBridgeResult> ClickAsync(int x, int y, CancellationToken ct = default)
        {
            ClickCalls.Add((x, y));
            return Task.FromResult(new BrowserBridgeResult(true, $"clicked at ({x},{y})", null));
        }

        public Task<BrowserBridgeResult> TypeAsync(string text, CancellationToken ct = default)
        {
            TypeCalls.Add(text);
            return Task.FromResult(new BrowserBridgeResult(true, $"typed {text.Length} chars", null));
        }

        public Task<BrowserBridgeResult> KeyAsync(string key, CancellationToken ct = default)
        {
            KeyCalls.Add(key);
            return Task.FromResult(new BrowserBridgeResult(true, $"pressed key {key}", null));
        }

        public Task<BrowserBridgeResult> ScrollAsync(int dx, int dy, CancellationToken ct = default)
        {
            ScrollCalls.Add((dx, dy));
            return Task.FromResult(new BrowserBridgeResult(true, $"scrolled dx={dx} dy={dy}", null));
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

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            });
        }
    }
}
