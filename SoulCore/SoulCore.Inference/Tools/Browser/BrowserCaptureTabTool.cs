using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary><c>browser_capture_tab</c> — gated by <see cref="ToolsOptions.AllowBrowserCapture"/>.</summary>
public sealed class BrowserCaptureTabTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"tab":{"type":"integer","description":"0-based page tab index among CDP page targets.","default":0}}}""")
        .RootElement.Clone();

    private readonly IOptions<ToolsOptions> _options;
    private readonly IBrowserControlBackend _backend;

    public BrowserCaptureTabTool(IOptions<ToolsOptions> options, IBrowserControlBackend backend)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_capture_tab",
        Description: "Capture the current browser tab (screenshot + DOM).",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsCaptureAllowed(_options.Value))
            return new ToolResult(false, BrowserToolGate.CaptureDeniedMessage, null);

        var tab = 0;
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("tab", out var t)
            && t.ValueKind == JsonValueKind.Number && t.TryGetInt32(out var ti))
        {
            tab = ti;
        }

        var result = await _backend.CaptureTabAsync(tab, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Message, result.Data);
    }
}
