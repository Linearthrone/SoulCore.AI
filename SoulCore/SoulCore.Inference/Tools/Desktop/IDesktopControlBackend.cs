namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Desktop capture/control backend (BED-135). Native Win32 implementation or
/// Hermes MCP <c>computer_use</c> stub/router (BED-144 polishes Hermes path).
/// Tools enforce the session gate <em>before</em> calling the backend — backends
/// must not bypass the gate.
/// </summary>
public interface IDesktopControlBackend
{
    Task<DesktopOpResult> ScreenshotAsync(int monitor, CancellationToken ct = default);

    Task<DesktopOpResult> ClickAsync(int x, int y, string button, CancellationToken ct = default);

    /// <summary>
    /// Press-drag-release from (<paramref name="x1"/>,<paramref name="y1"/>) to
    /// (<paramref name="x2"/>,<paramref name="y2"/>) in screen pixels (top-left origin).
    /// </summary>
    Task<DesktopOpResult> DragAsync(
        int x1, int y1, int x2, int y2, string button, CancellationToken ct = default);

    Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default);

    Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default);

    Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default);

    Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default);
}

/// <summary>
/// Backend operation result. Mapped to <see cref="ToolResult"/> by the tools.
/// <see cref="Data"/> may carry screenshot bytes/path for host-side use.
/// </summary>
public sealed record DesktopOpResult(bool Success, string Content, object? Data = null);
