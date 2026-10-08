namespace House.ChatDesktop.Services;

/// <summary>
/// Her-screen column sizing helpers. Side pane uses Pixel width; an Avalonia-only
/// east grip (not covered by the VM HWND) expands side + window together.
/// </summary>
public static class PresencePaneLayout
{
    public const double EastGripWidth = 10;

    /// <summary>
    /// Minimum window width that still fits chat min + splitter + side + east grip + margins.
    /// </summary>
    public static double MinWindowWidthForSide(double sideWidth)
    {
        var side = Math.Clamp(
            sideWidth,
            LocalUiSettings.MinSideColumnWidth,
            LocalUiSettings.MaxSideColumnWidth);
        return Math.Max(
            LocalUiSettings.MinWindowWidth,
            LocalUiSettings.MinChatWidth + 6 + side + EastGripWidth + 24);
    }
}
