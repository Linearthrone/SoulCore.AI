using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>Win32 EnumWindows / SetForegroundWindow helpers.</summary>
[SupportedOSPlatform("windows")]
internal static class Win32Windows
{
    internal sealed record WindowInfo(IntPtr Hwnd, string Title);

    internal static List<WindowInfo> ListVisibleWindows()
    {
        var list = new List<WindowInfo>();
        EnumWindows((hwnd, lParam) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;
            var title = GetTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;
            list.Add(new WindowInfo(hwnd, title));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    internal static bool TryFocusByTitle(string titleSubstring, out string? matchedTitle, out string? reason)
    {
        matchedTitle = null;
        reason = null;
        var needle = titleSubstring.Trim();
        var windows = ListVisibleWindows();
        var match = windows.FirstOrDefault(w =>
            w.Title.Contains(needle, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            reason = $"no visible window title contains '{needle}'";
            return false;
        }

        // Attach thread input so SetForegroundWindow is more reliable.
        var foreground = GetForegroundWindow();
        var targetThread = GetWindowThreadProcessId(match.Hwnd, out _);
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        if (targetThread != foregroundThread && foreground != IntPtr.Zero)
            AttachThreadInput(foregroundThread, targetThread, true);
        try
        {
            ShowWindow(match.Hwnd, SW_RESTORE);
            if (!SetForegroundWindow(match.Hwnd))
            {
                reason = "SetForegroundWindow failed";
                return false;
            }
        }
        finally
        {
            if (targetThread != foregroundThread && foreground != IntPtr.Zero)
                AttachThreadInput(foregroundThread, targetThread, false);
        }

        matchedTitle = match.Title;
        return true;
    }

    private static string GetTitle(IntPtr hwnd)
    {
        var len = GetWindowTextLength(hwnd);
        if (len <= 0)
            return string.Empty;
        var sb = new StringBuilder(len + 1);
        _ = GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private const int SW_RESTORE = 9;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
}
