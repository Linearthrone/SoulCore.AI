namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Platform desktop capture / input backend used by BED-135 desktop tools.
/// Implementations must not be invoked when the corresponding Tools gate is closed —
/// tools check gates before calling.
/// </summary>
public interface IDesktopControlBackend
{
    /// <summary>Capture a monitor screenshot; returns PNG path and/or bytes summary.</summary>
    Task<DesktopBackendResult> ScreenshotAsync(int monitor, CancellationToken ct = default);

    /// <summary>Click at absolute screen coordinates.</summary>
    Task<DesktopBackendResult> ClickAsync(int x, int y, string button, CancellationToken ct = default);

    /// <summary>Type Unicode text at the current keyboard focus.</summary>
    Task<DesktopBackendResult> TypeAsync(string text, CancellationToken ct = default);

    /// <summary>Press a named key (e.g. Enter, Escape).</summary>
    Task<DesktopBackendResult> KeyAsync(string key, CancellationToken ct = default);

    /// <summary>List open top-level windows.</summary>
    Task<DesktopBackendResult> ListWindowsAsync(CancellationToken ct = default);

    /// <summary>Focus a window by title substring (case-insensitive).</summary>
    Task<DesktopBackendResult> FocusWindowAsync(string title, CancellationToken ct = default);
}

/// <summary>Result of a single desktop-backend action (not the tool gate).</summary>
public sealed record DesktopBackendResult(
    bool Success,
    string Message,
    object? Data = null);
