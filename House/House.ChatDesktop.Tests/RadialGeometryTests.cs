using House.ChatDesktop.Controls;

namespace House.ChatDesktop.Tests;

public class RadialGeometryTests
{
    [Fact]
    public void EightServices_AreFullyInsideThePopup_AndLeftOfTheSlab()
    {
        var layout = RadialGeometry.Compute(8, leftDip: 640, aboveDip: 160);

        Assert.Equal(8, layout.Slots.Count);
        Assert.True(layout.OuterRadius > layout.InnerRadius);
        var band = layout.OuterRadius - layout.InnerRadius;
        Assert.True(layout.Slots[0].Diameter > band * 0.55, "orbs should fill the wedge");

        foreach (var slot in layout.Slots)
        {
            var radius = slot.Diameter / 2;
            Assert.InRange(slot.OrbX - radius, 0, layout.Width);
            Assert.InRange(slot.OrbX + radius, 0, layout.Width);
            Assert.InRange(slot.OrbY - radius, 0, layout.Height);
            Assert.InRange(slot.OrbY + radius, 0, layout.Height);

            var alongX = slot.OrbX - layout.OriginX;
            Assert.True(alongX < 8, $"orb crossed into the slab at x={alongX:0.#}");
        }

        Assert.True(layout.Slots[0].OrbY < layout.Slots[^1].OrbY, "Host should sit above Mailbox");
        var nearest = layout.Slots.Max(s => s.OrbX - layout.OriginX);
        Assert.InRange(nearest, -90, 8);
    }

    [Fact]
    public void TightHeadroom_KeepsTheRingOnScreen()
    {
        const double above = 36;
        var layout = RadialGeometry.Compute(8, leftDip: 480, aboveDip: above);

        Assert.Equal(8, layout.Slots.Count);
        foreach (var slot in layout.Slots)
        {
            var alongY = slot.OrbY - layout.OriginY;
            Assert.True(alongY >= -(above + 1), $"orb y {alongY:0.#} is above the free space ({above})");
            Assert.InRange(slot.OrbX, 0, layout.Width);
            Assert.InRange(slot.OrbY, 0, layout.Height);
        }

        Assert.True(layout.CrownY >= -(above + 2), $"top rim y {layout.CrownY:0.#} leaves the screen");
    }
}
