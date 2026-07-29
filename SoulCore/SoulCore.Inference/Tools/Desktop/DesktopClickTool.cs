using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary><c>desktop_click</c> — gated by <see cref="ToolsOptions.AllowComputerControl"/>.</summary>
public sealed class DesktopClickTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"x":{"type":"integer","description":"Screen X coordinate."},"y":{"type":"integer","description":"Screen Y coordinate."},"button":{"type":"string","description":"left|right|middle","default":"left"}},"required":["x","y"]}""")
        .RootElement.Clone();

    private readonly IOptions<ToolsOptions> _options;
    private readonly IDesktopControlBackend _backend;

    public DesktopClickTool(IOptions<ToolsOptions> options, IDesktopControlBackend backend)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_click",
        Description: "Click at screen coordinates.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!DesktopToolGate.IsControlAllowed(_options.Value))
            return new ToolResult(false, DesktopToolGate.ControlDeniedMessage, null);

        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("x", out var xProp) || xProp.ValueKind != JsonValueKind.Number || !xProp.TryGetInt32(out var x)
            || !args.TryGetProperty("y", out var yProp) || yProp.ValueKind != JsonValueKind.Number || !yProp.TryGetInt32(out var y))
        {
            return new ToolResult(false, "desktop_click requires integer 'x' and 'y'", null);
        }

        var button = "left";
        if (args.TryGetProperty("button", out var b) && b.ValueKind == JsonValueKind.String)
            button = b.GetString() ?? "left";

        var result = await _backend.ClickAsync(x, y, button, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Message, result.Data);
    }
}
