using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference;
using SoulCore.Inference.Tools.Browser;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// BED-136 browser tools: gate closed refuses control (no backend call);
/// gate open dispatches to mock backend; capture gate for health/capture_tab.
/// </summary>
public class BrowserToolsTests
{
    private const string AuthMessage =
        "browser control requires user authorization — ask the user to enable AllowComputerControl";

    private const string CaptureDenied =
        "browser capture requires AllowBrowserCapture=true";

    // ─────────────────────────────────────────────────────────────────────
    // Gate closed — control tools must NOT touch the backend
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("browser_click", """{"x":10,"y":20}""")]
    [InlineData("browser_type", """{"text":"hi"}""")]
    [InlineData("browser_key", """{"key":"Enter"}""")]
    [InlineData("browser_scroll", """{"dx":0,"dy":100}""")]
    public async Task ControlTools_GateClosed_RefuseAndDoNotInvokeBackend(string toolName, string argsJson)
    {
        var backend = new RecordingBrowserBackend();
        var opts = Options.Create(new ToolsOptions
        {
            AllowBrowserCapture = true,
            AllowComputerControl = false,
            BrowserBackend = "native",
        });

        var tool = CreateControlTool(toolName, opts, backend);
        var args = JsonDocument.Parse(argsJson).RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Equal(AuthMessage, result.Content);
        Assert.Empty(backend.Calls);
    }

    [Fact]
    public async Task BrowserClick_GateClosed_NeverInjectsInput()
    {
        var backend = new RecordingBrowserBackend();
        var tool = new BrowserClickTool(
            Options.Create(new ToolsOptions { AllowComputerControl = false }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"x":100,"y":100}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal(AuthMessage, result.Content);
        Assert.Empty(backend.Calls);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Gate open — control tools dispatch to backend
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BrowserClick_GateOpen_DispatchesToBackend()
    {
        var backend = new RecordingBrowserBackend();
        var tool = new BrowserClickTool(
            Options.Create(new ToolsOptions { AllowComputerControl = true }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"x":11,"y":22}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Contains("clicked", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "click:11,22" }, backend.Calls);
    }

    [Fact]
    public async Task BrowserType_GateOpen_DispatchesToBackend()
    {
        var backend = new RecordingBrowserBackend();
        var tool = new BrowserTypeTool(
            Options.Create(new ToolsOptions { AllowComputerControl = true }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"text":"hello"}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Equal(new[] { "type:hello" }, backend.Calls);
    }

    [Fact]
    public async Task BrowserKey_GateOpen_DispatchesToBackend()
    {
        var backend = new RecordingBrowserBackend();
        var tool = new BrowserKeyTool(
            Options.Create(new ToolsOptions { AllowComputerControl = true }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"key":"Escape"}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Equal(new[] { "key:Escape" }, backend.Calls);
    }

    [Fact]
    public async Task BrowserScroll_GateOpen_DispatchesToBackend()
    {
        var backend = new RecordingBrowserBackend();
        var tool = new BrowserScrollTool(
            Options.Create(new ToolsOptions { AllowComputerControl = true }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"dx":5,"dy":-40}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Equal(new[] { "scroll:5,-40" }, backend.Calls);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Capture gate
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BrowserHealth_CaptureAllowed_Dispatches()
    {
        var backend = new RecordingBrowserBackend();
        var tool = new BrowserHealthTool(
            Options.Create(new ToolsOptions { AllowBrowserCapture = true }),
            backend);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Equal(new[] { "health" }, backend.Calls);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task BrowserHealth_CaptureDenied_RefusesWithoutBackend()
    {
        var backend = new RecordingBrowserBackend();
        var tool = new BrowserHealthTool(
            Options.Create(new ToolsOptions { AllowBrowserCapture = false }),
            backend);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal(CaptureDenied, result.Content);
        Assert.Empty(backend.Calls);
    }

    [Fact]
    public async Task BrowserCaptureTab_CaptureAllowed_ReturnsPathAndDom()
    {
        var backend = new RecordingBrowserBackend();
        var tool = new BrowserCaptureTabTool(
            Options.Create(new ToolsOptions { AllowBrowserCapture = true }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"tab":0}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Contains("captured", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "capture:0" }, backend.Calls);
        Assert.NotNull(result.Data);
        var json = JsonSerializer.Serialize(result.Data);
        Assert.Contains("/tmp/fake-tab.png", json, StringComparison.Ordinal);
        Assert.Contains("dom", json, StringComparison.Ordinal);
        // System.Text.Json escapes '<' as \u003C in default serialization.
        Assert.True(
            json.Contains("<html>", StringComparison.Ordinal)
            || json.Contains("\\u003Chtml\\u003E", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BrowserCaptureTab_CaptureDenied_RefusesWithoutBackend()
    {
        var backend = new RecordingBrowserBackend();
        var tool = new BrowserCaptureTabTool(
            Options.Create(new ToolsOptions { AllowBrowserCapture = false }),
            backend);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal(CaptureDenied, result.Content);
        Assert.Empty(backend.Calls);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Defaults + definitions + DI registration
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToolsOptions_Defaults_ControlClosedCaptureOpen_NativeBackend()
    {
        var opts = new ToolsOptions();
        Assert.False(opts.AllowComputerControl);
        Assert.True(opts.AllowBrowserCapture);
        Assert.Equal("native", opts.BrowserBackend);
        Assert.Equal("http://127.0.0.1:9222", opts.BrowserCdpUrl);
    }

    [Fact]
    public void AllSixTools_Definitions_MatchContractNames()
    {
        var opts = Options.Create(new ToolsOptions());
        var backend = new RecordingBrowserBackend();
        ITool[] tools =
        [
            new BrowserHealthTool(opts, backend),
            new BrowserCaptureTabTool(opts, backend),
            new BrowserClickTool(opts, backend),
            new BrowserTypeTool(opts, backend),
            new BrowserKeyTool(opts, backend),
            new BrowserScrollTool(opts, backend),
        ];

        var names = tools.Select(t => t.Definition.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(
            new[]
            {
                "browser_capture_tab",
                "browser_click",
                "browser_health",
                "browser_key",
                "browser_scroll",
                "browser_type",
            },
            names);
    }

    [Fact]
    public void HostStyleDi_RegistersSixBrowserToolsInRegistry()
    {
        var services = new ServiceCollection();
        services.Configure<ToolsOptions>(o =>
        {
            o.AllowBrowserCapture = true;
            o.AllowComputerControl = false;
            o.BrowserBackend = "native";
        });
        services.AddSingleton<NativeBrowserControlBackend>();
        services.AddSingleton<HermesBrowserControlBackend>();
        services.AddSingleton<IBrowserControlBackend, BrowserBackendSelector>();
        services.AddSingleton<ITool, BrowserHealthTool>();
        services.AddSingleton<ITool, BrowserCaptureTabTool>();
        services.AddSingleton<ITool, BrowserClickTool>();
        services.AddSingleton<ITool, BrowserTypeTool>();
        services.AddSingleton<ITool, BrowserKeyTool>();
        services.AddSingleton<ITool, BrowserScrollTool>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        var defs = registry.GetDefinitions().Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("browser_health", defs);
        Assert.Contains("browser_capture_tab", defs);
        Assert.Contains("browser_click", defs);
        Assert.Contains("browser_type", defs);
        Assert.Contains("browser_key", defs);
        Assert.Contains("browser_scroll", defs);
    }

    [Fact]
    public async Task HermesBackend_ReturnsUnavailableStretchMessage()
    {
        var hermes = new HermesBrowserControlBackend();
        var capture = await hermes.CaptureTabAsync(0);
        Assert.False(capture.Success);
        Assert.Contains("OPS-143", capture.Message, StringComparison.Ordinal);
        Assert.Contains("browser_bridge", capture.Message, StringComparison.Ordinal);

        var health = await hermes.HealthAsync();
        Assert.True(health.Success);
        Assert.Contains("hermes", health.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NativeBackend_WithoutCdp_HealthReportsDisconnected_CaptureFailsHonestly()
    {
        var opts = Options.Create(new ToolsOptions
        {
            BrowserBackend = "native",
            BrowserCdpUrl = "http://127.0.0.1:1", // nothing listening
        });
        var backend = new NativeBrowserControlBackend(
            opts,
            Path.Combine(Path.GetTempPath(), "soulcore-browser-tests"));

        var health = await backend.HealthAsync();
        Assert.True(health.Success);
        Assert.Contains("native", health.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not connected", health.Message, StringComparison.OrdinalIgnoreCase);

        var capture = await backend.CaptureTabAsync(0);
        Assert.False(capture.Success);
        Assert.False(string.IsNullOrWhiteSpace(capture.Message));
    }

    [Fact]
    public async Task NativeBackend_RejectsNonLoopbackCdpUrl()
    {
        var opts = Options.Create(new ToolsOptions
        {
            BrowserBackend = "native",
            BrowserCdpUrl = "http://example.com:9222",
        });
        var backend = new NativeBrowserControlBackend(
            opts,
            Path.Combine(Path.GetTempPath(), "soulcore-browser-tests"));

        var health = await backend.HealthAsync();
        Assert.True(health.Success);
        Assert.Contains("invalid", health.Message, StringComparison.OrdinalIgnoreCase);

        var capture = await backend.CaptureTabAsync(0);
        Assert.False(capture.Success);
        Assert.Contains("loopback", capture.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ITool CreateControlTool(string name, IOptions<ToolsOptions> opts, IBrowserControlBackend backend)
        => name switch
        {
            "browser_click" => new BrowserClickTool(opts, backend),
            "browser_type" => new BrowserTypeTool(opts, backend),
            "browser_key" => new BrowserKeyTool(opts, backend),
            "browser_scroll" => new BrowserScrollTool(opts, backend),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    /// <summary>Mock backend — records calls; never talks to a real browser.</summary>
    private sealed class RecordingBrowserBackend : IBrowserControlBackend
    {
        public List<string> Calls { get; } = new();

        public Task<BrowserBackendResult> HealthAsync(CancellationToken ct = default)
        {
            Calls.Add("health");
            return Task.FromResult(new BrowserBackendResult(
                true,
                "browser backend=mock; connected",
                new { backend = "mock", connected = true }));
        }

        public Task<BrowserBackendResult> CaptureTabAsync(int tab, CancellationToken ct = default)
        {
            Calls.Add($"capture:{tab}");
            return Task.FromResult(new BrowserBackendResult(
                true,
                $"captured tab {tab} → /tmp/fake-tab.png",
                new { path = "/tmp/fake-tab.png", tab, dom = "<html><body>hi</body></html>", bytes = 12 }));
        }

        public Task<BrowserBackendResult> ClickAsync(int x, int y, CancellationToken ct = default)
        {
            Calls.Add($"click:{x},{y}");
            return Task.FromResult(new BrowserBackendResult(true, $"clicked at ({x},{y})", null));
        }

        public Task<BrowserBackendResult> TypeAsync(string text, CancellationToken ct = default)
        {
            Calls.Add($"type:{text}");
            return Task.FromResult(new BrowserBackendResult(true, $"typed {text.Length} chars", null));
        }

        public Task<BrowserBackendResult> KeyAsync(string key, CancellationToken ct = default)
        {
            Calls.Add($"key:{key}");
            return Task.FromResult(new BrowserBackendResult(true, $"pressed key '{key}'", null));
        }

        public Task<BrowserBackendResult> ScrollAsync(int dx, int dy, CancellationToken ct = default)
        {
            Calls.Add($"scroll:{dx},{dy}");
            return Task.FromResult(new BrowserBackendResult(true, $"scrolled dx={dx} dy={dy}", null));
        }
    }
}
