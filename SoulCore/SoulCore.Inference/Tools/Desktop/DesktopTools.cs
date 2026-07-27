using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Shared constructor wiring for desktop tools (BED-135/144).
/// </summary>
public abstract class DesktopToolBase : ITool
{
    protected readonly IHermesMcpInvoker Hermes;
    protected readonly ToolsOptions Options;

    protected DesktopToolBase(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
    {
        Hermes = hermes ?? throw new ArgumentNullException(nameof(hermes));
        Options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public abstract ToolDefinition Definition { get; }

    public abstract Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default);

    protected ToolResult? RefuseIfCaptureDisabled()
    {
        if (Options.AllowDesktopCapture) return null;
        return new ToolResult(false, HermesToolRouting.DesktopCaptureDisabledMessage, null);
    }

    protected ToolResult? RefuseIfControlDisabled()
    {
        if (Options.AllowComputerControl) return null;
        return new ToolResult(false, HermesToolRouting.ComputerControlRequiredMessage, null);
    }

    protected Task<ToolResult> RouteComputerUseAsync(string action, JsonElement args, CancellationToken ct)
    {
        var mcpArgs = HermesToolRouting.MergeObject(args, new Dictionary<string, object?>
        {
            ["action"] = action
        });
        return HermesToolRouting.RouteAsync(
            Hermes,
            Options.DesktopBackend,
            "computer_use",
            mcpArgs,
            nativeFallback: null,
            ct);
    }

    protected Task<ToolResult> RouteNamedAsync(string mcpName, JsonElement args, CancellationToken ct) =>
        HermesToolRouting.RouteAsync(
            Hermes,
            Options.DesktopBackend,
            mcpName,
            args.ValueKind == JsonValueKind.Object ? args : HermesToolRouting.EmptyArgs(),
            nativeFallback: null,
            ct);

    protected static JsonElement Schema(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();
}

/// <summary>Capture the desktop screen via Hermes <c>computer_use</c> (action=screenshot).</summary>
public sealed class DesktopScreenshotTool : DesktopToolBase
{
    public DesktopScreenshotTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "desktop_screenshot",
        "Capture the desktop screen.",
        Schema("""{"type":"object","properties":{"monitor":{"type":"integer","default":0}}}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfCaptureDisabled();
        if (denied is not null) return denied;
        return await RouteComputerUseAsync("screenshot", args, ct).ConfigureAwait(false);
    }
}

/// <summary>Click at screen coordinates via Hermes <c>computer_use</c>.</summary>
public sealed class DesktopClickTool : DesktopToolBase
{
    public DesktopClickTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "desktop_click",
        "Click at screen coordinates.",
        Schema("""{"type":"object","properties":{"x":{"type":"integer"},"y":{"type":"integer"},"button":{"type":"string","default":"left"}},"required":["x","y"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfControlDisabled();
        if (denied is not null) return denied;
        return await RouteComputerUseAsync("click", args, ct).ConfigureAwait(false);
    }
}

/// <summary>Type text via Hermes <c>computer_use</c>.</summary>
public sealed class DesktopTypeTool : DesktopToolBase
{
    public DesktopTypeTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "desktop_type",
        "Type text at the current focus.",
        Schema("""{"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfControlDisabled();
        if (denied is not null) return denied;
        return await RouteComputerUseAsync("type", args, ct).ConfigureAwait(false);
    }
}

/// <summary>Press a key via Hermes <c>computer_use</c>.</summary>
public sealed class DesktopKeyTool : DesktopToolBase
{
    public DesktopKeyTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "desktop_key",
        "Press a key (e.g. Enter, Escape).",
        Schema("""{"type":"object","properties":{"key":{"type":"string"}},"required":["key"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfControlDisabled();
        if (denied is not null) return denied;
        return await RouteComputerUseAsync("key", args, ct).ConfigureAwait(false);
    }
}

/// <summary>List open desktop windows via Hermes MCP <c>list_desktop_windows</c>.</summary>
public sealed class ListDesktopWindowsTool : DesktopToolBase
{
    public ListDesktopWindowsTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "list_desktop_windows",
        "List open desktop windows.",
        Schema("""{"type":"object","properties":{}}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var denied = RefuseIfCaptureDisabled();
        if (denied is not null) return denied;
        return await RouteNamedAsync("list_desktop_windows", args, ct).ConfigureAwait(false);
    }
}

/// <summary>Focus a desktop window via Hermes MCP <c>focus_desktop_window</c>.</summary>
public sealed class FocusDesktopWindowTool : DesktopToolBase
{
    public FocusDesktopWindowTool(IHermesMcpInvoker hermes, IOptions<ToolsOptions> options)
        : base(hermes, options) { }

    public override ToolDefinition Definition { get; } = new(
        "focus_desktop_window",
        "Focus a desktop window by title or index.",
        Schema("""{"type":"object","properties":{"title":{"type":"string"}},"required":["title"]}"""));

    public override async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        // Focus is treated as capture-class (read/navigation) per TASK-135.
        var denied = RefuseIfCaptureDisabled();
        if (denied is not null) return denied;
        return await RouteNamedAsync("focus_desktop_window", args, ct).ConfigureAwait(false);
    }
}
