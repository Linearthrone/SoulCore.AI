namespace House.ChatDesktop.Services;

/// <summary>
/// Maps a Her-screen pointer event into the VirtualBox absolute pointer.
/// <c>putMouseEventAbsolute</c> is pixel-based with the origin at (1,1).
/// A wheel seats the guest pointer in the center column, left-clicks there,
/// then sends a Page Up or Page Down tap. It does not send a mouse-wheel
/// <c>dz</c>.
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
    /// One absolute pointer sample for the wheel focus sequence. <c>Dz</c> stays 0.
    /// </summary>
    public readonly record struct WheelFocusStep(int X, int Y, int Dz, int Dw, int Buttons);

    /// <summary>
    /// Center of the guest framebuffer. A non-positive size falls back to
    /// <see cref="VictoriaBrowserCoordMap.DefaultGuestWidth"/> and
    /// <see cref="VictoriaBrowserCoordMap.DefaultGuestHeight"/> (1280×800),
    /// whose center is (640, 400).
    /// </summary>
    public static (int X, int Y) WheelSeat(int guestWidth, int guestHeight)
    {
        var width = guestWidth > 0 ? guestWidth : VictoriaBrowserCoordMap.DefaultGuestWidth;
        var height = guestHeight > 0 ? guestHeight : VictoriaBrowserCoordMap.DefaultGuestHeight;
        return (width / 2, height / 2);
    }

    /// <summary>
    /// Seat the guest pointer, then a left click (down, then up), all with
    /// <c>dz=0</c>. The Windows cursor is not parked for this sequence.
    /// </summary>
    public static WheelFocusStep[] WheelFocusSteps(int guestWidth, int guestHeight)
    {
        var (x, y) = WheelSeat(guestWidth, guestHeight);
        return
        [
            new WheelFocusStep(x, y, 0, 0, 0),
            new WheelFocusStep(x, y, 0, 0, Left),
            new WheelFocusStep(x, y, 0, 0, 0),
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
    /// One startup-log line for a wheel that never reached the guest.
    /// Unmapped wheels say <c>dropped-unmapped</c>. A mapped point without a
    /// stored count is not <c>sent</c>.
    /// </summary>
    public static string FormatWheelStartupLine(int notches, (int X, int Y)? guestPoint, string? comError)
    {
        var key = VictoriaVmKeyPlan.WheelKeyName(notches);
        if (guestPoint is null || key.Length == 0)
            return $"her-screen wheel notches={notches} dropped-unmapped";

        return FormatWheelDeliveryLine(notches, guestPoint.Value.X, guestPoint.Value.Y, 0, comError);
    }

    /// <summary>
    /// One startup-log line after a wheel inject. <c>sent</c> is written only
    /// when <paramref name="stored"/> equals the prefix, make, and break bytes
    /// handed to the keyboard. A short count is a <c>COM error:</c> line that
    /// still includes <c>seat=</c> and <c>stored=</c>.
    /// </summary>
    public static string FormatWheelDeliveryLine(
        int notches, int seatX, int seatY, int stored, string? comError)
    {
        var key = VictoriaVmKeyPlan.WheelKeyName(notches);
        if (key.Length == 0)
            return $"her-screen wheel notches={notches} dropped-unmapped";

        var expected = VictoriaVmKeyPlan.WheelStoredByteCount(notches);
        var head = $"her-screen wheel notches={notches} key={key} seat={seatX},{seatY} stored={stored}";
        if (comError is null && stored == expected && expected > 0)
            return head + " sent";

        var reason = string.IsNullOrWhiteSpace(comError)
            ? $"keyboard queue stored {stored} of {expected}"
            : comError;
        var flat = reason.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        if (flat.Length == 0)
            flat = "failed";
        return head + " COM error: " + flat;
    }
}
