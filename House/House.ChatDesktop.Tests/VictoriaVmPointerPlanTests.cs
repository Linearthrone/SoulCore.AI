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
        var sent = VictoriaVmPointerPlan.FormatWheelDeliveryLine(1, 640, 400, 10, null);
        var toward = VictoriaVmPointerPlan.FormatWheelDeliveryLine(-1, 640, 400, 10, null);
        var failed = VictoriaVmPointerPlan.FormatWheelDeliveryLine(-1, 640, 400, 0, "RPC_E_DISCONNECTED\r\nsecond");
        var shortCount = VictoriaVmPointerPlan.FormatWheelDeliveryLine(1, 640, 400, 4, null);

        Assert.Equal("her-screen wheel notches=1 dropped-unmapped", dropped);
        Assert.Equal("her-screen wheel notches=1 key=pageup seat=640,400 stored=10 sent", sent);
        Assert.Equal("her-screen wheel notches=-1 key=pagedown seat=640,400 stored=10 sent", toward);
        Assert.Equal(
            "her-screen wheel notches=-1 key=pagedown seat=640,400 stored=0 COM error: RPC_E_DISCONNECTED second",
            failed);
        Assert.Equal(
            "her-screen wheel notches=1 key=pageup seat=640,400 stored=4 COM error: keyboard queue stored 4 of 10",
            shortCount);
        Assert.Contains("key=pageup", sent, StringComparison.Ordinal);
        Assert.Contains("key=pagedown", failed, StringComparison.Ordinal);
        Assert.Contains("stored=", sent, StringComparison.Ordinal);
        Assert.Contains("seat=640,400", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("sent", shortCount, StringComparison.Ordinal);
        Assert.Contains("COM error:", shortCount, StringComparison.Ordinal);
        Assert.Contains("stored=4", shortCount, StringComparison.Ordinal);
        Assert.DoesNotContain("dz=", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", dropped, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", failed, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", shortCount, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped-unmapped", sent, StringComparison.Ordinal);
        Assert.Equal(new byte[] { 0xE0, 0x49, 0xE0, 0xC9 }, VictoriaVmKeyPlan.WheelTap(1));
        Assert.Equal(new byte[] { 0xE0, 0x51, 0xE0, 0xD1 }, VictoriaVmKeyPlan.WheelTap(-1));
    }

    [Fact]
    public void Wheel_SeatsFramebufferCenterThenLeftClicksWithoutDz()
    {
        Assert.Equal((640, 400), VictoriaVmPointerPlan.WheelSeat(0, 0));
        Assert.Equal((640, 400), VictoriaVmPointerPlan.WheelSeat(1280, 800));
        Assert.Equal((960, 540), VictoriaVmPointerPlan.WheelSeat(1920, 1080));
        Assert.False(VictoriaVmPointerPlan.ShouldParkForWheel(1));
        Assert.False(VictoriaVmPointerPlan.ShouldSeatCursorInsideEmbed(1));

        var steps = VictoriaVmPointerPlan.WheelFocusSteps(1280, 800);
        Assert.Equal(3, steps.Length);
        Assert.All(steps, step =>
        {
            Assert.Equal(0, step.Dz);
            Assert.Equal(640, step.X);
            Assert.Equal(400, step.Y);
        });
        Assert.Equal(0, steps[0].Buttons);
        Assert.Equal(VictoriaVmPointerPlan.Left, steps[1].Buttons);
        Assert.Equal(0, steps[2].Buttons);
    }

    [Fact]
    public void Wheel_PrefixOnceThenPageUpMakeGapBreak()
    {
        Assert.Equal(40, VictoriaVmKeyPlan.WheelMakeBreakGapMs);
        Assert.Equal(new byte[] { 0xAA, 0xB6, 0x9D, 0xB8, 0xE0, 0xDB }, VictoriaVmKeyPlan.ModifierReleasePrefix);

        var up = VictoriaVmKeyPlan.WheelKeyboardChunks(1);
        Assert.Equal(3, up.Length);
        Assert.Equal(VictoriaVmKeyPlan.ModifierReleasePrefix, up[0]);
        Assert.Equal(new byte[] { 0xE0, 0x49 }, up[1]);
        Assert.Equal(new byte[] { 0xE0, 0xC9 }, up[2]);
        Assert.Equal(10, VictoriaVmKeyPlan.WheelStoredByteCount(1));

        var down = VictoriaVmKeyPlan.WheelKeyboardChunks(-1);
        Assert.Equal(new byte[] { 0xAA, 0xB6, 0x9D, 0xB8, 0xE0, 0xDB }, down[0]);
        Assert.Equal(new byte[] { 0xE0, 0x51 }, down[1]);
        Assert.Equal(new byte[] { 0xE0, 0xD1 }, down[2]);

        var three = VictoriaVmKeyPlan.WheelKeyboardChunks(3);
        Assert.Equal(7, three.Length);
        Assert.Equal(VictoriaVmKeyPlan.ModifierReleasePrefix, three[0]);
        Assert.Equal(new byte[] { 0xE0, 0x49 }, three[1]);
        Assert.Equal(new byte[] { 0xE0, 0xC9 }, three[2]);
        Assert.Equal(new byte[] { 0xE0, 0x49 }, three[5]);
        Assert.Equal(new byte[] { 0xE0, 0xC9 }, three[6]);
        Assert.Equal(18, VictoriaVmKeyPlan.WheelStoredByteCount(3));
        Assert.Empty(VictoriaVmKeyPlan.WheelKeyboardChunks(0));
    }
}
