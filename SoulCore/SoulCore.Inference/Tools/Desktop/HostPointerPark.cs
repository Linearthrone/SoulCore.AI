using System.Runtime.InteropServices;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Parks the Windows host cursor away from the VirtualBox / Presence embed during
/// guest xdotool clicks. Mouse Integration (Absolute) only updates the guest while
/// the host cursor is over the VM HWND — parking to (0,0) often still sits on
/// Presence's top-left Her screen, so we park outside the VM (and its root) window.
/// </summary>
public static class HostPointerPark
{
    public readonly record struct Rect(int Left, int Top, int Right, int Bottom)
    {
        public bool Contains(int x, int y) =>
            x >= Left && x < Right && y >= Top && y < Bottom;
    }

    /// <summary>
    /// Park outside the victoria-sandbox HWND (and its root owner/parent window).
    /// Falls back to virtual-screen corners away from common top-left layouts.
    /// </summary>
    public static IDisposable BeginAwayFromVm(string? titleSubstring = "victoria-sandbox")
    {
        if (!OperatingSystem.IsWindows())
            return Noop.Instance;

        var excludes = new List<Rect>();
        var found = VictoriaVirtualBoxWindowLocator.TryFind(titleSubstring);
        if (found is not null)
        {
            if (GetWindowRect(found.Hwnd, out var rc))
                excludes.Add(new Rect(rc.Left, rc.Top, rc.Right, rc.Bottom));

            // SetParent child → root is Presence; Absolute tracks over that client too.
            var root = GetAncestor(found.Hwnd, GaRoot);
            if (root != 0 && root != found.Hwnd && GetWindowRect(root, out var rootRc))
                excludes.Add(new Rect(rootRc.Left, rootRc.Top, rootRc.Right, rootRc.Bottom));
        }

        return BeginAwayFrom(excludes);
    }

    /// <summary>Testable park: move to first virtual-screen candidate outside excludes.</summary>
    public static IDisposable BeginAwayFrom(IReadOnlyList<Rect> excludes)
    {
        if (!OperatingSystem.IsWindows())
            return Noop.Instance;

        if (!GetCursorPos(out var saved))
            return Noop.Instance;

        // Try several candidates — SetCursorPos clamps onto the virtual screen, so a
        // point "outside" a fullscreen Presence can snap back onto the embed.
        foreach (var (x, y) in EnumerateParkCandidates(excludes))
        {
            _ = SetCursorPos(x, y);
            Thread.Sleep(20);
            if (GetCursorPos(out var now) && !IsInsideAny(now.X, now.Y, excludes))
            {
                Thread.Sleep(SettleMs);
                return new Restorer(saved.X, saved.Y);
            }
        }

        // Last try: primary pick even if still inside (better than nothing).
        var fallback = PickParkPoint(excludes);
        _ = SetCursorPos(fallback.X, fallback.Y);
        Thread.Sleep(SettleMs);
        return new Restorer(saved.X, saved.Y);
    }

    /// <summary>Legacy entry — same as <see cref="BeginAwayFromVm"/>.</summary>
    public static IDisposable Begin() => BeginAwayFromVm();

    /// <summary>
    /// Prefer bottom-right / bottom-left / top-right of the virtual screen so we
    /// avoid Presence's usual top-left placement of Her screen.
    /// </summary>
    public static (int X, int Y) PickParkPoint(IReadOnlyList<Rect> excludes)
    {
        foreach (var c in EnumerateParkCandidates(excludes))
        {
            if (!IsInsideAny(c.X, c.Y, excludes))
                return c;
        }

        GetVirtualScreen(out var vx, out var vy, out var vw, out var vh);
        return (vx + vw - 4, vy + vh - 4);
    }

    /// <summary>Ordered park candidates (corners, then just outside excludes).</summary>
    public static IEnumerable<(int X, int Y)> EnumerateParkCandidates(IReadOnlyList<Rect> excludes)
    {
        GetVirtualScreen(out var vx, out var vy, out var vw, out var vh);

        yield return (vx + vw - 4, vy + vh - 4);
        yield return (vx + 4, vy + vh - 4);
        yield return (vx + vw - 4, vy + 4);
        yield return (vx + vw / 2, vy + vh - 4);
        yield return (vx + 4, vy + 4);

        if (excludes.Count == 0)
            yield break;

        var widest = excludes[0];
        foreach (var r in excludes)
        {
            if ((r.Right - r.Left) > (widest.Right - widest.Left))
                widest = r;
        }

        var midY = (widest.Top + widest.Bottom) / 2;
        var midX = (widest.Left + widest.Right) / 2;
        // Prefer points that may sit on a second monitor or in a gap beside Presence.
        yield return (widest.Right + 8, midY);
        yield return (widest.Left - 8, midY);
        yield return (midX, widest.Bottom + 8);
        yield return (midX, widest.Top - 8);
        yield return (widest.Right + 1, midY);
        yield return (vx + vw - 2, vy + vh - 2);
    }

    private static void GetVirtualScreen(out int vx, out int vy, out int vw, out int vh)
    {
        if (OperatingSystem.IsWindows())
        {
            vx = GetSystemMetrics(SmXVirtualScreen);
            vy = GetSystemMetrics(SmYVirtualScreen);
            vw = Math.Max(1, GetSystemMetrics(SmCxVirtualScreen));
            vh = Math.Max(1, GetSystemMetrics(SmCyVirtualScreen));
        }
        else
        {
            vx = 0;
            vy = 0;
            vw = 1920;
            vh = 1080;
        }
    }

    public static bool IsInsideAny(int x, int y, IReadOnlyList<Rect> excludes)
    {
        foreach (var r in excludes)
        {
            if (r.Contains(x, y))
                return true;
        }

        return false;
    }

    private const int SettleMs = 80;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const uint GaRoot = 2;

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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint hWnd, uint gaFlags);
}
