using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace House.ChatDesktop.Services;

/// <summary>
/// Copies pixels from an existing window with PrintWindow, then BitBlt.
/// The window stays where it is. This does not parent it, move it, or inject a click.
/// About 5 frames a second is enough for the VM still.
/// </summary>
public static class GuestWindowGrab
{
    public const int FramesPerSecond = 5;

    public const int IntervalMs = 1000 / FramesPerSecond;

    public const bool ParentsHwnd = false;

    public const bool MovesWindow = false;

    public const bool InjectsClicks = false;

    public static byte[]? TryBmp(nint hwnd)
    {
        if (!OperatingSystem.IsWindows() || hwnd == 0 || !IsWindow(hwnd))
            return null;

        if (!GetWindowRect(hwnd, out var rect))
            return null;

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0 || width > 8192 || height > 8192)
            return null;

        nint screen = 0;
        nint memory = 0;
        nint bitmap = 0;
        nint previous = 0;
        try
        {
            screen = GetDC(0);
            if (screen == 0)
                return null;
            memory = CreateCompatibleDC(screen);
            if (memory == 0)
                return null;
            bitmap = CreateCompatibleBitmap(screen, width, height);
            if (bitmap == 0)
                return null;
            previous = SelectObject(memory, bitmap);

            var copied = PrintWindow(hwnd, memory, PrintFullContent);
            if (!copied)
            {
                var windowDc = GetWindowDC(hwnd);
                if (windowDc != 0)
                {
                    copied = BitBlt(memory, 0, 0, width, height, windowDc, 0, 0, SrcCopy);
                    _ = ReleaseDC(hwnd, windowDc);
                }
            }

            if (!copied)
                return null;

            // GetDIBits refuses a bitmap that is still selected into a DC.
            _ = SelectObject(memory, previous);
            previous = 0;
            return ReadBmp(memory, bitmap, width, height);
        }
        finally
        {
            if (memory != 0 && previous != 0)
                _ = SelectObject(memory, previous);
            if (bitmap != 0)
                _ = DeleteObject(bitmap);
            if (memory != 0)
                _ = DeleteDC(memory);
            if (screen != 0)
                _ = ReleaseDC(0, screen);
        }
    }

    /// <summary>
    /// Creates a small window, copies it, and checks the parent did not change.
    /// No SetParent, no MoveWindow, no click.
    /// </summary>
    public static GrabProbe Probe()
    {
        if (!OperatingSystem.IsWindows())
            return new GrabProbe(false, false, false, 0, 0, 0, "Probe is Windows-only.");

        lock (ProbeGate)
            return ProbeCore();
    }

    public static bool IsBmp(byte[]? bytes) =>
        bytes is { Length: > 54 } && bytes[0] == (byte)'B' && bytes[1] == (byte)'M';

    private static GrabProbe ProbeCore()
    {
        nint hwnd = 0;
        var registered = false;
        try
        {
            var instance = GetModuleHandle(null);
            var wc = new WndClass
            {
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(ProbeProc),
                hInstance = instance,
                lpszClassName = ProbeClass
            };
            if (RegisterClass(ref wc) != 0)
                registered = true;
            else if (Marshal.GetLastPInvokeError() != ErrorClassAlreadyExists)
                return new GrabProbe(false, false, false, 0, 0, 0, "RegisterClass failed: " + Marshal.GetLastPInvokeError());

            hwnd = CreateWindowEx(
                0,
                ProbeClass,
                "guest-grab-probe",
                WsPopup | WsVisible,
                80,
                80,
                64,
                48,
                0,
                0,
                instance,
                0);
            if (hwnd == 0)
                return new GrabProbe(false, false, false, 0, 0, 0, "CreateWindowEx failed: " + Marshal.GetLastPInvokeError());

            var parentBefore = GetParent(hwnd);
            var bmp = TryBmp(hwnd);
            var parentAfter = GetParent(hwnd);
            var parentHeld = parentBefore == parentAfter;
            var isBmp = IsBmp(bmp);
            var detail =
                $"PrintWindow/BitBlt copied {bmp?.Length ?? 0} bytes. " +
                $"Parent before {parentBefore} after {parentAfter}. " +
                "The window was not parented, moved, or clicked.";
            return new GrabProbe(true, parentHeld, isBmp, parentBefore, parentAfter, bmp?.Length ?? 0, detail);
        }
        finally
        {
            if (hwnd != 0)
                _ = DestroyWindow(hwnd);
            if (registered)
                _ = UnregisterClass(ProbeClass, GetModuleHandle(null));
        }
    }

    private static byte[]? ReadBmp(nint memory, nint bitmap, int width, int height)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        var info = new BitmapInfo
        {
            biSize = 40,
            biWidth = width,
            biHeight = height,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0,
            biSizeImage = pixels.Length
        };
        var lines = GetDIBits(memory, bitmap, 0, (uint)height, pixels, ref info, 0);
        if (lines == 0)
            return null;

        var file = new byte[54 + pixels.Length];
        file[0] = (byte)'B';
        file[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(2), file.Length);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(22), height);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(28), 32);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(34), pixels.Length);
        pixels.CopyTo(file.AsSpan(54));
        return file;
    }

    private const string ProbeClass = "SoulCoreGuestGrabProbe";
    private const int ErrorClassAlreadyExists = 1410;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsVisible = 0x10000000;
    private const uint PrintFullContent = 0x2;
    private const uint SrcCopy = 0x00CC0020;

    private static readonly object ProbeGate = new();
    private static readonly WndProcDelegate ProbeProc = ProbeWndProc;

    private static nint ProbeWndProc(nint hWnd, uint msg, nint wParam, nint lParam) =>
        DefWindowProc(hWnd, msg, wParam, lParam);

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClass
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

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
        public uint colors;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WndClass lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string lpClassName, nint hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint GetParent(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint GetWindowDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDc);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(nint hWnd, nint hdcBlt, uint nFlags);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleBitmap(nint hdc, int cx, int cy);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint h);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(
        nint hdc,
        int x,
        int y,
        int cx,
        int cy,
        nint hdcSrc,
        int x1,
        int y1,
        uint rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint ho);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        nint hdc,
        nint hbm,
        uint start,
        uint cLines,
        byte[] lpvBits,
        ref BitmapInfo lpbmi,
        uint usage);
}

public readonly record struct GrabProbe(
    bool Ran,
    bool ParentUnchanged,
    bool CopiedBmp,
    nint ParentBefore,
    nint ParentAfter,
    int ByteLength,
    string Detail);
