namespace House.ChatDesktop.Services;

/// <summary>
/// Chat on the right edge of the work area.
/// Closed, only the 36×120 tab is placed, vertically centered, and it stays on screen.
/// Open, the body is the right half of the work area and hangs to the left of that tab.
/// The tab stays on the right edge as the header. This is the same window, not a second chat.
/// </summary>
public static class ChatEdgeLayout
{
    public const double ClosedTabWidthDip = 36;
    public const double ClosedTabHeightDip = 120;
    public const double DragOpenDip = 16;
    public const double CornerRadiusDip = 10;

    public readonly record struct Placement(
        bool Open,
        int X,
        int Y,
        int WidthPx,
        int HeightPx,
        int TabX,
        int TabY,
        int TabWidthPx,
        int TabHeightPx,
        int BodyX,
        int BodyY,
        int BodyWidthPx,
        int BodyHeightPx)
    {
        public bool TabStaysVisible => TabWidthPx > 0 && TabHeightPx > 0;
    }

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
        var right = areaX + areaWidth;

        if (!open)
        {
            var x = right - tabW;
            var y = areaY + Math.Max(0, (areaHeight - tabH) / 2);
            return new Placement(false, x, y, tabW, tabH, x, y, tabW, tabH, x, y, 0, 0);
        }

        var half = Math.Max(1, areaWidth / 2);
        var bodyW = half;
        var windowW = Math.Min(areaWidth, bodyW + tabW);
        bodyW = Math.Max(1, windowW - tabW);
        var height = areaHeight;
        var xOpen = right - windowW;
        var tabX = xOpen + bodyW;
        return new Placement(
            true,
            xOpen,
            areaY,
            windowW,
            height,
            tabX,
            areaY,
            tabW,
            height,
            xOpen,
            areaY,
            bodyW,
            height);
    }

    /// <summary>A drag toward the center is leftward (negative X) by at least <see cref="DragOpenDip"/>.</summary>
    public static bool DragPullsOpen(double deltaXDip) => deltaXDip <= -DragOpenDip;

    public static bool CountsAsDrag(double deltaXDip) => Math.Abs(deltaXDip) >= DragOpenDip;
}

/// <summary>
/// How the one chat window opens and closes. The closed state is the right-edge tab.
/// It does not hide the only window, and it does not quit.
/// </summary>
public static class ChatDrawerPolicy
{
    public const bool FirstLaunchIsClosedTab = true;
    public const bool UsesSavedFloatingRect = false;
    public const bool HideWindowWhenClosed = false;
    public const bool QuitWhenClosed = false;
    public const bool HideOnOpen = false;

    public static bool ShouldStartClosed() => FirstLaunchIsClosedTab;

    public static bool ShouldUseSavedFloatingRect() => UsesSavedFloatingRect;

    public static bool ShouldHideWindowWhenClosed() => HideWindowWhenClosed;

    public static bool ShouldQuitWhenClosed() => QuitWhenClosed;

    public static bool ShouldHideOnOpen() => HideOnOpen;
}
