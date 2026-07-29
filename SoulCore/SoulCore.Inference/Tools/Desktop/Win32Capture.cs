using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>Win32 GDI capture → 32-bpp BMP (no System.Drawing dependency).</summary>
[SupportedOSPlatform("windows")]
internal static class Win32Capture
{
    /// <summary>
    /// Capture monitor to a BMP file. <paramref name="monitor"/> 0 = virtual screen; 1..N = display index.
    /// Returns the written path (always <c>.bmp</c>).
    /// </summary>
    internal static string CaptureMonitor(int monitor, string preferredPath, out int width, out int height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredPath);

        RECT region;
        if (monitor == 0)
        {
            region = new RECT
            {
                Left = GetSystemMetrics(SM_XVIRTUALSCREEN),
                Top = GetSystemMetrics(SM_YVIRTUALSCREEN),
                Right = GetSystemMetrics(SM_XVIRTUALSCREEN) + GetSystemMetrics(SM_CXVIRTUALSCREEN),
                Bottom = GetSystemMetrics(SM_YVIRTUALSCREEN) + GetSystemMetrics(SM_CYVIRTUALSCREEN),
            };
        }
        else
        {
            var list = new List<RECT>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
                {
                    list.Add(rect);
                    return true;
                }, IntPtr.Zero);
            if (monitor < 1 || monitor > list.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(monitor),
                    $"monitor {monitor} out of range (1..{list.Count}; 0=virtual screen)");
            }

            region = list[monitor - 1];
        }

        width = Math.Max(1, region.Right - region.Left);
        height = Math.Max(1, region.Bottom - region.Top);

        var hdcScreen = GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
            throw new InvalidOperationException("GetDC failed");

        var hdcMem = CreateCompatibleDC(hdcScreen);
        var hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
        var old = SelectObject(hdcMem, hBitmap);
        var outPath = Path.ChangeExtension(preferredPath, ".bmp");
        try
        {
            if (!BitBlt(hdcMem, 0, 0, width, height, hdcScreen, region.Left, region.Top, SRCCOPY))
                throw new InvalidOperationException("BitBlt failed");
            WriteBmp(outPath, hBitmap, width, height);
            return outPath;
        }
        finally
        {
            SelectObject(hdcMem, old);
            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    private static void WriteBmp(string path, IntPtr hBitmap, int width, int height)
    {
        var bmi = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = height,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0,
        };
        var pixels = new byte[width * height * 4];
        var hdc = CreateCompatibleDC(IntPtr.Zero);
        try
        {
            GetDIBits(hdc, hBitmap, 0, (uint)height, pixels, ref bmi, DIB_RGB_COLORS);
        }
        finally
        {
            DeleteDC(hdc);
        }

        const int fileHeaderSize = 14;
        var infoSize = Marshal.SizeOf<BITMAPINFOHEADER>();
        var fileSize = fileHeaderSize + infoSize + pixels.Length;
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write((byte)'B');
        bw.Write((byte)'M');
        bw.Write(fileSize);
        bw.Write(0);
        bw.Write(fileHeaderSize + infoSize);
        bw.Write(bmi.biSize);
        bw.Write(bmi.biWidth);
        bw.Write(bmi.biHeight);
        bw.Write(bmi.biPlanes);
        bw.Write(bmi.biBitCount);
        bw.Write(bmi.biCompression);
        bw.Write(pixels.Length);
        bw.Write(0);
        bw.Write(0);
        bw.Write(0);
        bw.Write(0);
        bw.Write(pixels);
    }

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
    private const int SRCCOPY = 0x00CC0020;
    private const int DIB_RGB_COLORS = 0;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int w, int h, IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, byte[] lpvBits, ref BITMAPINFOHEADER lpbi, uint uUsage);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
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
