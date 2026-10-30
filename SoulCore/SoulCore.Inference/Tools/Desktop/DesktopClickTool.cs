using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// <c>desktop_click</c> — requires session <see cref="IComputerControlGate.AllowComputerControl"/>.
/// Optional <c>clicks: 2</c> for double-click (BED-174).
/// Publishes pink→teal soft cursor to Presence Her screen (VM embed).
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
    private readonly IDesktopViewHub? _view;
    private readonly IVisionClickCoordMap? _visionMap;

    public DesktopClickTool(IComputerControlGate gate, IDesktopControlBackend backend)
        : this(gate, backend, view: null, visionMap: null)
    {
    }

    public DesktopClickTool(
        IComputerControlGate gate,
        IDesktopControlBackend backend,
        IDesktopViewHub? view)
        : this(gate, backend, view, visionMap: null)
    {
    }

    public DesktopClickTool(
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
        Name: "desktop_click",
        Description:
            "Click at coordinates from the last desktop_screenshot image (origin 0,0). " +
            "Use the attached JPEG pixel x,y — Host maps downscaled vision grids onto the guest framebuffer. " +
            "Optional clicks:2 for double-click. " +
            "REQUIRED: call desktop_screenshot first — never guess window-center for in-page buttons. " +
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

        var mapped = _visionMap?.MapToGuest(x, y)
                     ?? new ToolImagePayload.ClickCoordResolution(x, y, Remapped: false, "no-map");
        x = mapped.GuestX;
        y = mapped.GuestY;

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

        // Pink aim on Her screen before the press (Playwright-parity for VM embed).
        await SoftCursorPresenceFeedback.AnnounceAimAsync(_view, x, y, ct).ConfigureAwait(false);

        var result = await _backend.ClickAsync(x, y, button, clicks, ct).ConfigureAwait(false);
        if (result.Success)
        {
            var label = clicks == 2 ? $"double-clicked {button}" : $"clicked {button}";
            var note = string.IsNullOrWhiteSpace(result.Content)
                ? $"{label} at ({x},{y})"
                : result.Content;
            if (mapped.Remapped)
                note += $" [mapped from vision:{mapped.Reason}]";
            await SoftCursorPresenceFeedback.AnnounceClickAsync(_view, note, x, y, ct)
                .ConfigureAwait(false);
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
