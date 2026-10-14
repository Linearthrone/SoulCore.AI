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
    public void Wheel_SendsOneKeyTapAndDoesNotSeatOrPark(int notches)
    {
        Assert.False(VictoriaVmPointerPlan.ShouldSeatCursorInsideEmbed(notches));
        Assert.False(VictoriaVmPointerPlan.ShouldParkForWheel(notches));
        var tap = VictoriaVmKeyPlan.WheelTap(notches);
        var one = notches > 0
            ? new byte[] { 0xE0, 0x49, 0xE0, 0xC9 }
            : new byte[] { 0xE0, 0x51, 0xE0, 0xD1 };
        Assert.Equal(one.Length * Math.Abs(notches), tap.Length);
        for (var i = 0; i < Math.Abs(notches); i++)
        {
            var at = i * one.Length;
            Assert.Equal(one[0], tap[at]);
            Assert.Equal(one[1], tap[at + 1]);
            Assert.Equal(one[2], tap[at + 2]);
            Assert.Equal(one[3], tap[at + 3]);
        }
    }

    [Fact]
    public void Wheel_ZeroNotchesDoNotSeatOrSend()
    {
        Assert.False(VictoriaVmPointerPlan.ShouldSeatCursorInsideEmbed(0));
        Assert.False(VictoriaVmPointerPlan.ShouldParkForWheel(0));
        Assert.Empty(VictoriaVmKeyPlan.WheelTap(0));
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
    public void Wheel_AwayFromOperatorIsPageUpMakeThenBreak()
    {
        const int away = 120;
        var notches = VictoriaVmPointerPlan.NotchesFromWheelDelta(away);
        Assert.Equal(1, notches);
        Assert.Equal(new byte[] { 0xE0, 0x49, 0xE0, 0xC9 }, VictoriaVmKeyPlan.WheelTap(notches));
        Assert.Equal("pageup", VictoriaVmKeyPlan.WheelKeyName(notches));

        var toward = VictoriaVmPointerPlan.NotchesFromWheelDelta(-120);
        Assert.Equal(-1, toward);
        Assert.Equal(new byte[] { 0xE0, 0x51, 0xE0, 0xD1 }, VictoriaVmKeyPlan.WheelTap(toward));
        Assert.Equal("pagedown", VictoriaVmKeyPlan.WheelKeyName(toward));
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
        var sent = VictoriaVmPointerPlan.FormatWheelStartupLine(1, (12, 34), null);
        var failed = VictoriaVmPointerPlan.FormatWheelStartupLine(-1, (8, 9), "RPC_E_DISCONNECTED\r\nsecond");

        Assert.Equal("her-screen wheel notches=1 dropped-unmapped", dropped);
        Assert.Equal("her-screen wheel notches=1 key=pageup sent", sent);
        Assert.Equal("her-screen wheel notches=-1 key=pagedown COM error: RPC_E_DISCONNECTED second", failed);
        Assert.Contains("key=pageup", sent, StringComparison.Ordinal);
        Assert.Contains("key=pagedown", failed, StringComparison.Ordinal);
        Assert.DoesNotContain("dz=", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", dropped, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", failed, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped-unmapped", sent, StringComparison.Ordinal);
        Assert.Equal(new byte[] { 0xE0, 0x49, 0xE0, 0xC9 }, VictoriaVmKeyPlan.WheelTap(1));
        Assert.Equal(new byte[] { 0xE0, 0x51, 0xE0, 0xD1 }, VictoriaVmKeyPlan.WheelTap(-1));
    }
}
