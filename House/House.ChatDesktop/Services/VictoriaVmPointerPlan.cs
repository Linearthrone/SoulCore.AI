namespace House.ChatDesktop.Services;

/// <summary>
/// Maps a Her-screen pointer event into the VirtualBox absolute pointer.
/// <c>putMouseEventAbsolute</c> is pixel-based with the origin at (1,1).
/// Wheel <c>dz</c> is one notch per Windows detent (<c>HIWORD / 120</c>).
/// </summary>
public static class VictoriaVmPointerPlan
{
    public const int Left = 0x01;
    public const int Right = 0x02;
    public const int Middle = 0x04;
    public const int LockTypeShared = 1;

    /// <summary>One Windows wheel detent. Guest <c>dz</c> stays in notches, not this unit.</summary>
    public const int WindowsWheelDetent = 120;

    public static (int X, int Y) ToAbsolute(int guestX, int guestY) =>
        (Math.Max(0, guestX) + 1, Math.Max(0, guestY) + 1);

    /// <summary>
    /// Mouse Integration keeps rewriting the guest pointer while the Windows cursor
    /// sits over Presence. A button change has to be sent with the cursor parked
    /// outside that window or the press is cleared before the guest sees it.
    /// </summary>
    public static bool ShouldPark(int previousButtons, int nextButtons) =>
        previousButtons != nextButtons;

    /// <summary>
    /// A non-zero wheel is cleared the same way as a button press while the Windows
    /// cursor sits over Presence. Park before sending <c>dz</c>.
    /// </summary>
    public static bool ShouldParkForWheel(int wheelNotches) => wheelNotches != 0;

    /// <summary>
    /// Windows delivers <c>WM_MOUSEWHEEL</c> to the focused HWND. A move (or a
    /// press) over the embed focuses the input window so chat does not take the wheel.
    /// </summary>
    public static bool ShouldFocusEmbedForWheel(bool pointerMovedOverEmbed, bool buttonPressed) =>
        pointerMovedOverEmbed || buttonPressed;

    /// <summary>
    /// One Windows detent (<c>HIWORD(wParam) / 120</c>) is one notch. Partial
    /// deltas below a detent are zero. Sign is preserved: positive is a wheel
    /// rotated away from the operator.
    /// </summary>
    public static int NotchesFromWheelDelta(int wheelDelta) => wheelDelta / WindowsWheelDetent;

    /// <summary>
    /// Guest <c>dz</c> for already-counted notches. One detent stays ±1.
    /// Live check on victoria-sandbox: raw <c>dz=-1</c> moved guest content up
    /// by about one notch, and raw <c>dz=+1</c> moved it down. Windows away is
    /// a positive detent, so the sign is negated to scroll content up.
    /// Do not scale by <see cref="WindowsWheelDetent"/> — 120 jumps many pages.
    /// </summary>
    public static int GuestWheelDz(int notches) => -notches;

    /// <summary>
    /// Move to the point with no wheel and no buttons, then send <c>dz</c>.
    /// Never a click.
    /// </summary>
    public static IReadOnlyList<AbsolutePointerStep> WheelSteps(int notches)
    {
        if (notches == 0)
            return [];

        var dz = GuestWheelDz(notches);
        return
        [
            new AbsolutePointerStep(0, 0, 0),
            new AbsolutePointerStep(dz, 0, 0)
        ];
    }
}

/// <summary>One <c>putMouseEventAbsolute</c> wheel/button payload at an already-mapped point.</summary>
public readonly record struct AbsolutePointerStep(int Dz, int Dw, int ButtonState);
