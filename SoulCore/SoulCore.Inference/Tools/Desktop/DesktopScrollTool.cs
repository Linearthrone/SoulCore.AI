using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// <c>desktop_scroll</c> — mouse-wheel scroll at a screen point (BED-174).
/// Requires session <see cref="IComputerControlGate.AllowComputerControl"/>.
/// </summary>
public sealed class DesktopScrollTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "x": { "type": "integer", "description": "Screen X coordinate." },
            "y": { "type": "integer", "description": "Screen Y coordinate." },
            "deltaY": {
              "type": "integer",
              "description": "Vertical wheel delta. Positive = scroll up; negative = scroll down. Typical notch ≈ 120."
            },
            "deltaX": {
              "type": "integer",
              "description": "Optional horizontal wheel delta. Positive = scroll right.",
              "default": 0
            }
          },
          "required": ["x", "y", "deltaY"]
        }
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IDesktopControlBackend _backend;

    public DesktopScrollTool(IComputerControlGate gate, IDesktopControlBackend backend)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_scroll",
        Description:
            "Scroll the mouse wheel at absolute screen coordinates. " +
            "deltaY > 0 scrolls up; deltaY < 0 scrolls down. Requires AllowComputerControl.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowComputerControl)
            return DesktopToolGate.RefuseControl();

        if (args.ValueKind != JsonValueKind.Object)
            return new ToolResult(false, "error: desktop_scroll expects a JSON object with x,y,deltaY.", null);

        if (!TryGetInt(args, "x", out var x) || !TryGetInt(args, "y", out var y)
            || !TryGetInt(args, "deltaY", out var deltaY))
        {
            return new ToolResult(false, "error: desktop_scroll requires integer 'x', 'y', and 'deltaY'.", null);
        }

        var deltaX = 0;
        if (args.TryGetProperty("deltaX", out var dx) && dx.ValueKind == JsonValueKind.Number
            && dx.TryGetInt32(out var dxVal))
        {
            deltaX = dxVal;
        }

        var result = await _backend.ScrollAsync(x, y, deltaY, deltaX, ct).ConfigureAwait(false);
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
