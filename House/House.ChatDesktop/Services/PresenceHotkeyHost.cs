using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace House.ChatDesktop.Services;

/// <summary>
/// Win32 RegisterHotKey on a message-only window so Ctrl+Shift+H / Q work with no tray icon
/// and no focused Presence window. Other platforms report not armed.
/// </summary>
public sealed class PresenceHotkeyHost : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int ErrorClassAlreadyExists = 1410;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly WndProcDelegate _proc;
    private IntPtr _hwnd;
    private bool _classRegistered;
    private string? _className;
    private bool _toggleRegistered;
    private bool _exitRegistered;
    private bool _disposed;

    public PresenceHotkeyHost()
    {
        _proc = WndProc;
    }

    public event Action<PresenceHotkeyAction>? Pressed;

    public bool TryRegister(out string detail)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!OperatingSystem.IsWindows())
        {
            detail = "Global hotkeys are registered on Windows only.";
            return false;
        }

        if (_hwnd != IntPtr.Zero)
        {
            detail = "Hotkeys already registered.";
            return _toggleRegistered && _exitRegistered;
        }

        _className = "SoulCorePresenceHotkeys_" + Environment.ProcessId;
        var wc = new WndClass
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            lpszClassName = _className,
            hInstance = GetModuleHandle(null)
        };

        var atom = RegisterClass(ref wc);
        if (atom == 0)
        {
            var err = Marshal.GetLastWin32Error();
            if (err != ErrorClassAlreadyExists)
            {
                detail = "RegisterClass failed: " + err;
                return false;
            }
        }
        else
        {
            _classRegistered = true;
        }

        _hwnd = CreateWindowEx(0, _className, "", 0, 0, 0, 0, 0, HwndMessage, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            detail = "Hotkey window failed: " + Marshal.GetLastWin32Error();
            return false;
        }

        var mods = PresenceHotkeyPlan.ModControl | PresenceHotkeyPlan.ModShift | PresenceHotkeyPlan.ModNoRepeat;
        _toggleRegistered = RegisterHotKey(_hwnd, PresenceHotkeyPlan.ToggleChatId, mods, PresenceHotkeyPlan.VkH);
        _exitRegistered = RegisterHotKey(_hwnd, PresenceHotkeyPlan.ExitId, mods, PresenceHotkeyPlan.VkQ);
        if (_toggleRegistered && _exitRegistered)
        {
            detail = "Ctrl+Shift+H and Ctrl+Shift+Q registered.";
            return true;
        }

        detail = "RegisterHotKey failed: " + Marshal.GetLastWin32Error()
            + $" toggle={_toggleRegistered} exit={_exitRegistered}";
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_hwnd != IntPtr.Zero)
        {
            if (_toggleRegistered)
                UnregisterHotKey(_hwnd, PresenceHotkeyPlan.ToggleChatId);
            if (_exitRegistered)
                UnregisterHotKey(_hwnd, PresenceHotkeyPlan.ExitId);
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        if (_classRegistered && _className is not null)
            UnregisterClass(_className, GetModuleHandle(null));
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY && PresenceHotkeyPlan.TryFromId(wParam.ToInt32(), out var action))
        {
            var captured = action;
            try
            {
                Dispatcher.UIThread.Post(() => Pressed?.Invoke(captured));
            }
            catch
            {
                Pressed?.Invoke(captured);
            }

            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClass
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "RegisterClassW")]
    private static extern ushort RegisterClass(ref WndClass lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
