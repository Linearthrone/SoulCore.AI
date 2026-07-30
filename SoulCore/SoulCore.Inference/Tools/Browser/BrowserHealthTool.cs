using System.Text.Json;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// <c>browser_health</c> — check browser bridge status (read; gated by browser capture).
/// </summary>
public sealed class BrowserHealthTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{}}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly IToolsAccessSettings _access;

    public BrowserHealthTool(IBrowserBridge bridge, IToolsAccessSettings access)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _access = access ?? throw new ArgumentNullException(nameof(access));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_health",
        Description: "Check browser bridge status.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsCaptureAllowed(_access))
            return new ToolResult(false, BrowserToolGate.CaptureDenied, null);

        var result = await _bridge.HealthAsync(ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}
