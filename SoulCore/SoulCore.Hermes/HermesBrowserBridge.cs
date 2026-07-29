using System.Text.Json;
using SoulCore.Inference;
using SoulCore.Inference.Tools;
using SoulCore.Inference.Tools.Browser;

namespace SoulCore.Hermes;

/// <summary>
/// Hermes MCP <c>browser_bridge_*</c> backend (BED-136 + BED-144 CallMcpToolAsync).
/// </summary>
public sealed class HermesBrowserBridge : IBrowserBridge
{
    public const string UnavailableContent = IHermesMcpInvoker.UnavailableMessage;

    private readonly IHermesClient _hermes;

    public HermesBrowserBridge(IHermesClient hermes)
    {
        _hermes = hermes ?? throw new ArgumentNullException(nameof(hermes));
    }

    public string BackendName => "hermes";

    public Task<BrowserBridgeResult> HealthAsync(CancellationToken ct = default) =>
        CallAsync("browser_bridge_health", HermesToolRouting.EmptyArgs(), ct);

    public Task<BrowserBridgeResult> CaptureTabAsync(int tab, CancellationToken ct = default) =>
        CallAsync("browser_bridge_capture_tab",
            HermesToolRouting.MergeObject(HermesToolRouting.EmptyArgs(), new Dictionary<string, object?> { ["tab"] = tab }), ct);

    public Task<BrowserBridgeResult> ClickAsync(int x, int y, CancellationToken ct = default) =>
        CallAsync("browser_bridge_click",
            HermesToolRouting.MergeObject(HermesToolRouting.EmptyArgs(), new Dictionary<string, object?> { ["x"] = x, ["y"] = y }), ct);

    public Task<BrowserBridgeResult> TypeAsync(string text, CancellationToken ct = default) =>
        CallAsync("browser_bridge_type",
            HermesToolRouting.MergeObject(HermesToolRouting.EmptyArgs(), new Dictionary<string, object?> { ["text"] = text }), ct);

    public Task<BrowserBridgeResult> KeyAsync(string key, CancellationToken ct = default) =>
        CallAsync("browser_bridge_key",
            HermesToolRouting.MergeObject(HermesToolRouting.EmptyArgs(), new Dictionary<string, object?> { ["key"] = key }), ct);

    public Task<BrowserBridgeResult> ScrollAsync(int dx, int dy, CancellationToken ct = default) =>
        CallAsync("browser_bridge_scroll",
            HermesToolRouting.MergeObject(HermesToolRouting.EmptyArgs(), new Dictionary<string, object?> { ["dx"] = dx, ["dy"] = dy }), ct);

    private async Task<BrowserBridgeResult> CallAsync(string mcpTool, JsonElement args, CancellationToken ct)
    {
        var result = await _hermes.CallMcpToolAsync(mcpTool, args, ct).ConfigureAwait(false);
        return new BrowserBridgeResult(result.Success, result.Content, result.Data);
    }
}
