using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary><c>browser_click</c> — gated by <see cref="ToolsOptions.AllowComputerControl"/>.</summary>
public sealed class BrowserClickTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"x":{"type":"integer","description":"Viewport X coordinate."},"y":{"type":"integer","description":"Viewport Y coordinate."}},"required":["x","y"]}""")
        .RootElement.Clone();

    private readonly IOptions<ToolsOptions> _options;
    private readonly IBrowserControlBackend _backend;

    public BrowserClickTool(IOptions<ToolsOptions> options, IBrowserControlBackend backend)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_click",
        Description: "Click in the browser tab at coordinates.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_options.Value))
            return new ToolResult(false, BrowserToolGate.ControlDeniedMessage, null);

        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("x", out var xProp) || xProp.ValueKind != JsonValueKind.Number || !xProp.TryGetInt32(out var x)
            || !args.TryGetProperty("y", out var yProp) || yProp.ValueKind != JsonValueKind.Number || !yProp.TryGetInt32(out var y))
        {
            return new ToolResult(false, "browser_click requires integer 'x' and 'y'", null);
        }

        var result = await _backend.ClickAsync(x, y, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Message, result.Data);
    }
}
