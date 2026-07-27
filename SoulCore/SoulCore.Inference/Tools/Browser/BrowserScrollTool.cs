using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// <c>browser_scroll</c> — scroll the browser tab.
/// Write/control; gated by <see cref="ToolsOptions.AllowComputerControl"/>.
/// </summary>
public sealed class BrowserScrollTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"dx":{"type":"integer","default":0,"description":"Horizontal scroll delta."},"dy":{"type":"integer","default":0,"description":"Vertical scroll delta."}}}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly ToolsOptions _options;

    public BrowserScrollTool(IBrowserBridge bridge, IOptions<ToolsOptions> options)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_scroll",
        Description: "Scroll the browser tab.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_options))
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
