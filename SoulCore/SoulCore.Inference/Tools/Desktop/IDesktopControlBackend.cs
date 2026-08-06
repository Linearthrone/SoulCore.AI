namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Desktop capture/control backend (BED-135 / BED-174). Native Win32 implementation,
/// cua-driver, or Hermes MCP stub. Tools enforce the session gate <em>before</em>
/// calling the backend — backends must not bypass the gate.
/// </summary>
public interface IDesktopControlBackend
{
    Task<DesktopOpResult> ScreenshotAsync(int monitor, CancellationToken ct = default);

    /// <summary>
    /// Click at screen coordinates. <paramref name="clicks"/> is 1 (single) or 2 (double).
    /// </summary>
    Task<DesktopOpResult> ClickAsync(
        int x, int y, string button, int clicks = 1, CancellationToken ct = default);

    /// <summary>
    /// Press-drag-release from (<paramref name="x1"/>,<paramref name="y1"/>) to
    /// (<paramref name="x2"/>,<paramref name="y2"/>) in screen pixels (top-left origin).
    /// </summary>
    Task<DesktopOpResult> DragAsync(
        int x1, int y1, int x2, int y2, string button, CancellationToken ct = default);

    Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Press a key or chord (e.g. <c>Enter</c>, <c>Ctrl+L</c>, <c>Alt+Tab</c>).
    /// </summary>
    Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Mouse-wheel scroll at screen point. Positive <paramref name="deltaY"/> scrolls up
    /// (away from user); negative scrolls down. <paramref name="deltaX"/> is horizontal.
    /// </summary>
    Task<DesktopOpResult> ScrollAsync(
        int x, int y, int deltaY, int deltaX = 0, CancellationToken ct = default);

    /// <summary>
    /// Launch an allowlisted local app (e.g. <c>chrome</c>, <c>notepad</c>).
    /// Optional <paramref name="args"/> may be a URL for browsers.
    /// </summary>
    Task<DesktopOpResult> OpenAppAsync(
        string app, string? args = null, CancellationToken ct = default);

    Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default);

    Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default);
}

/// <summary>
/// Backend operation result. Mapped to <see cref="ToolResult"/> by the tools.
/// <see cref="Data"/> may carry screenshot bytes/path for host-side use.
/// </summary>
public sealed record DesktopOpResult(bool Success, string Content, object? Data = null);
