using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Locate the Victoria VirtualBox guest window (victoria-sandbox) for Presence HWND embed.
/// Prefer VirtualBoxVM.exe (the running VM) over VirtualBox Manager.
/// After Presence SetParent, the HWND is no longer top-level — EnumWindows misses it.
/// Cache + IsWindow keeps returning the same HWND so Her screen does not tear down.
/// </summary>
public static class VictoriaVirtualBoxWindowLocator
{
    public sealed record FoundWindow(nint Hwnd, int Pid, string Title, string ExePath);

    private static readonly object CacheGate = new();
    private static FoundWindow? _cached;

    /// <summary>
    /// Best-effort find of the victoria-sandbox VirtualBox window.
    /// Prefers a visible top-level match; falls back to a still-alive cached HWND
    /// (required after Presence SetParent makes the window a child).
    /// </summary>
    public static FoundWindow? TryFind(string? titleSubstring = "victoria-sandbox")
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var needle = (titleSubstring ?? "").Trim();
        var topLevel = TryFindTopLevel(needle);
        if (topLevel is not null)
        {
            lock (CacheGate)
                _cached = topLevel;
            return topLevel;
        }

        lock (CacheGate)
        {
            if (_cached is null)
                return null;

            if (!IsWindow(_cached.Hwnd))
            {
                _cached = null;
                return null;
            }

            // Still the same process? (VM may have restarted under a new PID.)
            GetWindowThreadProcessId(_cached.Hwnd, out uint pid);
            if (pid == 0 || unchecked((int)pid) != _cached.Pid)
            {
                _cached = null;
                return null;
            }

            var title = GetTitle(_cached.Hwnd);
            if (string.IsNullOrWhiteSpace(title))
                title = _cached.Title;

            // Refresh title on the cached record (child windows still have titles).
            _cached = _cached with { Title = title };
            return _cached;
        }
    }

    /// <summary>Drop cache (e.g. when VmEmbedPane is turned off).</summary>
    public static void ClearCache()
    {
        lock (CacheGate)
            _cached = null;
    }

    private static FoundWindow? TryFindTopLevel(string needle)
    {
        FoundWindow? best = null;
        var bestScore = -1;

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;
            if (GetWindow(hwnd, GW_OWNER) != nint.Zero)
                return true;

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0)
                return true;

            string? exe;
            try
            {
                using var proc = Process.GetProcessById(unchecked((int)pid));
                exe = proc.MainModule?.FileName;
            }
            catch
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(exe) || !IsVirtualBoxUiProcess(exe))
                return true;

            var title = GetTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            if (!string.IsNullOrEmpty(needle)
                && !title.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;

            // Prefer titled Running VM windows from VirtualBoxVM.exe.
            var score = 0;
            if (exe.EndsWith("VirtualBoxVM.exe", StringComparison.OrdinalIgnoreCase))
                score += 10;
            else if (exe.EndsWith("VirtualBox.exe", StringComparison.OrdinalIgnoreCase))
                score += 2;

            if (title.Contains("[Running]", StringComparison.OrdinalIgnoreCase))
                score += 5;
            if (title.Contains("Oracle VirtualBox", StringComparison.OrdinalIgnoreCase)
                || title.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase))
                score += 2;
            if (!string.IsNullOrEmpty(needle)
                && title.Contains(needle, StringComparison.OrdinalIgnoreCase))
                score += 3;

            // Prefer larger windows (actual VM UI over tiny dialogs).
            if (GetWindowRect(hwnd, out var rc))
            {
                var area = Math.Max(0, rc.Right - rc.Left) * Math.Max(0, rc.Bottom - rc.Top);
                score += Math.Min(area / 50000, 8);
            }

            var candidate = new FoundWindow(hwnd, unchecked((int)pid), title, exe);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }

            return true;
        }, 0);

        return best;
    }

    /// <summary>True for VirtualBoxVM.exe or VirtualBox.exe only (not VBoxSVC / helpers).</summary>
    public static bool IsVirtualBoxUiProcess(string exePath)
    {
        var normalized = exePath.Replace('/', '\\');
        var slash = normalized.LastIndexOf('\\');
        var name = slash >= 0 && slash < normalized.Length - 1
            ? normalized[(slash + 1)..]
            : normalized;
        return name.Equals("VirtualBoxVM.exe", StringComparison.OrdinalIgnoreCase)
               || name.Equals("VirtualBox.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetTitle(nint hwnd)
    {
        var len = GetWindowTextLength(hwnd);
        if (len <= 0)
            return "";
        var sb = new StringBuilder(len + 1);
        _ = GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private const uint GW_OWNER = 4;

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);
}
