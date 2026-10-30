using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public class VictoriaVmKeyPlanTests
{
    [Fact]
    public void KeyDown_SendsMakeCode()
    {
        var ok = VictoriaVmKeyPlan.TryFromKeyMessage(
            VictoriaVmKeyPlan.WmKeyDown, Pack(0x1E, extended: false), out var codes, out var hold, out var up);

        Assert.True(ok);
        Assert.False(up);
        Assert.Equal(new byte[] { 0x1E }, codes);
        Assert.Equal(0x1E, hold);
    }

    [Fact]
    public void KeyUp_SendsBreakCode()
    {
        var ok = VictoriaVmKeyPlan.TryFromKeyMessage(
            VictoriaVmKeyPlan.WmKeyUp, Pack(0x1E, extended: false), out var codes, out _, out var up);

        Assert.True(ok);
        Assert.True(up);
        Assert.Equal(new byte[] { 0x9E }, codes);
    }

    [Fact]
    public void ExtendedArrow_PrefixesE0()
    {
        Assert.True(VictoriaVmKeyPlan.TryFromKeyMessage(
            VictoriaVmKeyPlan.WmKeyDown, Pack(0x48, extended: true), out var down, out var hold, out _));
        Assert.Equal(new byte[] { 0xE0, 0x48 }, down);

        Assert.True(VictoriaVmKeyPlan.TryFromKeyMessage(
            VictoriaVmKeyPlan.WmKeyUp, Pack(0x48, extended: true), out var up, out _, out var isUp));
        Assert.True(isUp);
        Assert.Equal(new byte[] { 0xE0, 0xC8 }, up);
        Assert.Equal(up, VictoriaVmKeyPlan.Break(hold));
    }

    [Fact]
    public void Alt_UsesSysKeyMessages()
    {
        Assert.True(VictoriaVmKeyPlan.TryFromKeyMessage(
            VictoriaVmKeyPlan.WmSysKeyDown, Pack(0x38, extended: false), out var codes, out _, out var up));
        Assert.False(up);
        Assert.Equal(new byte[] { 0x38 }, codes);
    }

    [Theory]
    [InlineData(0x0200u)]
    [InlineData(VictoriaVmKeyPlan.WmKeyDown)]
    public void MissingScanCode_IsNotAKey(uint msg)
    {
        var lParam = msg == VictoriaVmKeyPlan.WmKeyDown ? 0L : Pack(0x1E, extended: false);
        Assert.False(VictoriaVmKeyPlan.TryFromKeyMessage(msg, lParam, out _, out _, out _));
    }

    private static long Pack(byte scan, bool extended) =>
        ((long)scan << 16) | (extended ? 1L << 24 : 0);
}
