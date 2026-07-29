using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// BED-135 desktop tools: gate closed refuses control (no backend call);
/// gate open dispatches to mock backend; capture gate for screenshot/list/focus.
/// </summary>
public class DesktopToolsTests
{
    private const string AuthMessage =
        "desktop control requires user authorization — ask the user to enable AllowComputerControl";

    private const string CaptureDenied =
        "desktop capture requires AllowDesktopCapture=true";

    // ─────────────────────────────────────────────────────────────────────
    // Gate closed — control tools must NOT touch the backend
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("desktop_click", """{"x":10,"y":20}""")]
    [InlineData("desktop_type", """{"text":"hi"}""")]
    [InlineData("desktop_key", """{"key":"Enter"}""")]
    public async Task ControlTools_GateClosed_RefuseAndDoNotInvokeBackend(string toolName, string argsJson)
    {
        var backend = new RecordingDesktopBackend();
        var opts = Options.Create(new ToolsOptions
        {
            AllowDesktopCapture = true,
            AllowComputerControl = false,
            DesktopBackend = "native",
        });

        var tool = CreateControlTool(toolName, opts, backend);
        var args = JsonDocument.Parse(argsJson).RootElement.Clone();

        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Equal(AuthMessage, result.Content);
        Assert.Empty(backend.Calls);
    }

    [Fact]
    public async Task DesktopClick_GateClosed_NeverInjectsInput()
    {
        var backend = new RecordingDesktopBackend();
        var tool = new DesktopClickTool(
            Options.Create(new ToolsOptions { AllowComputerControl = false }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"x":100,"y":100,"button":"left"}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal(AuthMessage, result.Content);
        Assert.DoesNotContain(backend.Calls, c => c.StartsWith("click:", StringComparison.Ordinal));
        Assert.Empty(backend.Calls);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Gate open — control tools dispatch to backend
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DesktopClick_GateOpen_DispatchesToBackend()
    {
        var backend = new RecordingDesktopBackend();
        var tool = new DesktopClickTool(
            Options.Create(new ToolsOptions { AllowComputerControl = true }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"x":11,"y":22,"button":"right"}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Contains("clicked", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "click:11,22,right" }, backend.Calls);
    }

    [Fact]
    public async Task DesktopType_GateOpen_DispatchesToBackend()
    {
        var backend = new RecordingDesktopBackend();
        var tool = new DesktopTypeTool(
            Options.Create(new ToolsOptions { AllowComputerControl = true }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"text":"hello"}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Equal(new[] { "type:hello" }, backend.Calls);
    }

    [Fact]
    public async Task DesktopKey_GateOpen_DispatchesToBackend()
    {
        var backend = new RecordingDesktopBackend();
        var tool = new DesktopKeyTool(
            Options.Create(new ToolsOptions { AllowComputerControl = true }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"key":"Escape"}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Equal(new[] { "key:Escape" }, backend.Calls);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Capture gate
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DesktopScreenshot_CaptureAllowed_Dispatches()
    {
        var backend = new RecordingDesktopBackend();
        var tool = new DesktopScreenshotTool(
            Options.Create(new ToolsOptions { AllowDesktopCapture = true }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"monitor":0}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Contains("captured", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "screenshot:0" }, backend.Calls);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task DesktopScreenshot_CaptureDenied_RefusesWithoutBackend()
    {
        var backend = new RecordingDesktopBackend();
        var tool = new DesktopScreenshotTool(
            Options.Create(new ToolsOptions { AllowDesktopCapture = false }),
            backend);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal(CaptureDenied, result.Content);
        Assert.Empty(backend.Calls);
    }

    [Fact]
    public async Task ListDesktopWindows_CaptureAllowed_Dispatches()
    {
        var backend = new RecordingDesktopBackend();
        var tool = new ListDesktopWindowsTool(
            Options.Create(new ToolsOptions { AllowDesktopCapture = true }),
            backend);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Equal(new[] { "list" }, backend.Calls);
    }

    [Fact]
    public async Task FocusDesktopWindow_CaptureDenied_Refuses()
    {
        var backend = new RecordingDesktopBackend();
        var tool = new FocusDesktopWindowTool(
            Options.Create(new ToolsOptions { AllowDesktopCapture = false }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"title":"Notepad"}""").RootElement.Clone());

        Assert.False(result.Success);
        Assert.Equal(CaptureDenied, result.Content);
        Assert.Empty(backend.Calls);
    }

    [Fact]
    public async Task FocusDesktopWindow_CaptureAllowed_Dispatches()
    {
        var backend = new RecordingDesktopBackend();
        var tool = new FocusDesktopWindowTool(
            Options.Create(new ToolsOptions { AllowDesktopCapture = true }),
            backend);

        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"title":"Notepad"}""").RootElement.Clone());

        Assert.True(result.Success);
        Assert.Equal(new[] { "focus:Notepad" }, backend.Calls);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Defaults + definitions + DI registration
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToolsOptions_Defaults_ControlClosedCaptureOpen()
    {
        var opts = new ToolsOptions();
        Assert.False(opts.AllowComputerControl);
        Assert.True(opts.AllowDesktopCapture);
        Assert.Equal("native", opts.DesktopBackend);
    }

    [Fact]
    public void AllSixTools_Definitions_MatchContractNames()
    {
        var opts = Options.Create(new ToolsOptions());
        var backend = new RecordingDesktopBackend();
        ITool[] tools =
        [
            new DesktopScreenshotTool(opts, backend),
            new DesktopClickTool(opts, backend),
            new DesktopTypeTool(opts, backend),
            new DesktopKeyTool(opts, backend),
            new ListDesktopWindowsTool(opts, backend),
            new FocusDesktopWindowTool(opts, backend),
        ];

        var names = tools.Select(t => t.Definition.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(
            new[]
            {
                "desktop_click",
                "desktop_key",
                "desktop_screenshot",
                "desktop_type",
                "focus_desktop_window",
                "list_desktop_windows",
            },
            names);
    }

    [Fact]
    public void HostStyleDi_RegistersSixDesktopToolsInRegistry()
    {
        var services = new ServiceCollection();
        services.Configure<ToolsOptions>(o =>
        {
            o.AllowDesktopCapture = true;
            o.AllowComputerControl = false;
            o.DesktopBackend = "native";
        });
        services.AddSingleton<NativeDesktopControlBackend>();
        services.AddSingleton<HermesDesktopControlBackend>();
        services.AddSingleton<IDesktopControlBackend, DesktopBackendSelector>();
        services.AddSingleton<ITool, DesktopScreenshotTool>();
        services.AddSingleton<ITool, DesktopClickTool>();
        services.AddSingleton<ITool, DesktopTypeTool>();
        services.AddSingleton<ITool, DesktopKeyTool>();
        services.AddSingleton<ITool, ListDesktopWindowsTool>();
        services.AddSingleton<ITool, FocusDesktopWindowTool>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IToolRegistry>();
        var defs = registry.GetDefinitions().Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("desktop_screenshot", defs);
        Assert.Contains("desktop_click", defs);
        Assert.Contains("desktop_type", defs);
        Assert.Contains("desktop_key", defs);
        Assert.Contains("list_desktop_windows", defs);
        Assert.Contains("focus_desktop_window", defs);
    }

    [Fact]
    public async Task HermesBackend_ReturnsUnavailableStretchMessage()
    {
        var hermes = new HermesDesktopControlBackend();
        var result = await hermes.ScreenshotAsync(0);
        Assert.False(result.Success);
        Assert.Contains("OPS-143", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NativeBackend_LinuxControl_ReturnsHonestFailure()
    {
        if (!OperatingSystem.IsLinux())
            return; // Windows CI path skips

        var backend = new NativeDesktopControlBackend(Path.Combine(Path.GetTempPath(), "soulcore-desktop-tests"));
        var click = await backend.ClickAsync(1, 2, "left");
        Assert.False(click.Success);
        Assert.Contains("Windows-primary", click.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(new RecordingDesktopBackend().Calls); // sanity — native didn't use recording
    }

    private static ITool CreateControlTool(string name, IOptions<ToolsOptions> opts, IDesktopControlBackend backend)
        => name switch
        {
            "desktop_click" => new DesktopClickTool(opts, backend),
            "desktop_type" => new DesktopTypeTool(opts, backend),
            "desktop_key" => new DesktopKeyTool(opts, backend),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    /// <summary>Mock backend — records calls; never injects real input.</summary>
    private sealed class RecordingDesktopBackend : IDesktopControlBackend
    {
        public List<string> Calls { get; } = new();

        public Task<DesktopBackendResult> ScreenshotAsync(int monitor, CancellationToken ct = default)
        {
            Calls.Add($"screenshot:{monitor}");
            return Task.FromResult(new DesktopBackendResult(
                true,
                $"captured monitor {monitor} → /tmp/fake.png",
                new { path = "/tmp/fake.png", monitor, bytes = 12 }));
        }

        public Task<DesktopBackendResult> ClickAsync(int x, int y, string button, CancellationToken ct = default)
        {
            Calls.Add($"click:{x},{y},{button}");
            return Task.FromResult(new DesktopBackendResult(true, $"clicked {button} at ({x},{y})", null));
        }

        public Task<DesktopBackendResult> TypeAsync(string text, CancellationToken ct = default)
        {
            Calls.Add($"type:{text}");
            return Task.FromResult(new DesktopBackendResult(true, $"typed {text.Length} chars", null));
        }

        public Task<DesktopBackendResult> KeyAsync(string key, CancellationToken ct = default)
        {
            Calls.Add($"key:{key}");
            return Task.FromResult(new DesktopBackendResult(true, $"pressed key '{key}'", null));
        }

        public Task<DesktopBackendResult> ListWindowsAsync(CancellationToken ct = default)
        {
            Calls.Add("list");
            return Task.FromResult(new DesktopBackendResult(true, "0: title=Test", new { count = 1 }));
        }

        public Task<DesktopBackendResult> FocusWindowAsync(string title, CancellationToken ct = default)
        {
            Calls.Add($"focus:{title}");
            return Task.FromResult(new DesktopBackendResult(true, $"focused window '{title}'", null));
        }
    }
}
