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
    public void Wheel_ParksThenMovesBeforeDzAndDoesNotClick(int notches)
    {
        Assert.True(VictoriaVmPointerPlan.ShouldParkForWheel(notches));
        var steps = VictoriaVmPointerPlan.WheelSteps(notches);
        Assert.Equal(2, steps.Count);
        Assert.Equal(new AbsolutePointerStep(0, 0, 0), steps[0]);
        Assert.Equal(new AbsolutePointerStep(VictoriaVmPointerPlan.GuestWheelDz(notches), 0, 0), steps[1]);
        Assert.All(steps, step => Assert.Equal(0, step.ButtonState));
        Assert.Equal(-notches, steps[1].Dz);
        Assert.NotEqual(notches * VictoriaVmPointerPlan.WindowsWheelDetent, steps[1].Dz);
    }

    [Fact]
    public void Wheel_ZeroNotchesDoNotPark()
    {
        Assert.False(VictoriaVmPointerPlan.ShouldParkForWheel(0));
        Assert.Empty(VictoriaVmPointerPlan.WheelSteps(0));
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
        Assert.Equal(-notches, VictoriaVmPointerPlan.GuestWheelDz(notches));
    }

    [Fact]
    public void Wheel_AwayFromOperatorIsNegativeDzSoContentScrollsUp()
    {
        const int away = 120;
        var notches = VictoriaVmPointerPlan.NotchesFromWheelDelta(away);
        Assert.Equal(1, notches);
        Assert.Equal(-1, VictoriaVmPointerPlan.GuestWheelDz(notches));
        Assert.Equal(-1, VictoriaVmPointerPlan.WheelSteps(notches)[1].Dz);

        var toward = VictoriaVmPointerPlan.NotchesFromWheelDelta(-120);
        Assert.Equal(-1, toward);
        Assert.Equal(1, VictoriaVmPointerPlan.GuestWheelDz(toward));
    }

    [Fact]
    public void Wheel_FocusesEmbedOnPointerMoveWithoutAClick()
    {
        Assert.True(VictoriaVmPointerPlan.ShouldFocusEmbedForWheel(pointerMovedOverEmbed: true, buttonPressed: false));
        Assert.True(VictoriaVmPointerPlan.ShouldFocusEmbedForWheel(pointerMovedOverEmbed: false, buttonPressed: true));
        Assert.False(VictoriaVmPointerPlan.ShouldFocusEmbedForWheel(pointerMovedOverEmbed: false, buttonPressed: false));
    }
}
