namespace House.ChatDesktop.Services;

/// <summary>
/// VM view on the top edge of the work area.
/// Closed, only the tab is placed. Open, the drawer hangs down from that same edge.
/// </summary>
public static class ViewportEdgeLayout
{
    public const double ClosedTabWidthDip = 120;
    public const double ClosedTabHeightDip = 36;
    public const double OpenHeaderHeightDip = 132;
    public const double DragOpenDip = 16;

    public readonly record struct Placement(
        bool Open,
        int TabX,
        int TabY,
        int TabWidthPx,
        int TabHeightPx,
        bool DrawerVisible,
        int DrawerX,
        int DrawerY,
        int DrawerWidthPx,
        int DrawerHeightPx);

    public static Placement Place(int areaX, int areaY, int areaWidth, int areaHeight, bool open, double scale)
    {
        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale))
            scale = 1;
        areaWidth = Math.Max(1, areaWidth);
        areaHeight = Math.Max(1, areaHeight);

        var tabW = Math.Max(1, (int)Math.Round(ClosedTabWidthDip * scale));
        var tabH = Math.Max(1, (int)Math.Round(ClosedTabHeightDip * scale));
        tabW = Math.Min(tabW, areaWidth);
        tabH = Math.Min(tabH, areaHeight);
        var tabX = areaX + Math.Max(0, (areaWidth - tabW) / 2);

        if (!open)
            return new Placement(false, tabX, areaY, tabW, tabH, false, 0, 0, 0, 0);

        var quarter = ViewportLayout.OfWorkArea(areaX, areaY, areaWidth, areaHeight);
        var headerH = Math.Max(tabH, (int)Math.Round(OpenHeaderHeightDip * scale));
        headerH = Math.Min(headerH, Math.Max(tabH, quarter.HeightPx - 1));
        var drawerW = quarter.WidthPx;
        var drawerX = areaX + Math.Max(0, (areaWidth - drawerW) / 2);
        var bodyH = Math.Max(1, quarter.HeightPx - headerH);
        return new Placement(
            true,
            drawerX,
            areaY,
            drawerW,
            headerH,
            true,
            drawerX,
            areaY + headerH,
            drawerW,
            bodyH);
    }

    public static bool DragPullsOpen(double deltaYDip) => deltaYDip >= DragOpenDip;

    public static bool CountsAsDrag(double deltaYDip) => Math.Abs(deltaYDip) >= DragOpenDip;
}
