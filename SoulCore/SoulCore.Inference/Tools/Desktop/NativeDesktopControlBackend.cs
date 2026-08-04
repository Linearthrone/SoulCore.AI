using System.Runtime.InteropServices;
using System.Text;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Native C# desktop backend (BED-135): Win32 GDI capture + <c>SendInput</c>
/// for click/type/key, EnumWindows for list/focus. Windows-only — non-Windows
/// hosts get a clear <c>Success:false</c> (Linux CI uses a mock backend in tests).
/// </summary>
public sealed class NativeDesktopControlBackend : IDesktopControlBackend
{
    private readonly IDesktopViewHub? _view;
    private readonly Func<bool> _softCursorRestore;

    public NativeDesktopControlBackend()
        : this(view: null, softCursorRestore: null)
    {
    }

    public NativeDesktopControlBackend(IDesktopViewHub? view, IToolsAccessSettings? access)
        : this(view, access is null ? null : () => access.SoftCursorRestore)
    {
    }

    public NativeDesktopControlBackend(IDesktopViewHub? view, Func<bool>? softCursorRestore)
    {
        _view = view;
        _softCursorRestore = softCursorRestore ?? (() => true);
    }

    public Task<DesktopOpResult> ScreenshotAsync(int monitor, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(NotWindows("screenshot"));

        ct.ThrowIfCancellationRequested();
        try
        {
            var (path, width, height) = CapturePrimaryMonitorBmp(monitor);
            var bytes = File.ReadAllBytes(path);
            _view?.RecordScreenshot(bytes, "bmp", width, height, path);
            _view?.RecordAction($"screenshot monitor={monitor} ({width}x{height})");
            return Task.FromResult(new DesktopOpResult(
                Success: true,
                Content: $"captured desktop screenshot (monitor={monitor}) saved to {path} ({bytes.Length} bytes BMP)",
                Data: new { path, bytes, monitor, format = "bmp", width, height }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new DesktopOpResult(
                false, $"native screenshot failed: {ex.GetType().Name}: {ex.Message}", null));
        }
    }

    public Task<DesktopOpResult> ClickAsync(int x, int y, string button, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(NotWindows("click"));

        ct.ThrowIfCancellationRequested();
        var btn = (button ?? "left").Trim().ToLowerInvariant();
        if (btn is not ("left" or "right" or "middle"))
            return Task.FromResult(new DesktopOpResult(false, $"unsupported button '{button}' (use left|right|middle)", null));

        try
        {
            var restore = _softCursorRestore();
            // Prefer background PostMessage (OS cursor untouched) when soft/agent mode is on.
            if (restore && TryBackgroundClick(x, y, btn, out var bgNote))
            {
                _view?.RecordAction(bgNote, x, y);
                return Task.FromResult(new DesktopOpResult(
                    true, bgNote, new { x, y, button = btn, softCursor = true, delivery = "background" }));
            }

            POINT saved = default;
            var hadSaved = restore && GetCursorPos(out saved);

            if (!SetCursorPos(x, y))
                return Task.FromResult(new DesktopOpResult(false, $"SetCursorPos({x},{y}) failed", null));

            var (down, up) = btn switch
            {
                "right" => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
                "middle" => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
                _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
            };

            var inputs = new INPUT[2];
            inputs[0] = MouseInput(down);
            inputs[1] = MouseInput(up);
            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            if (sent != inputs.Length)
                return Task.FromResult(new DesktopOpResult(false, $"SendInput click failed (sent {sent}/{inputs.Length})", null));

            if (hadSaved)
                _ = SetCursorPos(saved.X, saved.Y);

            var note = restore
                ? $"clicked {btn} at ({x},{y}) [soft cursor — user pointer restored]"
                : $"clicked {btn} at ({x},{y})";
            _view?.RecordAction(note, x, y);
            return Task.FromResult(new DesktopOpResult(
                true, note, new { x, y, button = btn, softCursor = restore, delivery = "foreground" }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new DesktopOpResult(
                false, $"native click failed: {ex.GetType().Name}: {ex.Message}", null));
        }
    }

    public Task<DesktopOpResult> DragAsync(
        int x1, int y1, int x2, int y2, string button, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(NotWindows("drag"));

        ct.ThrowIfCancellationRequested();
        var btn = (button ?? "left").Trim().ToLowerInvariant();
        if (btn is not ("left" or "right" or "middle"))
            return Task.FromResult(new DesktopOpResult(false, $"unsupported button '{button}' (use left|right|middle)", null));

        try
        {
            var restore = _softCursorRestore();
            if (restore && TryBackgroundDrag(x1, y1, x2, y2, btn, out var bgNote))
            {
                _view?.RecordAction(bgNote, x2, y2);
                return Task.FromResult(new DesktopOpResult(
                    true, bgNote, new { x1, y1, x2, y2, button = btn, softCursor = true, delivery = "background" }));
            }

            POINT saved = default;
            var hadSaved = restore && GetCursorPos(out saved);

            var (down, up) = btn switch
            {
                "right" => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
                "middle" => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
                _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
            };

            if (!SetCursorPos(x1, y1))
                return Task.FromResult(new DesktopOpResult(false, $"SetCursorPos({x1},{y1}) failed", null));

            var downInput = new INPUT[] { MouseInput(down) };
            if (SendInput(1, downInput, Marshal.SizeOf<INPUT>()) != 1)
                return Task.FromResult(new DesktopOpResult(false, "SendInput mouse-down failed", null));

            // Interpolate moves while button held so CAD apps see a continuous drag.
            const int steps = 20;
            for (var i = 1; i <= steps; i++)
            {
                ct.ThrowIfCancellationRequested();
                var t = (double)i / steps;
                var mx = (int)Math.Round(x1 + (x2 - x1) * t);
                var my = (int)Math.Round(y1 + (y2 - y1) * t);
                if (!SetCursorPos(mx, my))
                    return Task.FromResult(new DesktopOpResult(false, $"SetCursorPos({mx},{my}) during drag failed", null));
                Thread.Sleep(15);
            }

            var upInput = new INPUT[] { MouseInput(up) };
            if (SendInput(1, upInput, Marshal.SizeOf<INPUT>()) != 1)
                return Task.FromResult(new DesktopOpResult(false, "SendInput mouse-up failed", null));

            if (hadSaved)
                _ = SetCursorPos(saved.X, saved.Y);

            var note = restore
                ? $"dragged {btn} from ({x1},{y1}) to ({x2},{y2}) [soft cursor — user pointer restored]"
                : $"dragged {btn} from ({x1},{y1}) to ({x2},{y2})";
            _view?.RecordAction(note, x2, y2);
            return Task.FromResult(new DesktopOpResult(
                true, note, new { x1, y1, x2, y2, button = btn, softCursor = restore, delivery = "foreground" }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new DesktopOpResult(
                false, $"native drag failed: {ex.GetType().Name}: {ex.Message}", null));
        }
    }

    public Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(NotWindows("type"));

        if (string.IsNullOrEmpty(text))
            return Task.FromResult(new DesktopOpResult(false, "text must be non-empty", null));

        ct.ThrowIfCancellationRequested();
        try
        {
            var inputs = new List<INPUT>(text.Length * 2);
            foreach (var ch in text)
            {
                inputs.Add(UnicodeKey(ch, keyUp: false));
                inputs.Add(UnicodeKey(ch, keyUp: true));
            }

            var arr = inputs.ToArray();
            var sent = SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
            if (sent != arr.Length)
                return Task.FromResult(new DesktopOpResult(false, $"SendInput type failed (sent {sent}/{arr.Length})", null));

            _view?.RecordAction($"typed {text.Length} character(s)");
            return Task.FromResult(new DesktopOpResult(
                true, $"typed {text.Length} character(s)", new { length = text.Length }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new DesktopOpResult(
                false, $"native type failed: {ex.GetType().Name}: {ex.Message}", null));
        }
    }

    public Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(NotWindows("key"));

        if (string.IsNullOrWhiteSpace(key))
            return Task.FromResult(new DesktopOpResult(false, "key must be non-empty", null));

        ct.ThrowIfCancellationRequested();
        if (!TryMapVirtualKey(key.Trim(), out var vk))
            return Task.FromResult(new DesktopOpResult(false, $"unsupported key '{key}'", null));

        try
        {
            var inputs = new INPUT[]
            {
                VKey(vk, keyUp: false),
                VKey(vk, keyUp: true),
            };
            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            if (sent != inputs.Length)
                return Task.FromResult(new DesktopOpResult(false, $"SendInput key failed (sent {sent}/{inputs.Length})", null));

            _view?.RecordAction($"pressed key '{key}'");
            return Task.FromResult(new DesktopOpResult(true, $"pressed key '{key}'", new { key }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new DesktopOpResult(
                false, $"native key failed: {ex.GetType().Name}: {ex.Message}", null));
        }
    }

    public Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(NotWindows("list_windows"));

        ct.ThrowIfCancellationRequested();
        try
        {
            var windows = new List<object>();
            var lines = new StringBuilder("open desktop windows:");
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                var len = GetWindowTextLength(hWnd);
                if (len <= 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                var title = sb.ToString();
                if (string.IsNullOrWhiteSpace(title)) return true;
                var index = windows.Count;
                var rect = default(RECT);
                GetWindowRect(hWnd, out rect);
                var wx = rect.Left;
                var wy = rect.Top;
                var ww = Math.Max(0, rect.Right - rect.Left);
                var wh = Math.Max(0, rect.Bottom - rect.Top);
                var cx = wx + ww / 2;
                var cy = wy + wh / 2;
                windows.Add(new
                {
                    index,
                    title,
                    hwnd = hWnd.ToInt64(),
                    x = wx,
                    y = wy,
                    width = ww,
                    height = wh,
                    centerX = cx,
                    centerY = cy
                });
                lines.Append("\n[").Append(index).Append("] ").Append(title)
                    .Append(" bounds=(").Append(wx).Append(',').Append(wy)
                    .Append(' ').Append(ww).Append('x').Append(wh).Append(')')
                    .Append(" center=(").Append(cx).Append(',').Append(cy).Append(')');
                return true;
            }, IntPtr.Zero);

            if (windows.Count == 0)
                return Task.FromResult(new DesktopOpResult(true, "no visible titled windows", windows));

            return Task.FromResult(new DesktopOpResult(true, lines.ToString(), windows));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new DesktopOpResult(
                false, $"native list_windows failed: {ex.GetType().Name}: {ex.Message}", null));
        }
    }

    public Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(NotWindows("focus_window"));

        if (string.IsNullOrWhiteSpace(title))
            return Task.FromResult(new DesktopOpResult(false, "title must be non-empty", null));

        ct.ThrowIfCancellationRequested();
        try
        {
            IntPtr found = IntPtr.Zero;
            string? matched = null;
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                var len = GetWindowTextLength(hWnd);
                if (len <= 0) return true;
                var sb = new StringBuilder(len + 1);
                _ = GetWindowText(hWnd, sb, sb.Capacity);
                var t = sb.ToString();
                if (t.Contains(title, StringComparison.OrdinalIgnoreCase))
                {
                    found = hWnd;
                    matched = t;
                    return false; // stop
                }
                return true;
            }, IntPtr.Zero);

            if (found == IntPtr.Zero)
                return Task.FromResult(new DesktopOpResult(false, $"no window matching title '{title}'", null));

            _ = ShowWindow(found, SW_RESTORE);
            var ok = SetForegroundWindow(found);
            if (!ok)
                return Task.FromResult(new DesktopOpResult(false, $"SetForegroundWindow failed for '{matched}'", null));

            return Task.FromResult(new DesktopOpResult(
                true, $"focused window '{matched}'", new { title = matched }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new DesktopOpResult(
                false, $"native focus_window failed: {ex.GetType().Name}: {ex.Message}", null));
        }
    }

    private static DesktopOpResult NotWindows(string op)
        => new(false, $"native desktop {op} requires Windows (OS={RuntimeInformation.OSDescription})", null);

    /// <summary>
    /// Click without moving the OS cursor (LLMOD/cua-style background path):
    /// <c>WindowFromPoint</c> + <c>PostMessage</c> WM_*BUTTON*.
    /// </summary>
    private static bool TryBackgroundClick(int screenX, int screenY, string button, out string note)
    {
        note = "";
        var hwnd = WindowFromPoint(new POINT { X = screenX, Y = screenY });
        if (hwnd == IntPtr.Zero)
            return false;

        var client = new POINT { X = screenX, Y = screenY };
        if (!ScreenToClient(hwnd, ref client))
            return false;

        var lParam = (IntPtr)((client.Y << 16) | (client.X & 0xFFFF));
        var (downMsg, upMsg) = button switch
        {
            "right" => (WM_RBUTTONDOWN, WM_RBUTTONUP),
            "middle" => (WM_MBUTTONDOWN, WM_MBUTTONUP),
            _ => (WM_LBUTTONDOWN, WM_LBUTTONUP),
        };

        // PostMessage returns bool; we still treat as best-effort for canvases that drop it.
        _ = PostMessage(hwnd, downMsg, IntPtr.Zero, lParam);
        _ = PostMessage(hwnd, upMsg, IntPtr.Zero, lParam);
        note = $"clicked {button} at ({screenX},{screenY}) [background PostMessage — OS mouse untouched]";
        return true;
    }

    /// <summary>
    /// Drag via PostMessage mouse-down / move / up without moving the OS cursor.
    /// Best-effort — many CAD canvases need the foreground SendInput path instead.
    /// </summary>
    private static bool TryBackgroundDrag(
        int x1, int y1, int x2, int y2, string button, out string note)
    {
        note = "";
        var hwnd = WindowFromPoint(new POINT { X = x1, Y = y1 });
        if (hwnd == IntPtr.Zero)
            return false;

        var (downMsg, upMsg) = button switch
        {
            "right" => (WM_RBUTTONDOWN, WM_RBUTTONUP),
            "middle" => (WM_MBUTTONDOWN, WM_MBUTTONUP),
            _ => (WM_LBUTTONDOWN, WM_LBUTTONUP),
        };

        static IntPtr ToLParam(POINT p) => (IntPtr)((p.Y << 16) | (p.X & 0xFFFF));

        var start = new POINT { X = x1, Y = y1 };
        if (!ScreenToClient(hwnd, ref start))
            return false;

        _ = PostMessage(hwnd, downMsg, IntPtr.Zero, ToLParam(start));

        const int steps = 12;
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            var sx = (int)Math.Round(x1 + (x2 - x1) * t);
            var sy = (int)Math.Round(y1 + (y2 - y1) * t);
            var pt = new POINT { X = sx, Y = sy };
            if (!ScreenToClient(hwnd, ref pt))
                return false;
            _ = PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, ToLParam(pt));
        }

        var end = new POINT { X = x2, Y = y2 };
        if (!ScreenToClient(hwnd, ref end))
            return false;
        _ = PostMessage(hwnd, upMsg, IntPtr.Zero, ToLParam(end));

        note = $"dragged {button} from ({x1},{y1}) to ({x2},{y2}) [background PostMessage — OS mouse untouched]";
        return true;
    }

    /// <summary>
    /// Capture via GDI BitBlt into a 24-bpp BMP. <paramref name="monitor"/> is
    /// currently advisory (primary virtual screen); multi-monitor indexing is a
    /// follow-up.
    /// </summary>
    private static (string Path, int Width, int Height) CapturePrimaryMonitorBmp(int monitor)
    {
        _ = monitor; // reserved for multi-monitor selection
        var x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        var h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException($"invalid virtual screen size {w}x{h}");

        var hdcScreen = GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
            throw new InvalidOperationException("GetDC failed");

        var hdcMem = CreateCompatibleDC(hdcScreen);
        var hBmp = CreateCompatibleBitmap(hdcScreen, w, h);
        var old = SelectObject(hdcMem, hBmp);
        try
        {
            if (!BitBlt(hdcMem, 0, 0, w, h, hdcScreen, x, y, SRCCOPY))
                throw new InvalidOperationException("BitBlt failed");

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SoulCore", "scratch", "desktop");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"shot-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.bmp");
            WriteBitmapFile(path, hdcMem, hBmp, w, h);
            return (path, w, h);
        }
        finally
        {
            SelectObject(hdcMem, old);
            DeleteObject(hBmp);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    private static void WriteBitmapFile(string path, IntPtr hdc, IntPtr hBmp, int width, int height)
    {
        var bmi = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = height, // bottom-up
            biPlanes = 1,
            biBitCount = 24,
            biCompression = BI_RGB,
        };

        var stride = ((width * 3) + 3) & ~3;
        var imageSize = stride * height;
        var buf = new byte[imageSize];
        var handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
        try
        {
            var got = GetDIBits(hdc, hBmp, 0, (uint)height, handle.AddrOfPinnedObject(), ref bmi, DIB_RGB_COLORS);
            if (got == 0)
                throw new InvalidOperationException("GetDIBits failed");

            const int fileHeaderSize = 14;
            var infoSize = Marshal.SizeOf<BITMAPINFOHEADER>();
            var fileSize = fileHeaderSize + infoSize + imageSize;

            using var fs = File.Create(path);
            // BITMAPFILEHEADER
            fs.WriteByte((byte)'B');
            fs.WriteByte((byte)'M');
            WriteInt32(fs, fileSize);
            WriteInt16(fs, 0);
            WriteInt16(fs, 0);
            WriteInt32(fs, fileHeaderSize + infoSize);

            var hdr = new byte[infoSize];
            var hdrHandle = GCHandle.Alloc(hdr, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(bmi, hdrHandle.AddrOfPinnedObject(), false);
                fs.Write(hdr, 0, hdr.Length);
            }
            finally
            {
                hdrHandle.Free();
            }

            fs.Write(buf, 0, buf.Length);
        }
        finally
        {
            handle.Free();
        }
    }

    private static void WriteInt16(Stream s, short v)
    {
        s.WriteByte((byte)(v & 0xff));
        s.WriteByte((byte)((v >> 8) & 0xff));
    }

    private static void WriteInt32(Stream s, int v)
    {
        s.WriteByte((byte)(v & 0xff));
        s.WriteByte((byte)((v >> 8) & 0xff));
        s.WriteByte((byte)((v >> 16) & 0xff));
        s.WriteByte((byte)((v >> 24) & 0xff));
    }

    private static bool TryMapVirtualKey(string key, out ushort vk)
    {
        vk = key.ToLowerInvariant() switch
        {
            "enter" or "return" => 0x0D,
            "escape" or "esc" => 0x1B,
            "tab" => 0x09,
            "space" => 0x20,
            "backspace" => 0x08,
            "delete" or "del" => 0x2E,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" => 0x21,
            "pagedown" => 0x22,
            "f1" => 0x70,
            "f2" => 0x71,
            "f3" => 0x72,
            "f4" => 0x73,
            "f5" => 0x74,
            "f6" => 0x75,
            "f7" => 0x76,
            "f8" => 0x77,
            "f9" => 0x78,
            "f10" => 0x79,
            "f11" => 0x7A,
            "f12" => 0x7B,
            _ => (ushort)0,
        };
        return vk != 0;
    }

    private static INPUT MouseInput(uint flags) => new()
    {
        type = INPUT_MOUSE,
        U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags } },
    };

    private static INPUT UnicodeKey(char ch, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = ch,
                dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0),
            },
        },
    };

    private static INPUT VKey(ushort vk, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = 0,
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
            },
        },
    };

    // ----- Win32 -----

    private const int INPUT_MOUSE = 0;
    private const int INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint SRCCOPY = 0x00CC0020;
    private const uint BI_RGB = 0;
    private const uint DIB_RGB_COLORS = 0;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
    private const int SW_RESTORE = 9;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_MBUTTONDOWN = 0x0207;
    private const uint WM_MBUTTONUP = 0x0208;
    private const uint WM_MOUSEMOVE = 0x0200;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

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

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int w, int h, IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, IntPtr lpvBits, ref BITMAPINFOHEADER lpbi, uint uUsage);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }
}
