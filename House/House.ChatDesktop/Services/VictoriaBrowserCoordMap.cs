namespace House.ChatDesktop.Services;

/// <summary>
/// Maps pointer position over Her-screen to guest framebuffer / page pixels
/// for guidance (<c>desktop_click</c> / <c>browser_click</c>).
/// JPEG uses Uniform letterbox; VM HWND embed uses stretch-to-fill (matches MoveWindow).
/// </summary>
public static class VictoriaBrowserCoordMap
{
    /// <summary>Default guest size when embed is live but no screenshot has published frame size yet.</summary>
    public const int DefaultGuestWidth = 1280;
    public const int DefaultGuestHeight = 800;

    /// <summary>
    /// Uniform letterbox map (JPEG / Playwright frame). Null when pointer is in letterbox bands.
    /// </summary>
    public static (int X, int Y)? TryMapPointerToPage(
        double pointerX,
        double pointerY,
        double surfaceWidth,
        double surfaceHeight,
        int imagePixelWidth,
        int imagePixelHeight)
    {
        if (surfaceWidth <= 0 || surfaceHeight <= 0
            || imagePixelWidth <= 0 || imagePixelHeight <= 0)
            return null;

        var scale = Math.Min(surfaceWidth / imagePixelWidth, surfaceHeight / imagePixelHeight);
        if (scale <= 0)
            return null;

        var dispW = imagePixelWidth * scale;
        var dispH = imagePixelHeight * scale;
        var offsetX = (surfaceWidth - dispW) / 2.0;
        var offsetY = (surfaceHeight - dispH) / 2.0;

        if (pointerX < offsetX || pointerY < offsetY
            || pointerX > offsetX + dispW || pointerY > offsetY + dispH)
            return null;

        var x = (int)Math.Floor((pointerX - offsetX) / scale);
        var y = (int)Math.Floor((pointerY - offsetY) / scale);
        x = Math.Clamp(x, 0, imagePixelWidth - 1);
        y = Math.Clamp(y, 0, imagePixelHeight - 1);
        return (x, y);
    }

    /// <summary>
    /// Stretch-to-fill map (VM HWND embed fills the slot). Null when pointer is outside the surface.
    /// </summary>
    public static (int X, int Y)? TryMapPointerStretchFill(
        double pointerX,
        double pointerY,
        double surfaceWidth,
        double surfaceHeight,
        int imagePixelWidth,
        int imagePixelHeight)
    {
        if (surfaceWidth <= 0 || surfaceHeight <= 0
            || imagePixelWidth <= 0 || imagePixelHeight <= 0)
            return null;

        if (pointerX < 0 || pointerY < 0
            || pointerX > surfaceWidth || pointerY > surfaceHeight)
            return null;

        var x = (int)Math.Floor(pointerX / surfaceWidth * imagePixelWidth);
        var y = (int)Math.Floor(pointerY / surfaceHeight * imagePixelHeight);
        x = Math.Clamp(x, 0, imagePixelWidth - 1);
        y = Math.Clamp(y, 0, imagePixelHeight - 1);
        return (x, y);
    }

    /// <summary>Guest pixel (cx,cy) → surface point for soft-cursor draw (stretch-to-fill).</summary>
    public static (double Left, double Top)? TryMapGuestToSurfaceStretchFill(
        int guestX,
        int guestY,
        double surfaceWidth,
        double surfaceHeight,
        int imagePixelWidth,
        int imagePixelHeight,
        double cursorWidth,
        double cursorHeight)
    {
        if (surfaceWidth <= 0 || surfaceHeight <= 0
            || imagePixelWidth <= 0 || imagePixelHeight <= 0)
            return null;

        var left = (guestX / (double)imagePixelWidth) * surfaceWidth - (cursorWidth / 2);
        var top = (guestY / (double)imagePixelHeight) * surfaceHeight - (cursorHeight / 2);
        return (left, top);
    }

    /// <summary>Guest pixel → surface point for soft-cursor draw (Uniform letterbox).</summary>
    public static (double Left, double Top)? TryMapGuestToSurfaceUniform(
        int guestX,
        int guestY,
        double surfaceWidth,
        double surfaceHeight,
        int imagePixelWidth,
        int imagePixelHeight,
        double cursorWidth,
        double cursorHeight)
    {
        if (surfaceWidth <= 0 || surfaceHeight <= 0
            || imagePixelWidth <= 0 || imagePixelHeight <= 0)
            return null;

        var scale = Math.Min(surfaceWidth / imagePixelWidth, surfaceHeight / imagePixelHeight);
        if (scale <= 0)
            return null;

        var drawW = imagePixelWidth * scale;
        var drawH = imagePixelHeight * scale;
        var offsetX = (surfaceWidth - drawW) / 2;
        var offsetY = (surfaceHeight - drawH) / 2;
        var left = offsetX + (guestX * scale) - (cursorWidth / 2);
        var top = offsetY + (guestY * scale) - (cursorHeight / 2);
        return (left, top);
    }

    public static string FormatClickHint(int x, int y) => $"click ({x}, {y})";
}
