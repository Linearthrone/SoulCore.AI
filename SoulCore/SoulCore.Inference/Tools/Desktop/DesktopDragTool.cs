using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// <c>desktop_drag</c> — press-drag-release for CAD-style drawing; requires
/// session <see cref="IComputerControlGate.AllowComputerControl"/>.
/// </summary>
public sealed class DesktopDragTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "x1": { "type": "integer", "description": "Drag start screen X (pixels, top-left origin)." },
            "y1": { "type": "integer", "description": "Drag start screen Y." },
            "x2": { "type": "integer", "description": "Drag end screen X." },
            "y2": { "type": "integer", "description": "Drag end screen Y." },
            "button": {
              "type": "string",
              "description": "Mouse button: left, right, or middle.",
              "default": "left"
            }
          },
          "required": ["x1", "y1", "x2", "y2"]
        }
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IDesktopControlBackend _backend;

    public DesktopDragTool(IComputerControlGate gate, IDesktopControlBackend backend)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_drag",
        Description:
            "Press-drag-release from (x1,y1) to (x2,y2) in absolute screen pixels. " +
            "Use for drawing walls / lines in CAD (e.g. Chief Architect) after activating a draw tool. " +
            "Moves the blue agent cursor overlay; prefer background delivery when available. " +
            "Requires AllowComputerControl.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowComputerControl)
            return DesktopToolGate.RefuseControl();

        if (args.ValueKind != JsonValueKind.Object)
            return new ToolResult(false, "error: desktop_drag expects a JSON object with x1,y1,x2,y2.", null);

        if (!TryGetInt(args, "x1", out var x1) || !TryGetInt(args, "y1", out var y1)
            || !TryGetInt(args, "x2", out var x2) || !TryGetInt(args, "y2", out var y2))
        {
            return new ToolResult(false, "error: desktop_drag requires integer 'x1','y1','x2','y2'.", null);
        }

        var button = "left";
        if (args.TryGetProperty("button", out var b) && b.ValueKind == JsonValueKind.String)
        {
            var s = b.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                button = s;
        }

        var result = await _backend.DragAsync(x1, y1, x2, y2, button, ct).ConfigureAwait(false);
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
