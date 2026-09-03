using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference;
using SoulCore.Inference.Tools.Browser;
using SoulCore.Inference.Tools.Desktop;

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
        var access = new ComputerControlGate(
            allowDesktopCapture: true,
            allowBrowserCapture: true,
            allowComputerControl: false,
            allowMt4Read: false,
            allowMt4Trade: false);

        var tool = CreateControlTool(toolName, bridge, access);
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
            new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: false));

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
            new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true));

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
            new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true));

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
            new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true));

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
            new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true));

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
            new ComputerControlGate(allowDesktopCapture: true, allowBrowserCapture: true, allowComputerControl: false, allowMt4Read: false, allowMt4Trade: false));

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
            new ComputerControlGate(allowDesktopCapture: true, allowBrowserCapture: false, allowComputerControl: false, allowMt4Read: false, allowMt4Trade: false));

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
            new ComputerControlGate(allowDesktopCapture: true, allowBrowserCapture: true, allowComputerControl: false, allowMt4Read: false, allowMt4Trade: false));

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
            new ComputerControlGate(allowDesktopCapture: true, allowBrowserCapture: false, allowComputerControl: false, allowMt4Read: false, allowMt4Trade: false));

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
            new ComputerControlGate(
                allowDesktopCapture: true,
                allowBrowserCapture: true,
                allowComputerControl: false,
                allowMt4Read: false,
                allowMt4Trade: false));

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
        var access = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: false);
        var tools = CreateAllTools(bridge, access);

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
        var access = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: false);
        var services = new ServiceCollection();
        foreach (var tool in CreateAllTools(bridge, access))
            services.AddSingleton<ITool>(tool);
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        var registry = services.BuildServiceProvider().GetRequiredService<IToolRegistry>();
        var names = registry.GetDefinitions().Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var expected in BrowserToolNames)
            Assert.Contains(expected, names);
    }

    [Fact]
    public async Task UnsupportedBrowserBridge_ReturnsClearError()
    {
        var bridge = new UnsupportedBrowserBridge("foobar");
        var result = await bridge.HealthAsync();

        Assert.False(result.Success);
        Assert.Contains("foobar", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("native", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NativeBrowserBridge_HealthOk_WhenBridgeResponds()
    {
        var handler = new FixedJsonHandler(
            """{"ok":true,"service":"hv-browser-capture-bridge","pending_jobs":0}""");
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:17891/") };
        var bridge = new NativeBrowserBridge(http, toolsOptions: null);

        var result = await bridge.HealthAsync();

        Assert.True(result.Success);
        Assert.Contains("browser bridge ok", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("GET", handler.LastMethod);
        Assert.Contains("/health", handler.LastPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NativeBrowserBridge_CaptureTab_FormatsPathAndPageMap()
    {
        var handler = new FixedJsonHandler(
            """{"ok":true,"url":"https://example.com","title":"Example","screenshot_path":"C:\\tmp\\tab.png","page_map":{"elements":[{"index":1,"tag":"a","text":"Hi"}]}}""");
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:17891/") };
        var bridge = new NativeBrowserBridge(http, toolsOptions: null);

        var result = await bridge.CaptureTabAsync(0);

        Assert.True(result.Success);
        Assert.Contains("captured tab screenshot:", result.Content, StringComparison.Ordinal);
        Assert.Contains("C:\\tmp\\tab.png", result.Content, StringComparison.Ordinal);
        Assert.Contains("page_map", result.Content, StringComparison.Ordinal);
        Assert.Equal("POST", handler.LastMethod);
        Assert.Contains("/capture", handler.LastPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NativeBrowserBridge_Click_PostsAction()
    {
        var handler = new FixedJsonHandler(
            """{"ok":true,"detail":"clicked","url":"https://example.com","title":"Example"}""");
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:17891/") };
        var bridge = new NativeBrowserBridge(http, toolsOptions: null);

        var result = await bridge.ClickAsync(10, 20);

        Assert.True(result.Success);
        Assert.Equal("POST", handler.LastMethod);
        Assert.Contains("/action", handler.LastPath, StringComparison.Ordinal);
        Assert.Contains("\"action\":\"click\"", handler.LastBody ?? "", StringComparison.Ordinal);
        Assert.Contains("\"x\":10", handler.LastBody ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task NativeBrowserBridge_WhenUnreachable_ReturnsUnavailable()
    {
        var handler = new FailingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:17891/") };
        var bridge = new NativeBrowserBridge(http, toolsOptions: null);

        var result = await bridge.HealthAsync();

        Assert.False(result.Success);
        Assert.Contains("unavailable", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FixedJsonHandler : HttpMessageHandler
    {
        private readonly string _json;
        public string LastMethod { get; private set; } = "";
        public string LastPath { get; private set; } = "";
        public string? LastBody { get; private set; }

        public FixedJsonHandler(string json) => _json = json;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method.Method;
            LastPath = request.RequestUri?.AbsolutePath ?? "";
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection refused");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static ITool CreateControlTool(string name, IBrowserBridge bridge, IToolsAccessSettings access)
        => name switch
        {
            "browser_click" => new BrowserClickTool(bridge, access),
            "browser_type" => new BrowserTypeTool(bridge, access),
            "browser_key" => new BrowserKeyTool(bridge, access),
            "browser_scroll" => new BrowserScrollTool(bridge, access),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };

    private static IReadOnlyList<ITool> CreateAllTools(IBrowserBridge bridge, IToolsAccessSettings access)
        => new ITool[]
        {
            new BrowserHealthTool(bridge, access),
            new BrowserCaptureTabTool(bridge, access),
            new BrowserClickTool(bridge, access),
            new BrowserTypeTool(bridge, access),
            new BrowserKeyTool(bridge, access),
            new BrowserScrollTool(bridge, access),
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
