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

    /// <summary>Open a URL in the scoped browser (guest Firefox when VM-scoped).</summary>
    Task<BrowserBridgeResult> NavigateAsync(string url, CancellationToken ct = default) =>
        Task.FromResult(new BrowserBridgeResult(false, "browser_navigate is not supported on this backend", null));

    /// <summary>List labeled controls (AT-SPI / page map) in the current page.</summary>
    Task<BrowserBridgeResult> SnapshotAsync(string? query = null, CancellationToken ct = default) =>
        Task.FromResult(new BrowserBridgeResult(false, "browser_snapshot is not supported on this backend", null));

    /// <summary>Click the nth visible control whose name/label contains <paramref name="text"/>.</summary>
    Task<BrowserBridgeResult> ClickTextAsync(string text, int nth = 1, CancellationToken ct = default) =>
        Task.FromResult(new BrowserBridgeResult(false, "browser_click_text is not supported on this backend", null));

    /// <summary>Click a named field and type <paramref name="value"/>.</summary>
    Task<BrowserBridgeResult> FillAsync(string field, string value, CancellationToken ct = default) =>
        Task.FromResult(new BrowserBridgeResult(false, "browser_fill is not supported on this backend", null));

    Task<BrowserBridgeResult> BackAsync(CancellationToken ct = default) =>
        Task.FromResult(new BrowserBridgeResult(false, "browser_back is not supported on this backend", null));

    Task<BrowserBridgeResult> TabsAsync(CancellationToken ct = default) =>
        Task.FromResult(new BrowserBridgeResult(false, "browser_tabs is not supported on this backend", null));
}

/// <summary>
/// Result of a browser-bridge operation. Mapped to <see cref="ToolResult"/> by tools.
/// </summary>
/// <param name="Success">Whether the backend completed the action.</param>
/// <param name="Content">Human/model-readable summary (path, DOM snippet, or error).</param>
/// <param name="Data">Optional structured payload (e.g. screenshot path).</param>
public sealed record BrowserBridgeResult(bool Success, string Content, object? Data = null);
