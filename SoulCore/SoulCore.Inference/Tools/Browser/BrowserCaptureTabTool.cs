using System.Text.Json;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// <c>browser_capture_tab</c> — capture current browser tab (screenshot + optional DOM).
/// Read-only; gated by browser capture session opt-in.
/// Pushes the PNG into <see cref="IDesktopViewHub"/> so ChatDesktop Desktop preview updates.
/// </summary>
public sealed class BrowserCaptureTabTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"tab":{"type":"integer","default":0,"description":"Tab index (0-based)."}}}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly IToolsAccessSettings _access;
    private readonly IDesktopViewHub? _view;

    public BrowserCaptureTabTool(IBrowserBridge bridge, IToolsAccessSettings access)
        : this(bridge, access, view: null)
    {
    }

    public BrowserCaptureTabTool(
        IBrowserBridge bridge,
        IToolsAccessSettings access,
        IDesktopViewHub? view)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _view = view;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_capture_tab",
        Description: "Capture the current browser tab (screenshot + DOM).",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsCaptureAllowed(_access))
            return new ToolResult(false, BrowserToolGate.CaptureDenied, null);

        var tab = 0;
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("tab", out var tabProp)
            && tabProp.ValueKind == JsonValueKind.Number
            && tabProp.TryGetInt32(out var parsed))
        {
            tab = parsed;
        }

        var result = await _bridge.CaptureTabAsync(tab, ct).ConfigureAwait(false);
        if (result.Success)
            TryPublishToDesktopView(result);

        return new ToolResult(result.Success, result.Content, result.Data);
    }

    private void TryPublishToDesktopView(BrowserBridgeResult result)
    {
        if (_view is null)
            return;

        try
        {
            var path = TryGetScreenshotPath(result);
            byte[]? bytes = null;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                bytes = File.ReadAllBytes(path);

            if (bytes is { Length: > 0 })
            {
                var (w, h) = TryReadPngSize(bytes);
                _view.RecordScreenshot(bytes, "png", w, h, path);
                _view.RecordAction(
                    string.IsNullOrWhiteSpace(path)
                        ? "browser capture (tab)"
                        : $"browser capture {w}x{h} (tab)");
            }
            else if (!string.IsNullOrWhiteSpace(result.Content))
            {
                _view.RecordAction(Truncate(result.Content, 120));
            }
        }
        catch
        {
            // Preview publish must never fail the tool.
        }
    }

    private static string? TryGetScreenshotPath(BrowserBridgeResult result)
    {
        if (result.Data is string json && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("screenshot_path", out var p)
                    && p.ValueKind == JsonValueKind.String)
                {
                    return p.GetString();
                }
            }
            catch (JsonException)
            {
                // fall through
            }
        }

        var content = result.Content ?? string.Empty;
        const string marker = "path=";
        var idx = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;
        return content[(idx + marker.Length)..].Trim();
    }

    private static (int Width, int Height) TryReadPngSize(byte[] png)
    {
        if (png.Length < 24)
            return (0, 0);
        // IHDR width/height at bytes 16..23, big-endian.
        var w = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        var h = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        return (w, h);
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
