using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public class VictoriaVmPointerPlanTests
{
    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(509, 399, 510, 400)]
    [InlineData(-4, 12, 1, 13)]
    public void ToAbsolute_UsesOneBasedPixels(int x, int y, int ax, int ay)
    {
        var abs = VictoriaVmPointerPlan.ToAbsolute(x, y);
        Assert.Equal(ax, abs.X);
        Assert.Equal(ay, abs.Y);
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
