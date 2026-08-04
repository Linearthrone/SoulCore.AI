using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// <c>desktop_click</c> — requires session <see cref="IComputerControlGate.AllowComputerControl"/>.
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
            "Click at absolute screen coordinates (pixels, top-left origin). " +
            "Get coords from list_desktop_windows bounds (click center: x+width/2, y+height/2) or from a screenshot. " +
            "Moves your blue agent cursor overlay only — does not steal Kurt's mouse. " +
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

        var result = await _backend.ClickAsync(x, y, button, ct).ConfigureAwait(false);
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
