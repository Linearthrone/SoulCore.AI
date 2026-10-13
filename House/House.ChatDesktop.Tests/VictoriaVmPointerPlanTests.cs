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
}
