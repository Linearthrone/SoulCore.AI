using System.Runtime.InteropServices;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Controls;

/// <summary>
/// Win32 layered child sibling of the SetParent'd VirtualBox HWND. Avalonia siblings
/// and owned popups can sit under VirtualBox's GPU surface — this paints pink/teal
/// soft cursor + coords badge in the same parent with HWND_TOP Z-order.
/// Magenta color-key = transparent; WS_EX_TRANSPARENT keeps clicks on the VM.
/// </summary>
public sealed class HerScreenHwndOverlay : IDisposable
{
    private static readonly object ClassGate = new();
    private static bool _classRegistered;
    private static readonly WndProc WndProcKeepAlive = StaticWndProc;

    private nint _hwnd;
    private nint _parent;
    private GCHandle _selfHandle;
    private bool _cursorVisible;
    private int _cursorX;
    private int _cursorY;
    private int _cursorSize = 28;
    private bool _clickFlash;
    private string? _badge;
    private bool _disposed;

    public bool IsAttached => _hwnd != 0 && _parent != 0;

    public void Attach(nint parentHwnd)
    {
        if (!OperatingSystem.IsWindows() || parentHwnd == 0)
            return;

        if (_hwnd != 0 && _parent == parentHwnd)
        {
            SyncToParent();
            return;
        }

        Detach();
        if (!EnsureWindowClass())
            return;

        _parent = parentHwnd;
        _hwnd = CreateWindowEx(
            WsExLayered | WsExTransparent | WsExNoActivate,
            WindowClassName,
            "HerScreenOverlay",
            WsChild | WsVisible | WsClipSiblings,
            0,
            0,
            1,
            1,
            parentHwnd,
            nint.Zero,
            GetModuleHandle(null),
            nint.Zero);

        if (_hwnd == 0)
        {
            PresenceStartupLog.Write(
                $"HerScreenHwndOverlay CreateWindowEx failed err={Marshal.GetLastPInvokeError()}");
            _parent = 0;
            return;
        }

        _selfHandle = GCHandle.Alloc(this);
        SetWindowLongPtr(_hwnd, GwlpUserData, GCHandle.ToIntPtr(_selfHandle));
        _ = SetLayeredWindowAttributes(_hwnd, ColorKeyMagenta, 0, LwaColorKey);
        SyncToParent();
    }

    public void SyncToParent()
    {
        if (!OperatingSystem.IsWindows() || _hwnd == 0 || _parent == 0)
            return;
        if (!GetClientRect(_parent, out var rc))
            return;
        var w = rc.Right - rc.Left;
        var h = rc.Bottom - rc.Top;
        if (w <= 1 || h <= 1)
            return;

        _ = SetWindowPos(_hwnd, HwndTop, 0, 0, w, h, SwpShowWindow | SwpNoActivate);
        RaiseAboveSiblings();
        Repaint();
    }

    public void RaiseAboveSiblings()
    {
        if (!OperatingSystem.IsWindows() || _hwnd == 0)
            return;
        _ = SetWindowPos(
            _hwnd,
            HwndTop,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpShowWindow | SwpNoActivate);
    }

    /// <summary>Cursor center in parent client pixels.</summary>
    public void SetSoftCursor(bool visible, int centerX, int centerY, int size, bool clickFlash)
    {
        _cursorVisible = visible;
        _cursorX = centerX;
        _cursorY = centerY;
        _cursorSize = Math.Clamp(size, 12, 72);
        _clickFlash = clickFlash;
        if (_hwnd != 0)
            _ = ShowWindow(_hwnd, SwShow);
        Repaint();
    }

    public void SetBadge(string? text)
    {
        _badge = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (_hwnd != 0 && (_badge is not null || _cursorVisible))
            _ = ShowWindow(_hwnd, SwShow);
        Repaint();
    }

    public void Hide()
    {
        if (!OperatingSystem.IsWindows() || _hwnd == 0)
            return;
        _ = ShowWindow(_hwnd, SwHide);
    }

    public void Detach()
    {
        if (_hwnd != 0 && OperatingSystem.IsWindows())
        {
            SetWindowLongPtr(_hwnd, GwlpUserData, nint.Zero);
            _ = DestroyWindow(_hwnd);
        }

        _hwnd = 0;
        _parent = 0;
        if (_selfHandle.IsAllocated)
            _selfHandle.Free();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Detach();
    }

    private void Repaint()
    {
        if (!OperatingSystem.IsWindows() || _hwnd == 0)
            return;
        _ = InvalidateRect(_hwnd, nint.Zero, true);
        _ = UpdateWindow(_hwnd);
    }

    private void Paint(nint hdc)
    {
        if (!GetClientRect(_hwnd, out var rc))
            return;
        var w = rc.Right - rc.Left;
        var h = rc.Bottom - rc.Top;
        if (w <= 1 || h <= 1)
            return;

        FillRect(hdc, ref rc, GetStockObject(BlackBrush)); // overwritten by color-key fill
        var keyBrush = CreateSolidBrush(ColorKeyMagenta);
        try
        {
            FillRect(hdc, ref rc, keyBrush);
        }
        finally
        {
            DeleteObject(keyBrush);
        }

        if (_cursorVisible)
        {
            var half = _cursorSize / 2;
            var left = _cursorX - half;
            var top = _cursorY - half;
            var right = left + _cursorSize;
            var bottom = top + _cursorSize;
            var fill = _clickFlash ? ColorTealFill : ColorPinkFill;
            var stroke = _clickFlash ? ColorTealStroke : ColorPinkStroke;
            var fillBrush = CreateSolidBrush(fill);
            var pen = CreatePen(PsSolid, 3, stroke);
            var oldBrush = SelectObject(hdc, fillBrush);
            var oldPen = SelectObject(hdc, pen);
            Ellipse(hdc, left, top, right, bottom);
            SelectObject(hdc, oldBrush);
            SelectObject(hdc, oldPen);
            DeleteObject(fillBrush);
            DeleteObject(pen);
        }

        if (_badge is null)
            return;

        var text = _badge;
        var font = CreateFont(
            -13, 0, 0, 0, 700, 0, 0, 0,
            1, 0, 0, ClearTypeQuality, 0, "Segoe UI");
        var oldFont = SelectObject(hdc, font);
        SetBkMode(hdc, Transparent);
        SetTextColor(hdc, ColorPinkStroke);
        _ = GetTextExtentPoint32(hdc, text, text.Length, out var size);
        const int pad = 6;
        var plate = new RECT
        {
            Left = 8,
            Top = 8,
            Right = 8 + size.cx + pad * 2,
            Bottom = 8 + size.cy + pad * 2
        };
        var plateBrush = CreateSolidBrush(ColorPlate);
        var platePen = CreatePen(PsSolid, 1, ColorPinkStroke);
        var oldB = SelectObject(hdc, plateBrush);
        var oldP = SelectObject(hdc, platePen);
        RoundRect(hdc, plate.Left, plate.Top, plate.Right, plate.Bottom, 6, 6);
        SelectObject(hdc, oldB);
        SelectObject(hdc, oldP);
        DeleteObject(plateBrush);
        DeleteObject(platePen);
        TextOut(hdc, plate.Left + pad, plate.Top + pad, text, text.Length);
        SelectObject(hdc, oldFont);
        DeleteObject(font);
    }

    private static bool EnsureWindowClass()
    {
        if (!OperatingSystem.IsWindows())
            return false;
        lock (ClassGate)
        {
            if (_classRegistered)
                return true;

            var wc = new WNDCLASS
            {
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WndProcKeepAlive),
                hInstance = GetModuleHandle(null),
                lpszClassName = WindowClassName
            };
            var atom = RegisterClass(ref wc);
            if (atom == 0 && Marshal.GetLastPInvokeError() != ErrorClassAlreadyExists)
            {
                PresenceStartupLog.Write(
                    $"HerScreenHwndOverlay RegisterClass failed err={Marshal.GetLastPInvokeError()}");
                return false;
            }

            _classRegistered = true;
            return true;
        }
    }

    private static nint StaticWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WmPaint)
        {
            var hdc = BeginPaint(hWnd, out var ps);
            try
            {
                var ptr = GetWindowLongPtr(hWnd, GwlpUserData);
                if (ptr != nint.Zero)
                {
                    var gch = GCHandle.FromIntPtr(ptr);
                    if (gch.IsAllocated && gch.Target is HerScreenHwndOverlay overlay)
                        overlay.Paint(hdc);
                }
            }
            catch
            {
                // never throw from WndProc
            }
            finally
            {
                EndPaint(hWnd, ref ps);
            }

            return nint.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private const string WindowClassName = "House.HerScreenHwndOverlay.v1";
    private const int ErrorClassAlreadyExists = 1410;
    private const uint WmPaint = 0x000F;
    private const int GwlpUserData = -21;
    private const int WsChild = 0x40000000;
    private const int WsVisible = 0x10000000;
    private const int WsClipSiblings = 0x04000000;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const uint LwaColorKey = 0x00000001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly nint HwndTop = nint.Zero;
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const int Transparent = 1;
    private const int PsSolid = 0;
    private const int BlackBrush = 4;
    private const uint ClearTypeQuality = 5;

    // COLORREF 0x00BBGGRR
    private const uint ColorKeyMagenta = 0x00FF00FF;
    private const uint ColorPinkStroke = 0x00552DFF;
    private const uint ColorPinkFill = 0x007A55AA;
    private const uint ColorTealStroke = 0x00B6C42E;
    private const uint ColorTealFill = 0x0090B070;
    private const uint ColorPlate = 0x000A0A0A;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx, cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public nint hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore;
        public int fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    private delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
        int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(nint hWnd, nint lpRect, bool bErase);

    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint BeginPaint(nint hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(nint hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("gdi32.dll")]
    private static extern nint CreateSolidBrush(uint color);

    [DllImport("gdi32.dll")]
    private static extern nint CreatePen(int style, int width, uint color);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateFont(
        int cHeight, int cWidth, int cEscapement, int cOrientation, int cWeight,
        uint bItalic, uint bUnderline, uint bStrikeOut, uint iCharSet,
        uint iOutPrecision, uint iClipPrecision, uint iQuality, uint iPitchAndFamily,
        string pszFaceName);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint ho);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint h);

    [DllImport("gdi32.dll")]
    private static extern nint GetStockObject(int i);

    [DllImport("gdi32.dll")]
    private static extern bool Ellipse(nint hdc, int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern bool RoundRect(nint hdc, int left, int top, int right, int bottom, int width, int height);

    [DllImport("user32.dll")]
    private static extern int FillRect(nint hdc, ref RECT lprc, nint hbr);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(nint hdc, int mode);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(nint hdc, uint color);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool TextOut(nint hdc, int x, int y, string lpString, int c);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetTextExtentPoint32(nint hdc, string lpString, int c, out SIZE psizl);
}
