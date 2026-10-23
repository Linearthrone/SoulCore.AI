using Avalonia;
using Avalonia.Controls;

namespace House.ChatDesktop.Services;

public static class DrawerDismiss
{
    /// <summary>True when less than a quarter of the window still sits on a working area.</summary>
    public static bool IsMostlyOffScreen(Window window)
    {
        var screens = window.Screens?.All;
        if (screens is null || screens.Count == 0)
            return false;

        var scale = window.RenderScaling <= 0 ? 1 : window.RenderScaling;
        var rect = new PixelRect(
            window.Position.X,
            window.Position.Y,
            Math.Max(1, (int)(window.Bounds.Width * scale)),
            Math.Max(1, (int)(window.Bounds.Height * scale)));
        var area = Math.Max(1, rect.Width * (long)rect.Height);
        long visible = 0;
        foreach (var screen in screens)
            visible += OverlapArea(rect, screen.WorkingArea);
        return visible * 4 < area;
    }

    private static long OverlapArea(PixelRect a, PixelRect b)
    {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
        var w = x2 - x1;
        var h = y2 - y1;
        if (w <= 0 || h <= 0)
            return 0;
        return w * (long)h;
    }
}
