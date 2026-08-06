using System.Text.Json;

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

    public DesktopScreenshotTool(
        IComputerControlGate gate,
        IDesktopControlBackend backend,
        IDesktopViewHub? view = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _view = view;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_screenshot",
        Description:
            "Capture the full desktop (PNG). Returns size plus a window list with screen bounds. " +
            "REQUIRED before claiming you looked at Kurt's screen. " +
            "list_desktop_windows alone is titles/bounds — not vision. " +
            "Use with desktop_click (screen x,y). Your blue agent cursor will show where you act; Kurt's mouse stays put.",
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

        // Ensure Presence “What she saw” updates even when the backend forgot to.
        DesktopViewHub.TryRecordFromToolData(
            _view,
            result.Data,
            DesktopViewHub.SourceDesktop,
            "desktop_screenshot");

        // Enrich Content with window bounds so the model can click without
        // relying solely on vision of a huge multi-monitor PNG.
        string? windowLines = null;
        try
        {
            var windows = await _backend.ListWindowsAsync(ct).ConfigureAwait(false);
            if (windows.Success && !string.IsNullOrWhiteSpace(windows.Content))
                windowLines = windows.Content;
        }
        catch
        {
            // best-effort enrichment
        }

        var content = result.Content;
        if (!string.IsNullOrWhiteSpace(windowLines))
            content = content + "\n\n" + windowLines +
                      "\nUse desktop_click with screen coords (window center ≈ x+width/2, y+height/2).";

        return new ToolResult(result.Success, content, result.Data);
    }
}
