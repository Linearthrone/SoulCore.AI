namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Browser tab capture / input backend used by BED-136 browser tools.
/// Implementations must not be invoked when the corresponding Tools gate is closed —
/// tools check gates before calling.
/// </summary>
public interface IBrowserControlBackend
{
    /// <summary>Report whether a browser bridge / CDP endpoint is reachable.</summary>
    Task<BrowserBackendResult> HealthAsync(CancellationToken ct = default);

    /// <summary>Capture tab screenshot (+ optional DOM text). <paramref name="tab"/> is 0-based among page targets.</summary>
    Task<BrowserBackendResult> CaptureTabAsync(int tab, CancellationToken ct = default);

    /// <summary>Click at viewport coordinates in the active/selected tab.</summary>
    Task<BrowserBackendResult> ClickAsync(int x, int y, CancellationToken ct = default);

    /// <summary>Type Unicode text into the focused element.</summary>
    Task<BrowserBackendResult> TypeAsync(string text, CancellationToken ct = default);

    /// <summary>Press a named key (e.g. Enter, Escape, Tab).</summary>
    Task<BrowserBackendResult> KeyAsync(string key, CancellationToken ct = default);

    /// <summary>Scroll the page by pixel deltas.</summary>
    Task<BrowserBackendResult> ScrollAsync(int dx, int dy, CancellationToken ct = default);
}

/// <summary>Result of a single browser-backend action (not the tool gate).</summary>
public sealed record BrowserBackendResult(
    bool Success,
    string Message,
    object? Data = null);
