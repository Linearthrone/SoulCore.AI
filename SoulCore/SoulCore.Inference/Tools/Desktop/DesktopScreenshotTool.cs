using System.Text.Json;
using SoulCore.Inference.Tools.Browser;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// <c>desktop_screenshot</c> — read-only capture. Gated by
/// <see cref="IComputerControlGate.AllowDesktopCapture"/> (default true).
/// </summary>
public sealed class DesktopScreenshotTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "monitor": {
              "type": "integer",
              "description": "Monitor index (0 = primary / virtual screen).",
              "default": 0
            }
          }
        }
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IDesktopControlBackend _backend;
    private readonly IDesktopViewHub? _view;
    private readonly IVictoriaBrowserViewHub? _browserView;
    private readonly IVisionClickCoordMap? _visionMap;

    public DesktopScreenshotTool(
        IComputerControlGate gate,
        IDesktopControlBackend backend,
        IDesktopViewHub? view = null,
        IVictoriaBrowserViewHub? browserView = null,
        IVisionClickCoordMap? visionMap = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _view = view;
        _browserView = browserView;
        _visionMap = visionMap;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_screenshot",
        Description:
            "Capture the Ubuntu guest framebuffer. A downscaled JPEG is attached for vision. " +
            "REQUIRED before claiming you looked at the VM screen or before desktop_click on in-page UI. " +
            "list_desktop_windows alone is titles/bounds — not vision. " +
            "Prefer browser_click_text for labeled buttons when it works; otherwise desktop_click from the image. " +
            "Do not call this after every click — once per look is enough.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowDesktopCapture)
            return DesktopToolGate.RefuseCapture();

        var monitor = 0;
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("monitor", out var m)
            && m.ValueKind == JsonValueKind.Number
            && m.TryGetInt32(out var mi)
            && mi >= 0)
        {
            monitor = mi;
        }

        var result = await _backend.ScreenshotAsync(monitor, ct).ConfigureAwait(false);
        if (!result.Success)
            return DesktopToolGate.FromBackend(result);

        // Same compress the tool-loop will attach — note grid so desktop_click can remap.
        var prepared = ToolImagePayload.TryPrepareFromToolData(result.Data);
        if (prepared is { } frame)
        {
            _visionMap?.NoteFrame(
                frame.SourceWidth,
                frame.SourceHeight,
                frame.VisionWidth,
                frame.VisionHeight);
        }

        // Ensure Presence “What she saw” updates even when the backend forgot to.
        DesktopViewHub.TryRecordFromToolData(
            _view,
            result.Data,
            DesktopViewHub.SourceDesktop,
            "desktop_screenshot");

        // Mirror guest framebuffer into Her screen so Kayleigh can hover→copy coords
        // that match desktop_click / guest browser_click (origin 0,0).
        VictoriaBrowserViewHub.TryPublishFromToolData(
            _browserView,
            result.Data,
            "desktop_screenshot",
            VictoriaBrowserViewHub.BackendVboxGuest,
            url: null,
            title: "victoria-sandbox");

        // Do not call ListWindowsAsync here — that is a second guestcontrol round
        // trip and was a major sandbox lag source. Window bounds stay available via
        // list_desktop_windows when needed; clicks use coords from the image.
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}
