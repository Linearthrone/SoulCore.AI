namespace House.ChatDesktop.Services;

/// <summary>
/// Maps a Her-screen pointer event into the VirtualBox absolute pointer.
/// <c>putMouseEventAbsolute</c> is pixel-based with the origin at (1,1).
/// A wheel is two absolute samples at the real point: a move with no button,
/// then the same point with <c>dz</c> set to the notch count. No Page key.
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
    /// One absolute pointer sample. A wheel uses two: a move, then <c>dz</c>.
    /// </summary>
    public readonly record struct WheelStep(int X, int Y, int Dz, int Dw, int Buttons);

    /// <summary>
    /// VirtualBox <c>dz</c> for a Windows wheel notch count. Windows away (+notches)
    /// must become negative <c>dz</c>: VBox positive <c>dz</c> is clockwise and moves
    /// guest content down. Live TASK-193: raw <c>dz=-1</c> scrolled the page up.
    /// </summary>
    public static int GuestWheelDz(int notches) => -notches;

    /// <summary>
    /// Move to the real guest point with no button, then one wheel event there.
    /// <paramref name="notches"/> keeps the Windows sign (positive = away from the
    /// operator); <see cref="GuestWheelDz"/> converts that to VBox <c>dz</c>.
    /// </summary>
    public static WheelStep[] WheelSteps(int guestX, int guestY, int notches)
    {
        if (notches == 0)
            return Array.Empty<WheelStep>();

        var (x, y) = ToAbsolute(guestX, guestY);
        var dz = GuestWheelDz(notches);
        return
        [
            new WheelStep(x, y, 0, 0, 0),
            new WheelStep(x, y, dz, 0, 0),
        ];
    }

    /// <summary>
    /// A wheel does not park the Windows cursor. Button presses still park outside.
    /// </summary>
    public static bool ShouldParkForWheel(int wheelNotches)
    {
        _ = wheelNotches;
        return false;
    }

    /// <summary>
    /// A wheel does not move the Windows cursor into the embed.
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
    /// One startup-log line for a wheel that never reached the guest.
    /// Unmapped wheels say <c>dropped-unmapped</c>.
    /// </summary>
    public static string FormatWheelStartupLine(int notches, (int X, int Y)? guestPoint, string? comError)
    {
        if (notches == 0 || guestPoint is null)
            return $"her-screen wheel notches={notches} dropped-unmapped";

        return FormatWheelDeliveryLine(
            notches, guestPoint.Value.X, guestPoint.Value.Y, sent: false, comError);
    }

    /// <summary>
    /// One startup-log line after a wheel inject. <c>sent</c> means the console
    /// accepted the wheel at <paramref name="x"/>,<paramref name="y"/>.
    /// </summary>
    public static string FormatWheelDeliveryLine(
        int notches, int x, int y, bool sent, string? comError)
    {
        if (notches == 0)
            return $"her-screen wheel notches={notches} dropped-unmapped";

        var head = $"her-screen wheel notches={notches} point={x},{y} dz={GuestWheelDz(notches)}";
        if (sent && string.IsNullOrWhiteSpace(comError))
            return head + " sent";

        var reason = string.IsNullOrWhiteSpace(comError) ? "console wheel failed" : comError;
        var flat = reason.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        if (flat.Length == 0)
            flat = "failed";
        return head + " COM error: " + flat;
    }
}
