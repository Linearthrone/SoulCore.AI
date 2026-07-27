using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Shared wiring for browser_bridge_* tools (BED-136/144).
/// </summary>
public abstract class BrowserToolBase : ITool
{
    protected readonly IHermesMcpInvoker Hermes;
    protected readonly ToolsOptions Options;

    protected BrowserToolBase(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
    {
        Hermes = hermes ?? throw new ArgumentNullException(nameof(hermes));
        Options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public abstract ToolDefinition Definition { get; }

    public abstract Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default);

    protected ToolResult? RefuseIfCaptureDisabled()
    {
        if (Options.AllowBrowserCapture) return null;
        return new ToolResult(false, HermesToolRouting.BrowserCaptureDisabledMessage, null);
    }

    protected ToolResult? RefuseIfControlDisabled()
    {
        // Same session opt-in as desktop (TASK-136).
        if (Options.AllowComputerControl) return null;
        return new ToolResult(false, HermesToolRouting.ComputerControlRequiredMessage, null);
    }

    protected Task<ToolResult> RouteAsync(string mcpName, JsonElement args, CancellationToken ct) =>
        HermesToolRouting.RouteAsync(
            Hermes,
            Options.BrowserBackend,
            mcpName,
            args.ValueKind == JsonValueKind.Object ? args : HermesToolRouting.EmptyArgs(),
            nativeFallback: null,
            ct);

    protected static JsonElement Schema(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();
}

public sealed class BrowserHealthTool : BrowserToolBase
{
    public BrowserHealthTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "browser_health",
        "Check browser bridge status.",
        Schema("""{"type":"object","properties":{}}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfCaptureDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("browser_bridge_health", args, ct).ConfigureAwait(false);
    }
}

public sealed class BrowserCaptureTabTool : BrowserToolBase
{
    public BrowserCaptureTabTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "browser_capture_tab",
        "Capture the current browser tab (screenshot + DOM).",
        Schema("""{"type":"object","properties":{"tab":{"type":"integer","default":0}}}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfCaptureDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("browser_bridge_capture_tab", args, ct).ConfigureAwait(false);
    }
}

public sealed class BrowserClickTool : BrowserToolBase
{
    public BrowserClickTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "browser_click",
        "Click in the browser tab at coordinates.",
        Schema("""{"type":"object","properties":{"x":{"type":"integer"},"y":{"type":"integer"}},"required":["x","y"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfControlDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("browser_bridge_click", args, ct).ConfigureAwait(false);
    }
}

public sealed class BrowserTypeTool : BrowserToolBase
{
    public BrowserTypeTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "browser_type",
        "Type into the browser tab.",
        Schema("""{"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfControlDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("browser_bridge_type", args, ct).ConfigureAwait(false);
    }
}

public sealed class BrowserKeyTool : BrowserToolBase
{
    public BrowserKeyTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "browser_key",
        "Press a key in the browser tab.",
        Schema("""{"type":"object","properties":{"key":{"type":"string"}},"required":["key"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfControlDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("browser_bridge_key", args, ct).ConfigureAwait(false);
    }
}

public sealed class BrowserScrollTool : BrowserToolBase
{
    public BrowserScrollTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "browser_scroll",
        "Scroll the browser tab.",
        Schema("""{"type":"object","properties":{"dx":{"type":"integer","default":0},"dy":{"type":"integer","default":0}}}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfControlDisabled();
        if (denied is not null) return denied;
        return await RouteAsync("browser_bridge_scroll", args, ct).ConfigureAwait(false);
    }
}
