using System.Runtime.InteropServices;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Controls;

/// <summary>
/// Win32 layered child siblings of the SetParent'd VirtualBox HWND. Avalonia siblings
/// and owned popups can sit under VirtualBox's GPU surface — these paint the arrow
/// cursor and coords badge in the same parent with HWND_TOP Z-order.
/// Each window is only as big as what it draws. A full-client child with
/// WS_EX_TRANSPARENT delivers clicks to the Avalonia parent, which never forwards
/// them into the VM, so the guest looked click-dead.
/// </summary>
public sealed class HerScreenHwndOverlay : IDisposable
{
    private static readonly object ClassGate = new();
    private static bool _classRegistered;
    private static readonly WndProc WndProcKeepAlive = StaticWndProc;

    private nint _cursorHwnd;
    private nint _badgeHwnd;
    private nint _parent;
    private nint _clickTarget;
    private GCHandle _selfHandle;
    private bool _cursorVisible;
    private int _cursorX;
    private int _cursorY;
    private int _cursorSize = 28;
    private bool _clickFlash;
    private string? _badge;
    private bool _disposed;

    public bool IsAttached => _cursorHwnd != 0 && _parent != 0;

    public void Attach(nint parentHwnd, nint clickTarget)
    {
        if (!OperatingSystem.IsWindows() || parentHwnd == 0)
            return;

        _clickTarget = clickTarget;
        if (_cursorHwnd != 0 && _parent == parentHwnd)
        {
            SyncToParent();
            return;
        }

        Detach();
        if (!EnsureWindowClass())
            return;

        _parent = parentHwnd;
        _selfHandle = GCHandle.Alloc(this);
        _cursorHwnd = CreateOverlayWindow(parentHwnd, "HerScreenCursor");
        _badgeHwnd = CreateOverlayWindow(parentHwnd, "HerScreenBadge");
        if (_cursorHwnd == 0)
        {
            PresenceStartupLog.Write(
                $"HerScreenHwndOverlay CreateWindowEx failed err={Marshal.GetLastPInvokeError()}");
            Detach();
            return;
        }

        SyncToParent();
    }

    private nint CreateOverlayWindow(nint parentHwnd, string name)
    {
        var hwnd = CreateWindowEx(
            HerScreenOverlayWin32.ChildOverlayExStyle,
            WindowClassName,
            name,
            WsChild | WsVisible | WsClipSiblings,
            0,
            0,
            1,
            1,
            parentHwnd,
            nint.Zero,
            GetModuleHandle(null),
            nint.Zero);
        if (hwnd == 0)
            return 0;

        SetWindowLongPtr(hwnd, GwlpUserData, GCHandle.ToIntPtr(_selfHandle));
        _ = SetLayeredWindowAttributes(hwnd, ColorKeyMagenta, 0, LwaColorKey);
        return hwnd;
    }

    public void SyncToParent()
    {
        if (!OperatingSystem.IsWindows() || _cursorHwnd == 0 || _parent == 0)
            return;
        PositionCursorWindow();
        PositionBadgeWindow();
        RaiseAboveSiblings();
    }

    public void RaiseAboveSiblings()
    {
        if (!OperatingSystem.IsWindows())
            return;
        Raise(_cursorHwnd);
        Raise(_badgeHwnd);
    }

    /// <summary>Cursor tip (hotspot) in parent client pixels.</summary>
    public void SetSoftCursor(bool visible, int tipX, int tipY, int size, bool clickFlash)
    {
        _cursorVisible = visible;
        _cursorX = tipX;
        _cursorY = tipY;
        _cursorSize = Math.Clamp(size, 12, 72);
        _clickFlash = clickFlash;
        PositionCursorWindow();
    }

    public void SetBadge(string? text)
    {
        _badge = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        PositionBadgeWindow();
    }

    public void Hide()
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (_cursorHwnd != 0)
            _ = ShowWindow(_cursorHwnd, SwHide);
        if (_badgeHwnd != 0)
            _ = ShowWindow(_badgeHwnd, SwHide);
    }

    public void Detach()
    {
        if (OperatingSystem.IsWindows())
        {
            DestroyOverlayWindow(ref _cursorHwnd);
            DestroyOverlayWindow(ref _badgeHwnd);
        }

        _cursorHwnd = 0;
        _badgeHwnd = 0;
        _parent = 0;
        _clickTarget = 0;
        if (_selfHandle.IsAllocated)
            _selfHandle.Free();
    }

    private static void DestroyOverlayWindow(ref nint hwnd)
    {
        if (hwnd == 0)
            return;
        SetWindowLongPtr(hwnd, GwlpUserData, nint.Zero);
        _ = DestroyWindow(hwnd);
        hwnd = 0;
    }

    private static void Raise(nint hwnd)
    {
        if (hwnd == 0 || !IsWindowVisible(hwnd))
            return;
        _ = SetWindowPos(
            hwnd,
            HwndTop,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Detach();
    }

    private void Repaint(nint hwnd)
    {
        if (!OperatingSystem.IsWindows() || hwnd == 0)
            return;
        _ = InvalidateRect(hwnd, nint.Zero, true);
        _ = UpdateWindow(hwnd);
    }

    private void PositionCursorWindow()
    {
        if (!OperatingSystem.IsWindows() || _cursorHwnd == 0)
            return;
        if (!_cursorVisible)
        {
            _ = ShowWindow(_cursorHwnd, SwHide);
            return;
        }

        var bounds = HerScreenCursorGlyph.BoundsAt(_cursorX, _cursorY, _cursorSize);
        _ = SetWindowPos(
            _cursorHwnd,
            HwndTop,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            SwpShowWindow | SwpNoActivate);
        ApplyCursorRegion(bounds);
        Repaint(_cursorHwnd);
    }

    private void ApplyCursorRegion((int X, int Y, int Width, int Height) bounds)
    {
        var outline = HerScreenCursorGlyph.OutlineAt(_cursorX, _cursorY, _cursorSize);
        var local = new POINT[outline.Length];
        for (var i = 0; i < outline.Length; i++)
        {
            local[i] = new POINT
            {
                X = outline[i].X - bounds.X,
                Y = outline[i].Y - bounds.Y
            };
        }

        var region = CreatePolygonRgn(local, local.Length, Winding);
        if (region == 0)
            return;
        // SetWindowRgn takes ownership of the region.
        if (SetWindowRgn(_cursorHwnd, region, true) == 0)
            DeleteObject(region);
    }

    private void PositionBadgeWindow()
    {
        if (!OperatingSystem.IsWindows() || _badgeHwnd == 0)
            return;
        if (_badge is null)
        {
            _ = ShowWindow(_badgeHwnd, SwHide);
            return;
        }

        var hdc = GetDC(_badgeHwnd);
        if (hdc == 0)
            return;
        var font = CreateFont(
            -13, 0, 0, 0, 700, 0, 0, 0,
            1, 0, 0, ClearTypeQuality, 0, "Segoe UI");
        var oldFont = SelectObject(hdc, font);
        _ = GetTextExtentPoint32(hdc, _badge, _badge.Length, out var size);
        SelectObject(hdc, oldFont);
        DeleteObject(font);
        _ = ReleaseDC(_badgeHwnd, hdc);

        const int pad = 6;
        var width = Math.Max(24, size.cx + pad * 2);
        var height = Math.Max(16, size.cy + pad * 2);
        _ = SetWindowPos(
            _badgeHwnd,
            HwndTop,
            8,
            8,
            width,
            height,
            SwpShowWindow | SwpNoActivate);
        Repaint(_badgeHwnd);
    }

    private void Paint(nint hwnd, nint hdc)
    {
        if (!GetClientRect(hwnd, out var rc))
            return;
        var w = rc.Right - rc.Left;
        var h = rc.Bottom - rc.Top;
        if (w <= 1 || h <= 1)
            return;

        var keyBrush = CreateSolidBrush(ColorKeyMagenta);
        try
        {
            FillRect(hdc, ref rc, keyBrush);
        }
        finally
        {
            DeleteObject(keyBrush);
        }

        if (hwnd == _cursorHwnd && _cursorVisible)
            PaintCursor(hdc);
        else if (hwnd == _badgeHwnd && _badge is not null)
            PaintBadge(hdc, rc);
    }

    private void PaintCursor(nint hdc)
    {
        var bounds = HerScreenCursorGlyph.BoundsAt(_cursorX, _cursorY, _cursorSize);
        var outline = HerScreenCursorGlyph.OutlineAt(_cursorX, _cursorY, _cursorSize);
        var local = new POINT[outline.Length];
        for (var i = 0; i < outline.Length; i++)
        {
            local[i] = new POINT
            {
                X = outline[i].X - bounds.X,
                Y = outline[i].Y - bounds.Y
            };
        }

        var fill = _clickFlash ? ColorTealStroke : ColorPinkStroke;
        var stroke = ColorWhite;
        var fillBrush = CreateSolidBrush(fill);
        var pen = CreatePen(PsSolid, 2, stroke);
        var oldBrush = SelectObject(hdc, fillBrush);
        var oldPen = SelectObject(hdc, pen);
        Polygon(hdc, local, local.Length);
        SelectObject(hdc, oldBrush);
        SelectObject(hdc, oldPen);
        DeleteObject(fillBrush);
        DeleteObject(pen);
    }

    private void PaintBadge(nint hdc, RECT client)
    {
        if (_badge is null)
            return;
        var text = _badge;
        var font = CreateFont(
            -13, 0, 0, 0, 700, 0, 0, 0,
            1, 0, 0, ClearTypeQuality, 0, "Segoe UI");
        var oldFont = SelectObject(hdc, font);
        SetBkMode(hdc, Transparent);
        SetTextColor(hdc, ColorPinkStroke);
        const int pad = 6;
        var plateBrush = CreateSolidBrush(ColorPlate);
        var platePen = CreatePen(PsSolid, 1, ColorPinkStroke);
        var oldB = SelectObject(hdc, plateBrush);
        var oldP = SelectObject(hdc, platePen);
        RoundRect(hdc, 0, 0, client.Right, client.Bottom, 6, 6);
        SelectObject(hdc, oldB);
        SelectObject(hdc, oldP);
        DeleteObject(plateBrush);
        DeleteObject(platePen);
        TextOut(hdc, pad, pad, text, text.Length);
        SelectObject(hdc, oldFont);
        DeleteObject(font);
    }

    private void ForwardPointer(nint source, uint msg, nint wParam, nint lParam)
    {
        var target = _clickTarget;
        if (target == 0 || source == 0 || !IsWindow(target))
            return;

        var pt = new POINT { X = SignedLow(lParam), Y = SignedHigh(lParam) };
        _ = MapWindowPoints(source, target, ref pt, 1);

        var dest = DeepestChild(target, ref pt);
        var packed = MakeLParam(pt.X, pt.Y);
        if (msg is WmLButtonDown or WmRButtonDown or WmMButtonDown)
            _ = PostMessage(dest, WmMouseMove, nint.Zero, packed);
        _ = PostMessage(dest, msg, wParam, packed);
    }

    private static nint DeepestChild(nint hwnd, ref POINT pt)
    {
        const uint skip = CwpSkipInvisible | CwpSkipDisabled | CwpSkipTransparent;
        for (var i = 0; i < 12; i++)
        {
            var child = ChildWindowFromPointEx(hwnd, pt, skip);
            if (child == 0 || child == hwnd)
                return hwnd;
            _ = MapWindowPoints(hwnd, child, ref pt, 1);
            hwnd = child;
        }

        return hwnd;
    }

    private static int SignedLow(nint lParam) => (short)((long)lParam & 0xFFFF);

    private static int SignedHigh(nint lParam) => (short)(((long)lParam >> 16) & 0xFFFF);

    private static nint MakeLParam(int x, int y) =>
        (nint)((ushort)(short)x | ((uint)(ushort)(short)y << 16));

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
                        overlay.Paint(hWnd, hdc);
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

        if (msg is WmMouseMove or WmLButtonDown or WmLButtonUp or WmLButtonDblClk
            or WmRButtonDown or WmRButtonUp or WmMButtonDown or WmMButtonUp or WmMouseWheel)
        {
            try
            {
                var ptr = GetWindowLongPtr(hWnd, GwlpUserData);
                if (ptr != nint.Zero)
                {
                    var gch = GCHandle.FromIntPtr(ptr);
                    if (gch.IsAllocated && gch.Target is HerScreenHwndOverlay overlay)
                        overlay.ForwardPointer(hWnd, msg, wParam, lParam);
                }
            }
            catch
            {
                // never throw from WndProc
            }

            return nint.Zero;
        }

        if (msg == WmMouseActivate)
            return (nint)MaNoActivate;

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private const string WindowClassName = "House.HerScreenHwndOverlay.v1";
    private const int ErrorClassAlreadyExists = 1410;
    private const uint WmPaint = 0x000F;
    private const uint WmMouseActivate = 0x0021;
    private const uint WmMouseMove = 0x0200;
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmLButtonDblClk = 0x0203;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmMButtonUp = 0x0208;
    private const uint WmMouseWheel = 0x020A;
    private const int MaNoActivate = 3;
    private const int Winding = 2;
    private const uint CwpSkipInvisible = 0x0001;
    private const uint CwpSkipDisabled = 0x0002;
    private const uint CwpSkipTransparent = 0x0004;
    private const int GwlpUserData = -21;
    private const int WsChild = 0x40000000;
    private const int WsVisible = 0x10000000;
    private const int WsClipSiblings = 0x04000000;
    private const uint LwaColorKey = 0x00000001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly nint HwndTop = nint.Zero;
    private const int SwHide = 0;
    private const int Transparent = 1;
    private const int PsSolid = 0;
    private const uint ClearTypeQuality = 5;

    // COLORREF 0x00BBGGRR
    private const uint ColorKeyMagenta = 0x00FF00FF;
    private const uint ColorPinkStroke = 0x00552DFF;
    private const uint ColorTealStroke = 0x00B6C42E;
    private const uint ColorPlate = 0x000A0A0A;
    private const uint ColorWhite = 0x00FFFFFF;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
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
    private static extern bool Polygon(nint hdc, POINT[] apt, int cpt);

    [DllImport("gdi32.dll")]
    private static extern nint CreatePolygonRgn(POINT[] lppt, int cPoints, int fnPolyFillMode);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint hWnd, nint hRgn, bool bRedraw);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern int MapWindowPoints(nint hWndFrom, nint hWndTo, ref POINT lpPoints, uint cPoints);

    [DllImport("user32.dll")]
    private static extern nint ChildWindowFromPointEx(nint hwndParent, POINT pt, uint flags);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDC);

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
