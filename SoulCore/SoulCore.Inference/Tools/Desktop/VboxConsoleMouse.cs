using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Injects guest mouse events through the VirtualBox COM console API
/// (<c>IMouse.putMouseEventAbsolute</c>). Does not need guestcontrol, xdotool,
/// or SOULCORE_VBOX_GUEST_PASS — and works while the host cursor is parked off
/// the Presence embed (so Mouse Integration Absolute cannot yank the pointer).
/// </summary>
public static class VboxConsoleMouse
{
    public const int LeftButton = 0x01;
    public const int RightButton = 0x02;
    public const int MiddleButton = 0x04;
    public const int LockTypeShared = 1;

    /// <summary>
    /// Guest framebuffer (0,0) → VBox absolute (1,1) origin; button mask for down/up.
    /// </summary>
    public static (int Ax, int Ay, int DownFlags) PlanAbsolutePress(int x, int y, int xdotoolButton)
    {
        var ax = Math.Max(0, x) + 1;
        var ay = Math.Max(0, y) + 1;
        var flags = xdotoolButton switch
        {
            1 => LeftButton,
            2 => MiddleButton,
            3 => RightButton,
            _ => LeftButton
        };
        return (ax, ay, flags);
    }

    /// <summary>
    /// Click at guest framebuffer coords. Returns null when COM/VBox is unavailable
    /// (non-Windows, ProgID missing, lock failed) so callers can fall back.
    /// </summary>
    public static DesktopOpResult? TryClick(string vmName, int x, int y, int xdotoolButton, int clicks)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        if (string.IsNullOrWhiteSpace(vmName) || x < 0 || y < 0 || clicks is not (1 or 2))
            return null;

        try
        {
            return ClickWindows(vmName.Trim(), x, y, xdotoolButton, clicks);
        }
        catch (COMException ex)
        {
            return new DesktopOpResult(
                false,
                $"VirtualBox console mouse COM failed (0x{ex.ErrorCode:X8}): {ex.Message}",
                null);
        }
        catch (Exception ex)
        {
            return new DesktopOpResult(
                false,
                $"VirtualBox console mouse failed: {ex.GetType().Name}: {ex.Message}",
                null);
        }
    }

    [SupportedOSPlatform("windows")]
    private static DesktopOpResult ClickWindows(string vmName, int x, int y, int xdotoolButton, int clicks)
    {
        var vboxType = Type.GetTypeFromProgID("VirtualBox.VirtualBox");
        var sessionType = Type.GetTypeFromProgID("VirtualBox.Session");
        if (vboxType is null || sessionType is null)
            return new DesktopOpResult(false, "VirtualBox COM ProgID not registered on this host.", null);

        dynamic vbox = Activator.CreateInstance(vboxType)
            ?? throw new InvalidOperationException("VirtualBox.VirtualBox create failed");
        dynamic session = Activator.CreateInstance(sessionType)
            ?? throw new InvalidOperationException("VirtualBox.Session create failed");
        dynamic machine = vbox.FindMachine(vmName);
        machine.LockMachine(session, LockTypeShared);
        try
        {
            dynamic mouse = session.Console.Mouse;
            var (ax, ay, down) = PlanAbsolutePress(x, y, xdotoolButton);

            // Park Absolute: out-of-range then our point (host cursor should already be off VM).
            mouse.putMouseEventAbsolute(ax, ay, 0, 0, 0);
            Thread.Sleep(40);

            for (var i = 0; i < clicks; i++)
            {
                mouse.putMouseEventAbsolute(ax, ay, 0, 0, down);
                Thread.Sleep(45);
                mouse.putMouseEventAbsolute(ax, ay, 0, 0, 0);
                if (i + 1 < clicks)
                    Thread.Sleep(80);
            }

            // Release host-drawn cursor convention so Guest Additions can show pointer again.
            mouse.putMouseEventAbsolute(0x7fffffff, 0x7fffffff, 0, 0, 0);

            var label = clicks == 2 ? "double-clicked" : "clicked";
            return new DesktopOpResult(
                true,
                $"{label} via VirtualBox console mouse at guest ({x},{y}) in the Ubuntu VM (not Windows).",
                new { x, y, clicks, coords = "guest-framebuffer", method = "vbox-console-mouse" });
        }
        finally
        {
            try { session.UnlockMachine(); }
            catch { /* best-effort */ }
            try { Marshal.FinalReleaseComObject(session); }
            catch { /* ignore */ }
            try { Marshal.FinalReleaseComObject(vbox); }
            catch { /* ignore */ }
        }
    }
}
