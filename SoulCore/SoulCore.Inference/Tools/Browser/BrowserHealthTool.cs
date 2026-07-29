using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// <c>browser_health</c> — check browser bridge status (read; gated by
/// <see cref="ToolsOptions.AllowBrowserCapture"/>).
/// </summary>
public sealed class BrowserHealthTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{}}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly ToolsOptions _options;

    public BrowserHealthTool(IBrowserBridge bridge, IOptions<ToolsOptions> options)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_health",
        Description: "Check browser bridge status.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsCaptureAllowed(_options))
            return new ToolResult(false, BrowserToolGate.CaptureDenied, null);

        var result = await _bridge.HealthAsync(ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}
