using System.Text.Json;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// <c>browser_capture_tab</c> — capture current browser tab (screenshot + optional DOM).
/// Read-only; gated by browser capture session opt-in.
/// </summary>
public sealed class BrowserCaptureTabTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"tab":{"type":"integer","default":0,"description":"Tab index (0-based)."}}}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly IToolsAccessSettings _access;
    private readonly IDesktopViewHub? _view;

    public BrowserCaptureTabTool(
        IBrowserBridge bridge,
        IToolsAccessSettings access,
        IDesktopViewHub? view = null)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _view = view;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_capture_tab",
        Description: "Capture the current browser tab (screenshot + DOM). Updates Presence with the tab image when bytes are available.",
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
        {
            DesktopViewHub.TryRecordFromToolData(
                _view,
                result.Data,
                DesktopViewHub.SourceBrowser,
                $"browser_capture_tab[{tab}]");
        }

        return new ToolResult(result.Success, result.Content, result.Data);
    }
}
