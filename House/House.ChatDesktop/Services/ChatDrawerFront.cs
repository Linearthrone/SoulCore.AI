using Avalonia;
using Avalonia.Controls;

namespace House.ChatDesktop.Services;

/// <summary>Windows re-entered into the topmost band when the chat drawer opens. The last one paints in front.</summary>
public enum GlassTopmostWindow
{
    Radial,
    VmTab,
    Chat
}

/// <summary>
/// Puts the chat drawer on the work area and in front of the topmost radial and VM tab.
/// </summary>
public static class ChatDrawerFront
{
    public static IReadOnlyList<GlassTopmostWindow> TopmostRaiseOrder { get; } =
    [
        GlassTopmostWindow.Radial,
        GlassTopmostWindow.VmTab,
        GlassTopmostWindow.Chat
    ];

    public static bool PaintsInFrontOf(GlassTopmostWindow front, GlassTopmostWindow behind)
    {
        var frontAt = -1;
        var behindAt = -1;
        for (var i = 0; i < TopmostRaiseOrder.Count; i++)
        {
            if (TopmostRaiseOrder[i] == front)
                frontAt = i;
            if (TopmostRaiseOrder[i] == behind)
                behindAt = i;
        }

        return frontAt >= 0 && frontAt > behindAt;
    }

    /// <summary>
    /// Toggles topmost off and on so the platform inserts this window at the front of the topmost band.
    /// Setting the flag when it is already true does not raise the window again.
    /// </summary>
    public static void ReenterTopmost(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.Topmost)
            window.Topmost = false;
        window.Topmost = true;
    }

    /// <summary>
    /// When the window misses every work area, returns a point that centers it on the first area.
    /// Returns null when it already intersects a work area, or when no work area is known.
    /// </summary>
    public static PixelPoint? MoveOntoWorkArea(
        PixelPoint position,
        PixelSize size,
        IReadOnlyList<PixelRect> workAreas)
    {
        if (workAreas.Count == 0)
            return null;

        var width = Math.Max(1, size.Width);
        var height = Math.Max(1, size.Height);
        var fitted = new PixelSize(width, height);
        var bounds = new PixelRect(position, fitted);
        if (PresenceWindowPlacement.IntersectsAnyScreen(bounds, workAreas))
            return null;

        return PresenceWindowPlacement.CenterOnPrimary(fitted, workAreas);
    }
}
