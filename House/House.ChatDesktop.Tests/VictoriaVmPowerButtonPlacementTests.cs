using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public class VictoriaVmPowerButtonPlacementTests
{
    [Fact]
    public void TopRight_sits_inset_from_the_surface_corner()
    {
        var offset = VictoriaVmPowerButtonPlacement.TopRightOffset(640);

        Assert.Equal(640 - 76 - 8, offset.X);
        Assert.Equal(8, offset.Y);
    }

    [Fact]
    public void TopRight_stays_inside_a_narrow_surface()
    {
        var offset = VictoriaVmPowerButtonPlacement.TopRightOffset(40);

        Assert.Equal(8, offset.X);
        Assert.Equal(8, offset.Y);
    }
}
