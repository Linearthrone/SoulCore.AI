using House.ChatDesktop.Controls;

namespace House.ChatDesktop.Tests;

public class HerScreenCursorGlyphTests
{
    [Fact]
    public void Outline_HotspotIsTheTip()
    {
        Assert.Equal((0.0, 0.0), HerScreenCursorGlyph.Outline[0]);
    }

    [Fact]
    public void Outline_BodySitsDownAndRightOfTheTip()
    {
        foreach (var point in HerScreenCursorGlyph.Outline.Skip(1))
        {
            Assert.True(point.X >= 0);
            Assert.True(point.Y > 0);
        }
    }

    [Fact]
    public void BoundsAt_PlacesTopLeftOnTheTipMinusPad()
    {
        var bounds = HerScreenCursorGlyph.BoundsAt(100, 80, 22);
        Assert.Equal(98, bounds.X);
        Assert.Equal(78, bounds.Y);
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
        Assert.True(bounds.Width < bounds.Height || bounds.Width == bounds.Height);
    }
}
