namespace House.ChatDesktop.Services;

/// <summary>
/// Chat vs Her-screen column star weights. Both columns must be <c>*</c> so window
/// east-resize and the GridSplitter can widen the VM embed (a Pixel side column cannot).
/// </summary>
public static class PresencePaneLayout
{
    /// <summary>
    /// Returns star weights (chat, side) that approximate <paramref name="sideWidth"/>
    /// inside a window of <paramref name="windowWidth"/> (splitter gutter excluded).
    /// </summary>
    public static (double ChatStar, double SideStar) StarWeightsForSideWidth(
        double sideWidth,
        double windowWidth)
    {
        var side = Math.Clamp(sideWidth, LocalUiSettings.MinSideColumnWidth, LocalUiSettings.MaxSideColumnWidth);
        var usable = Math.Max(windowWidth - 6, LocalUiSettings.MinChatWidth + LocalUiSettings.MinSideColumnWidth);
        var chat = Math.Max(LocalUiSettings.MinChatWidth, usable - side);
        // Normalize so weights stay in a friendly range for GridSplitter.
        var total = chat + side;
        if (total <= 0)
            return (1, 1);
        return (chat / total * 100.0, side / total * 100.0);
    }
}
