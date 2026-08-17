namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Opens an allowlisted app inside a VM (not on the Windows host).
/// </summary>
public interface IVmGuestAppLauncher
{
    Task<DesktopOpResult> OpenAppAsync(string app, string? args = null, CancellationToken ct = default);
}

/// <summary>
/// Full guest desktop: open/click/type/screenshot in guest coordinates.
/// The host VirtualBox window does not need to be visible or focused.
/// </summary>
public interface IVmGuestDesktop : IVmGuestAppLauncher
{
    Task<DesktopOpResult> ScreenshotAsync(CancellationToken ct = default);

    Task<DesktopOpResult> ClickAsync(
        int x, int y, string button, int clicks = 1, CancellationToken ct = default);

    Task<DesktopOpResult> DragAsync(
        int x1, int y1, int x2, int y2, string button, CancellationToken ct = default);

    Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default);

    Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default);

    Task<DesktopOpResult> ScrollAsync(
        int x, int y, int deltaY, int deltaX = 0, CancellationToken ct = default);

    Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default);

    Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default);
}
