using System.Runtime.InteropServices;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Parks the Windows host cursor away from the VirtualBox window during guest
/// xdotool clicks. With Mouse Integration (Absolute), hovering Her screen keeps
/// overriding the guest pointer — so aim overlay looks right but the press lands
/// under Kayleigh's mouse (or nowhere useful).
/// </summary>
public static class HostPointerPark
{
    /// <summary>
    /// Move host cursor to the virtual-screen origin, wait briefly for Absolute
    /// pointing to settle, restore on dispose. No-op on non-Windows.
    /// </summary>
    public static IDisposable Begin()
    {
        if (!OperatingSystem.IsWindows())
            return Noop.Instance;

        if (!GetCursorPos(out var saved))
            return Noop.Instance;

        var x = GetSystemMetrics(SmXVirtualScreen);
        var y = GetSystemMetrics(SmYVirtualScreen);
        // Nudge one pixel if already there so Absolute still gets an update.
        if (saved.X == x && saved.Y == y)
            x += 1;

        _ = SetCursorPos(x, y);
        Thread.Sleep(SettleMs);
        return new Restorer(saved.X, saved.Y);
    }

    private const int SettleMs = 40;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;

    private sealed class Restorer : IDisposable
    {
        private readonly int _x;
        private readonly int _y;
        private bool _done;

        public Restorer(int x, int y)
        {
            _x = x;
            _y = y;
        }

        public void Dispose()
        {
            if (_done || !OperatingSystem.IsWindows())
                return;
            _done = true;
            _ = SetCursorPos(_x, _y);
        }
    }

    private sealed class Noop : IDisposable
    {
        public static readonly Noop Instance = new();
        public void Dispose() { }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
