namespace House.ChatDesktop.Services;

/// <summary>
/// Maps a Her-screen pointer event into the VirtualBox absolute pointer.
/// Slot→Guest0→Absolute stretch-fill lives on <see cref="VictoriaBrowserCoordMap"/>
/// (PROP-19.3). <c>putMouseEventAbsolute</c> is guest-display pixels starting at
/// <c>[1,1]</c> top-left (VirtualBox SDK). Guest tools / stretch-fill stay 0-based;
/// Absolute adds +1 at the last step (<see cref="ToAbsolute"/>).
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

    /// <summary>
    /// Guest 0-based framebuffer pixel → Absolute [1,1] origin (SDK).
    /// Must stay identical to Host <c>VboxConsoleMouse.PlanAbsolutePress</c> (+1,+1).
    /// Soft-cursor tip uses Guest0 via <see cref="VictoriaBrowserCoordMap.TryMapGuestTipToSurfaceStretchFill"/>;
    /// do not paint Absolute coords.
    /// </summary>
    public static (int X, int Y) ToAbsolute(int guestX, int guestY) =>
        (Math.Max(0, guestX) + 1, Math.Max(0, guestY) + 1);

    /// <summary>
    /// Guest framebuffer size for stretch-fill. Prefer live <c>IDisplay</c> size,
    /// then hub/screenshot (last known real FB), then the HWND slot, then the JPEG
    /// default. Preferring the live slot over hub when display COM fails warps
    /// Absolute coords whenever Auto-resize and the hub disagree.
    /// Also exposed as <see cref="VictoriaBrowserCoordMap.ResolveGuestFramebuffer"/>.
    /// </summary>
    public static (int Width, int Height) ResolveGuestFramebuffer(
        int hubWidth,
        int hubHeight,
        int? displayWidth,
        int? displayHeight,
        int surfaceWidth = 0,
        int surfaceHeight = 0)
    {
        if (displayWidth is > 0 && displayHeight is > 0)
            return (displayWidth.Value, displayHeight.Value);

        if (hubWidth > 0 && hubHeight > 0)
            return (hubWidth, hubHeight);

        if (surfaceWidth > 1 && surfaceHeight > 1)
            return (surfaceWidth, surfaceHeight);

        return (
            VictoriaBrowserCoordMap.DefaultGuestWidth,
            VictoriaBrowserCoordMap.DefaultGuestHeight);
    }

    /// <summary>
    /// Exclude rect for host-cursor park during Absolute button inject.
    /// </summary>
    public readonly record struct ParkRect(int Left, int Top, int Right, int Bottom)
    {
        public bool Contains(int x, int y) =>
            x >= Left && x < Right && y >= Top && y < Bottom;
    }

    /// <summary>
    /// True when Presence GA_ROOT can be excluded without trapping park (another
    /// virtual-screen region exists outside the root). Mouse Integration tracks the
    /// whole Presence client — chrome park is a false win when root is excluded away.
    /// Single-monitor maximized root → false (keep embed-only / #171).
    /// </summary>
    public static bool CanExcludePresenceRoot(
        ParkRect root,
        int virtualX,
        int virtualY,
        int virtualW,
        int virtualH)
    {
        var vw = Math.Max(1, virtualW);
        var vh = Math.Max(1, virtualH);
        var rootOnly = new[] { root };
        foreach (var c in EnumerateParkCandidates(rootOnly, virtualX, virtualY, vw, vh))
        {
            // SetCursorPos clamps onto the virtual screen — ignore off-screen
            // "just outside exclude" candidates (they caused the #171 trap).
            if (c.X < virtualX || c.Y < virtualY
                || c.X >= virtualX + vw || c.Y >= virtualY + vh)
                continue;
            if (!root.Contains(c.X, c.Y))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Park exclude policy: always the embed HWND; add GA_ROOT when a free region
    /// exists outside Presence (multi-monitor). Never empty when embed is known.
    /// </summary>
    public static List<ParkRect> BuildParkExcludePolicy(
        ParkRect? embed,
        ParkRect? presenceRoot,
        int virtualX,
        int virtualY,
        int virtualW,
        int virtualH)
    {
        var excludes = new List<ParkRect>(2);
        if (embed is { } e)
            excludes.Add(e);

        if (presenceRoot is { } root
            && CanExcludePresenceRoot(root, virtualX, virtualY, virtualW, virtualH))
        {
            var sameAsEmbed = embed is { } emb
                && emb.Left == root.Left
                && emb.Top == root.Top
                && emb.Right == root.Right
                && emb.Bottom == root.Bottom;
            if (!sameAsEmbed)
                excludes.Add(root);
        }

        return excludes;
    }

    /// <summary>
    /// Ordered park candidates (virtual-screen corners, then just outside excludes).
    /// When GA_ROOT is in excludes (multi-monitor), park must leave Presence entirely.
    /// </summary>
    public static IEnumerable<(int X, int Y)> EnumerateParkCandidates(
        IReadOnlyList<ParkRect> excludes,
        int virtualX,
        int virtualY,
        int virtualW,
        int virtualH)
    {
        var vw = Math.Max(1, virtualW);
        var vh = Math.Max(1, virtualH);

        yield return (virtualX + vw - 4, virtualY + vh - 4);
        yield return (virtualX + 4, virtualY + vh - 4);
        yield return (virtualX + vw - 4, virtualY + 4);
        yield return (virtualX + vw / 2, virtualY + vh - 4);
        yield return (virtualX + 4, virtualY + 4);

        if (excludes.Count == 0)
            yield break;

        var widest = excludes[0];
        foreach (var r in excludes)
        {
            if ((r.Right - r.Left) > (widest.Right - widest.Left))
                widest = r;
        }

        var midY = (widest.Top + widest.Bottom) / 2;
        var midX = (widest.Left + widest.Right) / 2;
        yield return (widest.Right + 8, midY);
        yield return (widest.Left - 8, midY);
        yield return (midX, widest.Bottom + 8);
        yield return (midX, widest.Top - 8);
        yield return (widest.Right + 1, midY);
        yield return (virtualX + vw - 2, virtualY + vh - 2);
    }

    public static (int X, int Y) PickParkPoint(
        IReadOnlyList<ParkRect> excludes,
        int virtualX,
        int virtualY,
        int virtualW,
        int virtualH)
    {
        foreach (var c in EnumerateParkCandidates(excludes, virtualX, virtualY, virtualW, virtualH))
        {
            if (!IsInsideAnyPark(c.X, c.Y, excludes))
                return c;
        }

        return (virtualX + Math.Max(1, virtualW) - 4, virtualY + Math.Max(1, virtualH) - 4);
    }

    public static bool IsInsideAnyPark(int x, int y, IReadOnlyList<ParkRect> excludes)
    {
        foreach (var r in excludes)
        {
            if (r.Contains(x, y))
                return true;
        }

        return false;
    }

    /// <summary>One startup-log line for a click that never reached the guest.</summary>
    public static string FormatClickDroppedLine(int surfaceX, int surfaceY, int surfaceW, int surfaceH) =>
        $"her-screen click surface={surfaceW}x{surfaceH} pt={surfaceX},{surfaceY} dropped-unmapped";

    /// <summary>
    /// Operator samples are mirrored live. Stashing until release, then parking the
    /// Windows cursor, took the mouse off the pane and dropped the press when park failed.
    /// </summary>
    public static bool ShouldStashPress(bool buttonChanged, int buttons)
    {
        _ = buttonChanged;
        _ = buttons;
        return false;
    }

    /// <summary>Release-only inject is off. Button-down and button-up are each one Absolute sample.</summary>
    public static bool ShouldInjectOnRelease(bool buttonChanged, int buttons, bool hasPending)
    {
        _ = buttonChanged;
        _ = buttons;
        _ = hasPending;
        return false;
    }

    /// <summary>Honest Absolute success: COM accepted the sample. The host cursor is not moved.</summary>
    public static bool IsHonestClickSuccess(bool sent, string? comError) =>
        sent && string.IsNullOrWhiteSpace(comError);

    /// <summary>One startup-log line after an Absolute sample. The Windows cursor stays put.</summary>
    public static string FormatClickDeliveryLine(
        int guestX, int guestY, int buttons, bool sent, string? comError)
    {
        var (ax, ay) = ToAbsolute(guestX, guestY);
        var head = $"her-screen click abs={ax},{ay} buttons={buttons} cursor=held";
        if (IsHonestClickSuccess(sent, comError))
            return head + " sent";

        var reason = string.IsNullOrWhiteSpace(comError) ? "console click failed" : comError;
        var flat = Flatten(reason);
        if (flat.Length == 0)
            flat = "failed";
        return head + " COM error: " + flat;
    }

    /// <summary>VirtualBox <c>E_FAIL</c> / ConsoleWrap unresolved host platform error.</summary>
    public const int ConsoleWrapEFail = unchecked((int)0x80004005);

    public static string FormatConsoleWrapFault() =>
        "Her screen: VirtualBox ConsoleWrap 0x80004005 (unresolved host platform error). " +
        "The guest has no pointer until that console is running. " +
        "Free host memory, and confirm Hyper-V is not blocking VirtualBox.";

    /// <summary>
    /// Map a COM failure onto the ConsoleWrap sentence when VirtualBox reports
    /// <c>0x80004005</c> or <c>VERR_UNRESOLVED_ERROR</c>.
    /// </summary>
    public static string DescribeConsoleFailure(string? message, int? hresult)
    {
        var flat = Flatten(message);
        if (flat.StartsWith("Her screen: VirtualBox ConsoleWrap", StringComparison.Ordinal))
            return flat;

        var named = flat.Contains("ConsoleWrap", StringComparison.OrdinalIgnoreCase)
            || flat.Contains("VERR_UNRESOLVED", StringComparison.OrdinalIgnoreCase)
            || flat.Contains("unresolved host platform", StringComparison.OrdinalIgnoreCase);
        if (hresult == ConsoleWrapEFail || named)
            return flat.Length == 0 ? FormatConsoleWrapFault() : FormatConsoleWrapFault() + " (" + flat + ")";

        return flat.Length == 0 ? "console click failed" : flat;
    }

    private static string Flatten(string? reason)
    {
        var flat = (reason ?? string.Empty).Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return flat;
    }

    /// <summary>Operator-visible Her screen status when Absolute delivery fails.</summary>
    public static string FormatOperatorClickFault(string? detail)
    {
        var flat = string.IsNullOrWhiteSpace(detail) ? "click did not reach the guest" : detail.Trim();
        if (flat.StartsWith("Her screen:", StringComparison.Ordinal))
            return flat;
        if (flat.Contains("0x80004005", StringComparison.OrdinalIgnoreCase)
            || flat.Contains("ConsoleWrap", StringComparison.OrdinalIgnoreCase)
            || flat.Contains("VERR_UNRESOLVED", StringComparison.OrdinalIgnoreCase)
            || flat.Contains("unresolved host platform", StringComparison.OrdinalIgnoreCase))
            return FormatConsoleWrapFault();
        if (flat.Contains("unmapped", StringComparison.OrdinalIgnoreCase))
            return "Her screen: click blocked — surface point unmapped to guest framebuffer.";
        if (flat.Contains("no mouse", StringComparison.OrdinalIgnoreCase))
            return "Her screen: click blocked — VirtualBox console has no mouse.";
        return "Her screen: click failed — " + flat;
    }

    /// <summary>Operator-visible status when the input overlay is missing over the VM HWND.</summary>
    public static string FormatOperatorOverlayMissingFault() =>
        "Her screen: input overlay missing — clicks hit VirtualBox paint only (guest deaf).";

    /// <summary>
    /// Click never reached Absolute (console missing before guest map / inject).
    /// The host cursor was not moved.
    /// </summary>
    public static string FormatClickFailedBeforeInjectLine(int buttons, string? comError)
    {
        var flat = DescribeConsoleFailure(comError, hresult: null);
        if (flat.StartsWith("Her screen:", StringComparison.Ordinal))
            flat = Flatten(comError);
        if (flat.Length == 0)
            flat = "failed";
        return $"her-screen click buttons={buttons} cursor=held COM error: {flat}";
    }

    /// <summary>
    /// Operator input never calls <c>SetCursorPos</c>. Parking the cursor off Her screen
    /// is what made the mouse leave the pane.
    /// </summary>
    public static bool ShouldPark(int previousButtons, int nextButtons)
    {
        _ = previousButtons;
        _ = nextButtons;
        return false;
    }

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
    /// A wheel does not move the Windows cursor. Button samples do not either.
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
