using System.Text.Json;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// <c>browser_scroll</c> — scroll the browser tab.
/// Write/control; gated by computer-control session opt-in.
/// </summary>
public sealed class BrowserScrollTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"dx":{"type":"integer","default":0,"description":"Horizontal scroll delta."},"dy":{"type":"integer","default":0,"description":"Vertical scroll delta."}}}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly IToolsAccessSettings _access;

    public BrowserScrollTool(IBrowserBridge bridge, IToolsAccessSettings access)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _access = access ?? throw new ArgumentNullException(nameof(access));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_scroll",
        Description: "Scroll the browser tab.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_access))
            return new ToolResult(false, BrowserToolGate.ControlDenied, null);

        var dx = 0;
        var dy = 0;
        if (args.ValueKind == JsonValueKind.Object)
        {
            if (args.TryGetProperty("dx", out var dxProp)
                && dxProp.ValueKind == JsonValueKind.Number
                && dxProp.TryGetInt32(out var dxVal))
            {
                dx = dxVal;
            }

            if (args.TryGetProperty("dy", out var dyProp)
                && dyProp.ValueKind == JsonValueKind.Number
                && dyProp.TryGetInt32(out var dyVal))
            {
                dy = dyVal;
            }
        }

        var result = await _bridge.ScrollAsync(dx, dy, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}
