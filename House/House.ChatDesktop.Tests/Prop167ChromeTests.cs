using Avalonia;
using Avalonia.Media;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public class Prop167ChromeTests
{
    [Fact]
    public void CaptionWords_HitOutsideTheDot_ForCreateAgentContactsAndFavorites()
    {
        var at = new Point(40, 180);
        foreach (var caption in new[] { "Create agent", "Contacts", "Nova" })
        {
            var width = caption.Length * 7d;
            const double height = 14;
            var words = new Point(at.X + RadialSlotHit.CaptionLead + (width / 2), at.Y);
            var hit = RadialSlotHit.For(at, width, height);
            var dotOnly = RadialSlotHit.Grow(RadialSlotHit.Dot(at), RadialSlotHit.DotPad);

            Assert.True(hit.Contains(words));
            Assert.False(dotOnly.Contains(words));
            Assert.True(RadialSlotHit.Caption(at, width, height).Contains(words));
        }
    }

    [Fact]
    public void RadialFill_IsMoreTranslucentThanAlpha230_AndLightBlue()
    {
        var previous = Color.FromArgb(230, 247, 250, 255);
        var fill = RadialTabChrome.FillColor;

        Assert.True(fill.A < previous.A);
        Assert.True(fill.A < 230);
        Assert.True(fill.B > fill.G && fill.G > fill.R);
        Assert.True(fill.B - fill.R > previous.B - previous.R);
        Assert.True(fill.R > 190 && fill.G > 210 && fill.B > 240);

        Assert.Equal(255, RadialTabChrome.MarkColor.A);
        Assert.True(RadialTabChrome.MarkColor.R < 40);
        Assert.True(RadialTabChrome.StrokeColor.B > RadialTabChrome.StrokeColor.R);

        Assert.NotEqual(fill, ViewportStill.FillColor);
        Assert.Equal(255, ViewportStill.FillColor.A);
        Assert.False(ViewportStill.Frosted);
        Assert.False(ViewportStill.HostsGuestHwnd);
        Assert.Equal(1d, ViewportStill.WindowOpacity);
    }

    [Fact]
    public void ClosedTab_SitsOnTheTopEdge_AndOpeningHangsDownFromIt()
    {
        var closed = ViewportEdgeLayout.Place(10, 20, 1920, 1080, open: false, scale: 1);
        var quarter = ViewportLayout.OfWorkArea(10, 20, 1920, 1080);

        Assert.False(closed.Open);
        Assert.False(closed.DrawerVisible);
        Assert.Equal(20, closed.TabY);
        Assert.Equal((int)ViewportEdgeLayout.ClosedTabHeightDip, closed.TabHeightPx);
        Assert.Equal((int)ViewportEdgeLayout.ClosedTabWidthDip, closed.TabWidthPx);
        Assert.Equal(10 + (1920 - closed.TabWidthPx) / 2, closed.TabX);
        Assert.True(closed.TabWidthPx < quarter.WidthPx);
        Assert.True(closed.TabHeightPx < quarter.HeightPx);

        var open = ViewportEdgeLayout.Place(10, 20, 1920, 1080, open: true, scale: 1);
        Assert.True(open.Open);
        Assert.True(open.DrawerVisible);
        Assert.Equal(closed.TabY, open.TabY);
        Assert.Equal(open.TabY + open.TabHeightPx, open.DrawerY);
        Assert.Equal(open.TabX, open.DrawerX);
        Assert.Equal(quarter.WidthPx, open.DrawerWidthPx);
        Assert.Equal(quarter.HeightPx, open.TabHeightPx + open.DrawerHeightPx);
        Assert.True(open.DrawerHeightPx > closed.TabHeightPx);

        var hidpi = ViewportEdgeLayout.Place(0, 100, 2000, 1200, open: false, scale: 2);
        Assert.False(hidpi.Open);
        Assert.False(hidpi.DrawerVisible);
        Assert.Equal(100, hidpi.TabY);
        Assert.Equal((int)Math.Round(ViewportEdgeLayout.ClosedTabHeightDip * 2), hidpi.TabHeightPx);

        Assert.False(ViewportEdgeLayout.DragPullsOpen(0));
        Assert.False(ViewportEdgeLayout.CountsAsDrag(0));
        Assert.True(ViewportEdgeLayout.DragPullsOpen(ViewportEdgeLayout.DragOpenDip));
        Assert.False(ViewportEdgeLayout.DragPullsOpen(-ViewportEdgeLayout.DragOpenDip));
    }
}
