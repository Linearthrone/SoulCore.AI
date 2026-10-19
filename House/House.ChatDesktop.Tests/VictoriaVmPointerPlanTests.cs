using System.Linq;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public class VictoriaVmPointerPlanTests
{
    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(509, 399, 510, 400)]
    [InlineData(-4, 12, 1, 13)]
    public void ToAbsolute_UsesOneBasedVirtualDisplayOrigin(int x, int y, int ax, int ay)
    {
        var abs = VictoriaVmPointerPlan.ToAbsolute(x, y);
        Assert.Equal(ax, abs.X);
        Assert.Equal(ay, abs.Y);
    }

    [Fact]
    public void ResolveGuestFramebuffer_PrefersDisplayThenHubOverSurface()
    {
        var resolved = VictoriaVmPointerPlan.ResolveGuestFramebuffer(
            hubWidth: 1280,
            hubHeight: 800,
            displayWidth: 1600,
            displayHeight: 900);
        Assert.Equal((1600, 900), resolved);

        // When display COM fails, hub/screenshot FB beats the HWND slot so
        // Absolute does not warp when Auto-resize and the slot disagree.
        var hubOverSurface = VictoriaVmPointerPlan.ResolveGuestFramebuffer(
            hubWidth: 1280,
            hubHeight: 800,
            displayWidth: null,
            displayHeight: null,
            surfaceWidth: 900,
            surfaceHeight: 600);
        Assert.Equal((1280, 800), hubOverSurface);

        var surfaceOnly = VictoriaVmPointerPlan.ResolveGuestFramebuffer(
            hubWidth: 0,
            hubHeight: 0,
            displayWidth: null,
            displayHeight: null,
            surfaceWidth: 900,
            surfaceHeight: 600);
        Assert.Equal((900, 600), surfaceOnly);

        var hubOnly = VictoriaVmPointerPlan.ResolveGuestFramebuffer(1280, 800, null, null);
        Assert.Equal((1280, 800), hubOnly);

        var defaults = VictoriaVmPointerPlan.ResolveGuestFramebuffer(0, 0, null, null);
        Assert.Equal(
            (VictoriaBrowserCoordMap.DefaultGuestWidth, VictoriaBrowserCoordMap.DefaultGuestHeight),
            defaults);
    }

    [Fact]
    public void EnumerateParkCandidates_PreferVirtualCornersOutsideExclude()
    {
        var excludes = new[] { new VictoriaVmPointerPlan.ParkRect(0, 0, 800, 600) };
        var list = VictoriaVmPointerPlan.EnumerateParkCandidates(
            excludes, virtualX: 0, virtualY: 0, virtualW: 1920, virtualH: 1080).ToList();
        Assert.Contains((1916, 1076), list);
        Assert.False(VictoriaVmPointerPlan.IsInsideAnyPark(1916, 1076, excludes));

        var pick = VictoriaVmPointerPlan.PickParkPoint(
            excludes, virtualX: 0, virtualY: 0, virtualW: 1920, virtualH: 1080);
        Assert.False(VictoriaVmPointerPlan.IsInsideAnyPark(pick.X, pick.Y, excludes));
    }

    /// <summary>
    /// Regression: excluding GA_ROOT (maximized Presence) left no park on a single
    /// monitor → park=failed → Mouse Integration cleared every Absolute press.
    /// Exclude only the embed/VM HWND so Presence chrome remains parkable.
    /// </summary>
    [Fact]
    public void Park_EmbedOnlyExcludeLeavesChromeCorners_FullscreenRootDoesNot()
    {
        const int vw = 1920;
        const int vh = 1080;

        var fullscreenRoot = new[] { new VictoriaVmPointerPlan.ParkRect(0, 0, vw, vh) };
        // Virtual-screen corners are inside a fullscreen exclude.
        foreach (var c in new[] { (1916, 1076), (4, 1076), (1916, 4), (960, 1076), (4, 4) })
        {
            Assert.True(
                VictoriaVmPointerPlan.IsInsideAnyPark(c.Item1, c.Item2, fullscreenRoot),
                $"corner {c} should be inside fullscreen exclude");
        }

        // EnumerateParkCandidates also yields just-outside-rect points, but SetCursorPos
        // clamps them back onto the virtual screen — which is still inside GA_ROOT.
        var outside = (vw + 8, vh / 2);
        Assert.False(VictoriaVmPointerPlan.IsInsideAnyPark(outside.Item1, outside.Item2, fullscreenRoot));
        var clampedX = Math.Clamp(outside.Item1, 0, vw - 1);
        Assert.True(
            VictoriaVmPointerPlan.IsInsideAnyPark(clampedX, outside.Item2, fullscreenRoot),
            "Win32 clamp of outside candidate lands back inside fullscreen exclude");

        // Her-screen embed pane (left); chat/chrome occupies the right strip.
        var embedOnly = new[] { new VictoriaVmPointerPlan.ParkRect(0, 0, 1280, vh) };
        var pick = VictoriaVmPointerPlan.PickParkPoint(embedOnly, 0, 0, vw, vh);
        Assert.False(
            VictoriaVmPointerPlan.IsInsideAnyPark(pick.X, pick.Y, embedOnly),
            $"embed-only park landed inside exclude: {pick}");
        Assert.Contains(
            (1916, 1076),
            VictoriaVmPointerPlan.EnumerateParkCandidates(embedOnly, 0, 0, vw, vh));
    }

    [Fact]
    public void FormatClickDroppedLine_MentionsUnmapped()
    {
        var line = VictoriaVmPointerPlan.FormatClickDroppedLine(10, 20, 900, 600);
        Assert.Contains("dropped-unmapped", line, StringComparison.Ordinal);
        Assert.Contains("900x600", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldStashPress_And_InjectOnRelease_HostButtonParity()
    {
        Assert.True(VictoriaVmPointerPlan.ShouldStashPress(buttonChanged: true, buttons: 1));
        Assert.False(VictoriaVmPointerPlan.ShouldStashPress(buttonChanged: true, buttons: 0));
        Assert.False(VictoriaVmPointerPlan.ShouldStashPress(buttonChanged: false, buttons: 1));

        Assert.True(VictoriaVmPointerPlan.ShouldInjectOnRelease(
            buttonChanged: true, buttons: 0, hasPending: true));
        Assert.False(VictoriaVmPointerPlan.ShouldInjectOnRelease(
            buttonChanged: true, buttons: 0, hasPending: false));
        Assert.False(VictoriaVmPointerPlan.ShouldInjectOnRelease(
            buttonChanged: true, buttons: 1, hasPending: true));
    }

    [Fact]
    public void FormatClickDeliveryLine_IncludesParkAndSent()
    {
        var ok = VictoriaVmPointerPlan.FormatClickDeliveryLine(10, 20, 1, parkOk: true, sent: true, null);
        Assert.Contains("park=ok", ok, StringComparison.Ordinal);
        Assert.Contains("sent", ok, StringComparison.Ordinal);
        Assert.Contains("abs=11,21", ok, StringComparison.Ordinal);

        var fail = VictoriaVmPointerPlan.FormatClickDeliveryLine(
            10, 20, 1, parkOk: false, sent: false, "boom");
        Assert.Contains("park=failed", fail, StringComparison.Ordinal);
        Assert.Contains("COM error: boom", fail, StringComparison.Ordinal);
    }

    [Fact]
    public void StretchFill_SlotVsGuestFramebuffer_MapsCenterCorrectly()
    {
        // Regression: treating slot 900×600 as the FB sent Absolute(450,300) for a
        // center click; real FB 1280×800 must map center → (640,400).
        var mapped = VictoriaBrowserCoordMap.TryMapPointerStretchFill(
            pointerX: 450,
            pointerY: 300,
            surfaceWidth: 900,
            surfaceHeight: 600,
            imagePixelWidth: 1280,
            imagePixelHeight: 800);
        Assert.NotNull(mapped);
        Assert.Equal(640, mapped!.Value.X);
        Assert.Equal(400, mapped.Value.Y);
    }

    [Fact]
    public void ShouldPark_WhenTheButtonMaskChanges()
    {
        Assert.True(VictoriaVmPointerPlan.ShouldPark(0, VictoriaVmPointerPlan.Left));
        Assert.True(VictoriaVmPointerPlan.ShouldPark(VictoriaVmPointerPlan.Left, 0));
        Assert.False(VictoriaVmPointerPlan.ShouldPark(VictoriaVmPointerPlan.Left, VictoriaVmPointerPlan.Left));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(3)]
    public void Wheel_UsesDzAtTheRealPointAndDoesNotSeatOrPark(int notches)
    {
        Assert.False(VictoriaVmPointerPlan.ShouldSeatCursorInsideEmbed(notches));
        Assert.False(VictoriaVmPointerPlan.ShouldParkForWheel(notches));
        var steps = VictoriaVmPointerPlan.WheelSteps(100, 200, notches);
        Assert.Equal(2, steps.Length);
        Assert.Equal((101, 201), (steps[0].X, steps[0].Y));
        Assert.Equal(0, steps[0].Dz);
        Assert.Equal(0, steps[0].Buttons);
        Assert.Equal((101, 201), (steps[1].X, steps[1].Y));
        Assert.Equal(VictoriaVmPointerPlan.GuestWheelDz(notches), steps[1].Dz);
        Assert.Equal(0, steps[1].Dw);
        Assert.Equal(0, steps[1].Buttons);
    }

    [Fact]
    public void Wheel_ZeroNotchesDoNotSeatOrSend()
    {
        Assert.False(VictoriaVmPointerPlan.ShouldSeatCursorInsideEmbed(0));
        Assert.False(VictoriaVmPointerPlan.ShouldParkForWheel(0));
        Assert.Empty(VictoriaVmPointerPlan.WheelSteps(10, 20, 0));
    }

    [Theory]
    [InlineData(5750, 144, 6971, 827, 6360, 485)]
    [InlineData(-1, -1, 0, 0, -1, -1)]
    [InlineData(0, 0, 1, 1, 0, 0)]
    public void Wheel_DoesNotSeatCursorEvenWhenEmbedCenterIsKnown(
        int left, int top, int right, int bottom, int x, int y)
    {
        Assert.False(VictoriaVmPointerPlan.ShouldSeatCursorInsideEmbed(1));
        Assert.False(VictoriaVmPointerPlan.ShouldSeatCursorInsideEmbed(-1));
        var seat = VictoriaVmPointerPlan.EmbedCursorSeat(left, top, right, bottom);
        Assert.Equal((x, y), seat);
        Assert.True(seat!.Value.X >= left && seat.Value.X < right);
        Assert.True(seat.Value.Y >= top && seat.Value.Y < bottom);
        Assert.Null(VictoriaVmPointerPlan.EmbedCursorSeat(left, top, left, bottom));
    }

    [Theory]
    [InlineData(120, 1)]
    [InlineData(-120, -1)]
    [InlineData(240, 2)]
    [InlineData(-240, -2)]
    [InlineData(0, 0)]
    [InlineData(119, 0)]
    [InlineData(-119, 0)]
    public void Wheel_OneDetentIsOneNotchAndKeepsSign(int delta, int notches)
    {
        Assert.Equal(notches, VictoriaVmPointerPlan.NotchesFromWheelDelta(delta));
    }

    [Fact]
    public void Wheel_AwayFromOperatorBecomesNegativeDz()
    {
        // Windows away (+120) must become VBox dz=-1 (content scrolls up).
        const int away = 120;
        var notches = VictoriaVmPointerPlan.NotchesFromWheelDelta(away);
        Assert.Equal(1, notches);
        Assert.Equal(-1, VictoriaVmPointerPlan.GuestWheelDz(notches));
        var steps = VictoriaVmPointerPlan.WheelSteps(10, 20, notches);
        Assert.Equal(-1, steps[1].Dz);
        Assert.Equal(0, steps[1].Buttons);

        var toward = VictoriaVmPointerPlan.NotchesFromWheelDelta(-120);
        Assert.Equal(-1, toward);
        Assert.Equal(1, VictoriaVmPointerPlan.WheelSteps(10, 20, toward)[1].Dz);
    }

    [Fact]
    public void Wheel_FocusesEmbedOnPointerMoveWithoutAClick()
    {
        Assert.True(VictoriaVmPointerPlan.ShouldFocusEmbedForWheel(pointerMovedOverEmbed: true, buttonPressed: false));
        Assert.True(VictoriaVmPointerPlan.ShouldFocusEmbedForWheel(pointerMovedOverEmbed: false, buttonPressed: true));
        Assert.False(VictoriaVmPointerPlan.ShouldFocusEmbedForWheel(pointerMovedOverEmbed: false, buttonPressed: false));
    }

    // MachineState: 5 Running, 2 Saved, 4 Aborted, 1 PoweredOff.
    [Theory]
    [InlineData(5, true, true, true, 100, 100, false)]
    [InlineData(2, true, true, true, 100, 100, true)]
    [InlineData(4, true, true, true, 100, 100, true)]
    [InlineData(1, true, true, true, 100, 100, true)]
    [InlineData(5, false, true, true, 100, 100, true)]
    [InlineData(5, true, false, false, 100, 100, true)]
    [InlineData(5, true, true, false, 100, 100, true)]
    [InlineData(5, true, true, true, 100, 240, true)]
    [InlineData(null, true, true, true, 100, 100, true)]
    [InlineData(5, true, true, true, null, 100, false)]
    [InlineData(5, true, true, true, 100, null, false)]
    public void Wheel_ReopensConsoleLockWhenNotRunningOrMouseGone(
        int? machineState,
        bool sessionLocked,
        bool consolePresent,
        bool mousePresent,
        int? lockedSessionPid,
        int? currentSessionPid,
        bool reconnect)
    {
        Assert.Equal(
            reconnect,
            VictoriaVmPointerPlan.NeedsConsoleReconnect(
                machineState,
                sessionLocked,
                consolePresent,
                mousePresent,
                lockedSessionPid,
                currentSessionPid));
    }

    [Fact]
    public void Wheel_StartupLineIsOneLineWithNotchesAndSentOrDropped()
    {
        var dropped = VictoriaVmPointerPlan.FormatWheelStartupLine(1, null, null);
        var sent = VictoriaVmPointerPlan.FormatWheelDeliveryLine(1, 100, 200, sent: true, comError: null);
        var toward = VictoriaVmPointerPlan.FormatWheelDeliveryLine(-1, 100, 200, sent: true, comError: null);
        var failed = VictoriaVmPointerPlan.FormatWheelDeliveryLine(-1, 100, 200, sent: false, "RPC_E_DISCONNECTED\r\nsecond");
        var unsent = VictoriaVmPointerPlan.FormatWheelDeliveryLine(1, 100, 200, sent: false, null);

        Assert.Equal("her-screen wheel notches=1 dropped-unmapped", dropped);
        Assert.Equal("her-screen wheel notches=1 point=100,200 dz=-1 sent", sent);
        Assert.Equal("her-screen wheel notches=-1 point=100,200 dz=1 sent", toward);
        Assert.Equal(
            "her-screen wheel notches=-1 point=100,200 dz=1 COM error: RPC_E_DISCONNECTED second",
            failed);
        Assert.Equal(
            "her-screen wheel notches=1 point=100,200 dz=-1 COM error: console wheel failed",
            unsent);
        Assert.Contains("dz=-1", sent, StringComparison.Ordinal);
        Assert.Contains("point=100,200", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("pageup", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("pagedown", failed, StringComparison.Ordinal);
        Assert.DoesNotContain("sent", unsent, StringComparison.Ordinal);
        Assert.Contains("COM error:", unsent, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", dropped, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", failed, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", unsent, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped-unmapped", sent, StringComparison.Ordinal);
    }

    [Fact]
    public void Wheel_StaysOnTheRealPointWithNoButton()
    {
        Assert.False(VictoriaVmPointerPlan.ShouldParkForWheel(1));
        Assert.False(VictoriaVmPointerPlan.ShouldSeatCursorInsideEmbed(1));

        var steps = VictoriaVmPointerPlan.WheelSteps(639, 399, 2);
        Assert.Equal(2, steps.Length);
        Assert.All(steps, step =>
        {
            Assert.Equal(640, step.X);
            Assert.Equal(400, step.Y);
            Assert.Equal(0, step.Buttons);
        });
        Assert.Equal(0, steps[0].Dz);
        Assert.Equal(-2, steps[1].Dz);
    }
}
