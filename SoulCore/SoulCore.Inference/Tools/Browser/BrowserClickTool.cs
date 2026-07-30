using System.Text.Json;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// <c>browser_click</c> — click at coordinates in the browser tab.
/// Write/control; gated by computer-control session opt-in.
/// </summary>
public sealed class BrowserClickTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"x":{"type":"integer","description":"X coordinate."},"y":{"type":"integer","description":"Y coordinate."}},"required":["x","y"]}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly IToolsAccessSettings _access;

    public BrowserClickTool(IBrowserBridge bridge, IToolsAccessSettings access)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _access = access ?? throw new ArgumentNullException(nameof(access));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_click",
        Description: "Click in the browser tab at coordinates.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_access))
            return new ToolResult(false, BrowserToolGate.ControlDenied, null);

        if (args.ValueKind != JsonValueKind.Object)
            return new ToolResult(false, "error: browser_click expects a JSON object with 'x' and 'y'.", null);

        if (!TryGetInt(args, "x", out var x) || !TryGetInt(args, "y", out var y))
            return new ToolResult(false, "error: browser_click requires 'x' and 'y' (integers).", null);

        var result = await _bridge.ClickAsync(x, y, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Content, result.Data);
    }

    private static bool TryGetInt(JsonElement args, string name, out int value)
    {
        value = 0;
        if (!args.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Number)
            return false;
        return prop.TryGetInt32(out value);
    }
}
