namespace House.ChatDesktop.Controls;

/// <summary>
/// Arrow pointer for Victoria's soft cursor. The hotspot is the tip at (0, 0);
/// the body extends down and to the right, the way a mouse cursor does.
/// </summary>
public static class HerScreenCursorGlyph
{
    public const double DesignWidth = 14;
    public const double DesignHeight = 22;

    /// <summary>Outline in design pixels. First point is the click hotspot.</summary>
    public static readonly (double X, double Y)[] Outline =
    [
        (0.0, 0.0),
        (0.0, 16.0),
        (4.4, 12.2),
        (7.2, 21.2),
        (10.4, 19.8),
        (7.4, 11.2),
        (13.2, 11.0),
    ];

    public const string GeometryData =
        "M0,0 L0,16 L4.4,12.2 L7.2,21.2 L10.4,19.8 L7.4,11.2 L13.2,11 Z";

    public static double WidthForHeight(double height) =>
        height * (DesignWidth / DesignHeight);

    /// <summary>Outline translated so the tip sits on <paramref name="tipX"/>,<paramref name="tipY"/>.</summary>
    public static (int X, int Y)[] OutlineAt(int tipX, int tipY, int heightPx)
    {
        var scale = Math.Max(8, heightPx) / DesignHeight;
        var pts = new (int X, int Y)[Outline.Length];
        for (var i = 0; i < Outline.Length; i++)
        {
            pts[i] = (
                tipX + (int)Math.Round(Outline[i].X * scale),
                tipY + (int)Math.Round(Outline[i].Y * scale));
        }

        return pts;
    }

    /// <summary>
    /// Child-window bounds whose top-left is the tip. A 1px pad keeps the stroke inside the window.
    /// </summary>
    public static (int X, int Y, int Width, int Height) BoundsAt(int tipX, int tipY, int heightPx)
    {
        var pts = OutlineAt(tipX, tipY, heightPx);
        var minX = pts.Min(p => p.X);
        var minY = pts.Min(p => p.Y);
        var maxX = pts.Max(p => p.X);
        var maxY = pts.Max(p => p.Y);
        const int pad = 2;
        return (
            minX - pad,
            minY - pad,
            Math.Max(1, maxX - minX + 1 + pad * 2),
            Math.Max(1, maxY - minY + 1 + pad * 2));
    }
}
