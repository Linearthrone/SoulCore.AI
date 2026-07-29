using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// <c>browser_capture_tab</c> — capture current browser tab (screenshot + optional DOM).
/// Read-only; gated by <see cref="ToolsOptions.AllowBrowserCapture"/>.
/// </summary>
public sealed class BrowserCaptureTabTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"tab":{"type":"integer","default":0,"description":"Tab index (0-based)."}}}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly ToolsOptions _options;

    public BrowserCaptureTabTool(IBrowserBridge bridge, IOptions<ToolsOptions> options)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_capture_tab",
        Description: "Capture the current browser tab (screenshot + DOM).",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsCaptureAllowed(_options))
            return new ToolResult(false, BrowserToolGate.CaptureDenied, null);

        var tab = 0;
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("tab", out var tabProp)
            && tabProp.ValueKind == JsonValueKind.Number
            && tabProp.TryGetInt32(out var parsed))
        {
            tab = parsed;
        }

        if (tab < 0)
            return new ToolResult(false, "error: browser_capture_tab 'tab' must be >= 0", null);

        var result = await _bridge.CaptureTabAsync(tab, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}
