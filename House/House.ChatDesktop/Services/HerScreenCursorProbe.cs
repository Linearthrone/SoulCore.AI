using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace House.ChatDesktop.Services;

/// <summary>
/// Polls the system cursor against Her screen surface bounds without stealing Win32 input
/// from an embedded VirtualBox HWND (Avalonia PointerMoved never fires over the child).
/// </summary>
public static class HerScreenCursorProbe
{
    /// <summary>
    /// Returns pointer position in surface-local coordinates when the system cursor is over
    /// <paramref name="surface"/>; otherwise null.
    /// </summary>
    public static Point? TryGetPointerInSurface(Control surface)
    {
        if (!OperatingSystem.IsWindows() || surface is null)
            return null;

        var bounds = surface.Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1)
            return null;

        if (!GetCursorPos(out var pt))
            return null;

        PixelPoint topLeft;
        PixelPoint bottomRight;
        try
        {
            topLeft = surface.PointToScreen(new Point(0, 0));
            bottomRight = surface.PointToScreen(new Point(bounds.Width, bounds.Height));
        }
        catch
        {
            return null;
        }

        var left = Math.Min(topLeft.X, bottomRight.X);
        var right = Math.Max(topLeft.X, bottomRight.X);
        var top = Math.Min(topLeft.Y, bottomRight.Y);
        var bottom = Math.Max(topLeft.Y, bottomRight.Y);

        if (pt.X < left || pt.X > right || pt.Y < top || pt.Y > bottom)
            return null;

        var localX = (pt.X - left) / (double)Math.Max(1, right - left) * bounds.Width;
        var localY = (pt.Y - top) / (double)Math.Max(1, bottom - top) * bounds.Height;
        return new Point(localX, localY);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);
}
