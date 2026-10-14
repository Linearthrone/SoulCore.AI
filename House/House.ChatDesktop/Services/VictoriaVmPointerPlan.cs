namespace House.ChatDesktop.Services;

/// <summary>
/// Maps a Her-screen pointer event into the VirtualBox absolute pointer.
/// <c>putMouseEventAbsolute</c> is pixel-based with the origin at (1,1).
/// A wheel is not a mouse event: one detent is a Page Up or Page Down tap.
/// </summary>
public static class VictoriaVmPointerPlan
{
    public const int Left = 0x01;
    public const int Right = 0x02;
    public const int Middle = 0x04;
    public const int LockTypeShared = 1;

    /// <summary>VirtualBox <c>MachineState_Running</c>.</summary>
    public const int MachineStateRunning = 5;

    /// <summary>VirtualBox <c>SessionState_Locked</c>.</summary>
    public const int SessionStateLocked = 2;

    /// <summary>One Windows <c>WM_MOUSEWHEEL</c> detent in the high word of <c>wParam</c>.</summary>
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
    /// A wheel does not park the cursor. Button presses still park outside.
    /// </summary>
    public static bool ShouldParkForWheel(int wheelNotches)
    {
        _ = wheelNotches;
        return false;
    }

    /// <summary>
    /// A wheel is a key tap. It does not move the Windows cursor into the embed.
    /// </summary>
    public static bool ShouldSeatCursorInsideEmbed(int wheelNotches)
    {
        _ = wheelNotches;
        return false;
    }

    /// <summary>
    /// Center of the embed window rectangle. The point stays inside a non-empty
    /// rect, including a one-pixel window on a monitor with negative coordinates.
    /// </summary>
    public static (int X, int Y)? EmbedCursorSeat(int left, int top, int right, int bottom)
    {
        var width = right - left;
        var height = bottom - top;
        if (width <= 0 || height <= 0)
            return null;

        return (left + (width / 2), top + (height / 2));
    }

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
    /// Re-open the shared console lock before injecting. A lock held across
    /// save / abort / start is not <see cref="MachineStateRunning"/>, is no
    /// longer <see cref="SessionStateLocked"/>, has no console mouse, or points
    /// at a different VM process. A missing process id does not by itself drop
    /// a lock that otherwise looks live.
    /// </summary>
    public static bool NeedsConsoleReconnect(
        int? machineState,
        bool sessionLocked,
        bool consolePresent,
        bool mousePresent,
        int? lockedSessionPid,
        int? currentSessionPid)
    {
        if (machineState != MachineStateRunning
            || !sessionLocked
            || !consolePresent
            || !mousePresent)
            return true;

        return lockedSessionPid is > 0
            && currentSessionPid is > 0
            && lockedSessionPid != currentSessionPid;
    }

    /// <summary>
    /// One startup-log line for a wheel. Unmapped wheels say <c>dropped-unmapped</c>.
    /// A delivered wheel names <c>pageup</c> or <c>pagedown</c> and says <c>sent</c>.
    /// A failed inject puts <c>COM error:</c> on that same line.
    /// </summary>
    public static string FormatWheelStartupLine(int notches, (int X, int Y)? guestPoint, string? comError)
    {
        var key = VictoriaVmKeyPlan.WheelKeyName(notches);
        if (guestPoint is null || key.Length == 0)
            return $"her-screen wheel notches={notches} dropped-unmapped";

        if (comError is null)
            return $"her-screen wheel notches={notches} key={key} sent";

        var flat = comError.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        if (flat.Length == 0)
            flat = "failed";
        return $"her-screen wheel notches={notches} key={key} COM error: {flat}";
    }
}
