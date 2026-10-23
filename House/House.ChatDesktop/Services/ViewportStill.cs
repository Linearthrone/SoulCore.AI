using Avalonia.Media;

namespace House.ChatDesktop.Services;

/// <summary>
/// PROP-16.2b. The viewport picture is the desktop still inside the chat drawer.
/// A mismatched frame stays on <see cref="GuestEmbedFit.ScaleStop"/>. Nothing here parents an HWND.
/// </summary>
public static class ViewportStill
{
    public const string Label = "What she saw · still, not a live guest";

    public const double WindowOpacity = 1d;

    public const double BorderDip = 1d;

    public const bool HostsGuestHwnd = false;

    public const bool AllowsClickthroughOnStill = false;

    public const bool Frosted = false;

    public static Color FillColor { get; } = Color.FromRgb(0x11, 0x12, 0x14);

    public static Color BorderColor { get; } = Color.FromArgb(0xCC, 0xD8, 0xDE, 0xE8);

    public static SolidColorBrush CreateFillBrush() => new(FillColor);

    public static SolidColorBrush CreateBorderBrush() => new(BorderColor);

    public static LiveViewAction LiveView() => new(true, false, true);

    public static bool MayParent(int slotWidth, int slotHeight, int frameWidth, int frameHeight) =>
        Present(slotWidth, slotHeight, frameWidth, frameHeight, clickthroughRequested: false).ParentHwnd
        && HostsGuestHwnd;

    public static StillPlan Present(
        int slotWidth,
        int slotHeight,
        int frameWidth,
        int frameHeight,
        bool clickthroughRequested)
    {
        var fit = GuestEmbedFit.Decide(
            slotWidth,
            slotHeight,
            frameWidth,
            frameHeight,
            hostClickThrough: false,
            hostFrostedOrTransparent: false);
        return new StillPlan(
            ParentHwnd: false,
            Opaque: FillColor.A == 255,
            Frosted: Frosted,
            WindowOpacity: WindowOpacity,
            ClickthroughApplied: AllowsClickthroughOnStill && clickthroughRequested,
            ClickthroughRequested: clickthroughRequested,
            Label: Label,
            StopLine: fit.Allow ? null : fit.Detail);
    }
}

public readonly record struct LiveViewAction(
    bool FocusStillInChatDrawer,
    bool ParentHwnd,
    bool HerScreenStaysReachable);

public readonly record struct StillPlan(
    bool ParentHwnd,
    bool Opaque,
    bool Frosted,
    double WindowOpacity,
    bool ClickthroughApplied,
    bool ClickthroughRequested,
    string Label,
    string? StopLine);
