namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Browser tab control backend (BED-136 / BED-182). Preferred implementation is
/// <c>NativeBrowserBridge</c> → local <c>BrowserCaptureBridge</c> :17891 +
/// unpacked Chrome extension. Legacy: Hermes MCP <c>browser_bridge_*</c> when
/// <c>Tools:BrowserBackend=hermes</c>. Tools gate capture/control before
/// calling this surface — the bridge itself does not re-check session opt-in.
/// </summary>
public interface IBrowserBridge
{
    /// <summary>Configured backend id (e.g. <c>hermes</c>).</summary>
    string BackendName { get; }

    Task<BrowserBridgeResult> HealthAsync(CancellationToken ct = default);

    Task<BrowserBridgeResult> CaptureTabAsync(int tab, CancellationToken ct = default);

    Task<BrowserBridgeResult> ClickAsync(int x, int y, CancellationToken ct = default);

    Task<BrowserBridgeResult> TypeAsync(string text, CancellationToken ct = default);

    Task<BrowserBridgeResult> KeyAsync(string key, CancellationToken ct = default);

    Task<BrowserBridgeResult> ScrollAsync(int dx, int dy, CancellationToken ct = default);
}

/// <summary>
/// Result of a browser-bridge operation. Mapped to <see cref="ToolResult"/> by tools.
/// </summary>
/// <param name="Success">Whether the backend completed the action.</param>
/// <param name="Content">Human/model-readable summary (path, DOM snippet, or error).</param>
/// <param name="Data">Optional structured payload (e.g. screenshot path).</param>
public sealed record BrowserBridgeResult(bool Success, string Content, object? Data = null);
