using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// <c>browser_click</c> — click at coordinates in the browser tab.
/// Write/control; gated by <see cref="ToolsOptions.AllowComputerControl"/>.
/// </summary>
public sealed class BrowserClickTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"x":{"type":"integer","description":"X coordinate."},"y":{"type":"integer","description":"Y coordinate."}},"required":["x","y"]}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly ToolsOptions _options;

    public BrowserClickTool(IBrowserBridge bridge, IOptions<ToolsOptions> options)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_click",
        Description: "Click in the browser tab at coordinates.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_options))
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
