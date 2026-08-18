using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// <c>desktop_click</c> — requires session <see cref="IComputerControlGate.AllowComputerControl"/>.
/// Optional <c>clicks: 2</c> for double-click (BED-174).
/// </summary>
public sealed class DesktopClickTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "x": { "type": "integer", "description": "Screen X coordinate." },
            "y": { "type": "integer", "description": "Screen Y coordinate." },
            "button": {
              "type": "string",
              "description": "Mouse button: left, right, or middle.",
              "default": "left"
            },
            "clicks": {
              "type": "integer",
              "description": "Number of clicks: 1 (default) or 2 for double-click.",
              "default": 1
            }
          },
          "required": ["x", "y"]
        }
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IDesktopControlBackend _backend;

    public DesktopClickTool(IComputerControlGate gate, IDesktopControlBackend backend)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_click",
        Description:
            "Click at guest framebuffer coordinates (Ubuntu VM pixels, origin 0,0). " +
            "Optional clicks:2 for double-click. " +
            "REQUIRED: call desktop_screenshot first and read x,y from the attached image — " +
            "never guess window-center for in-page buttons. " +
            "Prefer browser_click_text for labeled controls (Login, Sign in). " +
            "Requires AllowComputerControl.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowComputerControl)
            return DesktopToolGate.RefuseControl();

        if (args.ValueKind != JsonValueKind.Object)
            return new ToolResult(false, "error: desktop_click expects a JSON object with x,y.", null);

        if (!TryGetInt(args, "x", out var x) || !TryGetInt(args, "y", out var y))
            return new ToolResult(false, "error: desktop_click requires integer 'x' and 'y'.", null);

        var button = "left";
        if (args.TryGetProperty("button", out var b) && b.ValueKind == JsonValueKind.String)
        {
            var s = b.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                button = s;
        }

        var clicks = 1;
        if (args.TryGetProperty("clicks", out var c) && c.ValueKind == JsonValueKind.Number
            && c.TryGetInt32(out var cVal))
        {
            clicks = cVal;
        }

        if (clicks is not (1 or 2))
            return new ToolResult(false, "error: desktop_click 'clicks' must be 1 or 2.", null);

        var result = await _backend.ClickAsync(x, y, button, clicks, ct).ConfigureAwait(false);
        return DesktopToolGate.FromBackend(result);
    }

    private static bool TryGetInt(JsonElement args, string name, out int value)
    {
        value = 0;
        return args.TryGetProperty(name, out var p)
            && p.ValueKind == JsonValueKind.Number
            && p.TryGetInt32(out value);
    }
}
