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
    private readonly IDesktopViewHub? _view;
    private readonly IVisionClickCoordMap? _visionMap;

    public DesktopScrollTool(IComputerControlGate gate, IDesktopControlBackend backend)
        : this(gate, backend, view: null, visionMap: null)
    {
    }

    public DesktopScrollTool(
        IComputerControlGate gate,
        IDesktopControlBackend backend,
        IDesktopViewHub? view)
        : this(gate, backend, view, visionMap: null)
    {
    }

    public DesktopScrollTool(
        IComputerControlGate gate,
        IDesktopControlBackend backend,
        IDesktopViewHub? view,
        IVisionClickCoordMap? visionMap)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _view = view;
        _visionMap = visionMap;
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

        if (_visionMap is not null)
        {
            var mapped = _visionMap.MapToGuest(x, y);
            x = mapped.GuestX;
            y = mapped.GuestY;
        }

        var deltaX = 0;
        if (args.TryGetProperty("deltaX", out var dx) && dx.ValueKind == JsonValueKind.Number
            && dx.TryGetInt32(out var dxVal))
        {
            deltaX = dxVal;
        }

        await SoftCursorPresenceFeedback.AnnounceAimAsync(_view, x, y, ct).ConfigureAwait(false);
        var result = await _backend.ScrollAsync(x, y, deltaY, deltaX, ct).ConfigureAwait(false);
        if (result.Success)
        {
            // Scroll keeps idle pink at the wheel point (not a click flash).
            _view?.RecordAction(
                string.IsNullOrWhiteSpace(result.Content)
                    ? $"scrolled at ({x},{y})"
                    : result.Content,
                x,
                y);
        }

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
