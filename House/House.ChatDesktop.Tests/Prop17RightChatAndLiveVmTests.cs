using Avalonia.Controls;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public class Prop17RightChatAndLiveVmTests
{
    [Fact]
    public void ClosedTab_SitsOnTheRightEdge_AndOpenBodyIsTheRightHalf()
    {
        var closed = ChatEdgeLayout.Place(10, 20, 1920, 1080, open: false, scale: 1);

        Assert.False(closed.Open);
        Assert.True(closed.TabStaysVisible);
        Assert.Equal(0, closed.BodyWidthPx);
        Assert.Equal((int)ChatEdgeLayout.ClosedTabWidthDip, closed.WidthPx);
        Assert.Equal((int)ChatEdgeLayout.ClosedTabHeightDip, closed.HeightPx);
        Assert.Equal(36, closed.TabWidthPx);
        Assert.Equal(120, closed.TabHeightPx);
        Assert.Equal(10 + 1920 - 36, closed.X);
        Assert.Equal(closed.X, closed.TabX);
        Assert.Equal(10 + 1920, closed.X + closed.WidthPx);
        Assert.Equal(20 + ((1080 - 120) / 2), closed.Y);
        Assert.Equal(20 + 540, closed.Y + (closed.HeightPx / 2));

        var open = ChatEdgeLayout.Place(10, 20, 1920, 1080, open: true, scale: 1);
        var half = 1920 / 2;

        Assert.True(open.Open);
        Assert.True(open.TabStaysVisible);
        Assert.Equal(half, open.BodyWidthPx);
        Assert.Equal(1080, open.BodyHeightPx);
        Assert.Equal(20, open.BodyY);
        Assert.Equal(open.BodyX + open.BodyWidthPx, open.TabX);
        Assert.Equal(10 + 1920 - open.TabWidthPx, open.TabX);
        Assert.Equal(10 + 1920, open.TabX + open.TabWidthPx);
        Assert.Equal(36, open.TabWidthPx);
        Assert.Equal(1080, open.TabHeightPx);
        Assert.Equal(open.BodyWidthPx + open.TabWidthPx, open.WidthPx);
        Assert.Equal(open.X, open.BodyX);

        var hidpi = ChatEdgeLayout.Place(0, 100, 2000, 1200, open: false, scale: 2);
        Assert.False(hidpi.Open);
        Assert.Equal((int)Math.Round(ChatEdgeLayout.ClosedTabWidthDip * 2), hidpi.WidthPx);
        Assert.Equal((int)Math.Round(ChatEdgeLayout.ClosedTabHeightDip * 2), hidpi.HeightPx);
        Assert.Equal(2000 - hidpi.WidthPx, hidpi.X);
        Assert.Equal(100 + ((1200 - hidpi.HeightPx) / 2), hidpi.Y);

        Assert.False(ChatEdgeLayout.DragPullsOpen(0));
        Assert.False(ChatEdgeLayout.DragPullsOpen(-15));
        Assert.True(ChatEdgeLayout.DragPullsOpen(-ChatEdgeLayout.DragOpenDip));
        Assert.True(ChatEdgeLayout.DragPullsOpen(-40));
        Assert.False(ChatEdgeLayout.DragPullsOpen(ChatEdgeLayout.DragOpenDip));
        Assert.False(ChatEdgeLayout.CountsAsDrag(15));
        Assert.True(ChatEdgeLayout.CountsAsDrag(ChatEdgeLayout.DragOpenDip));
        Assert.True(ChatEdgeLayout.CountsAsDrag(-ChatEdgeLayout.DragOpenDip));
        Assert.Equal(16, ChatEdgeLayout.DragOpenDip);
    }

    [Fact]
    public void FirstLaunch_IsTheClosedTab_AndClosingDoesNotHideOrQuit()
    {
        Assert.True(ChatDrawerPolicy.ShouldStartClosed());
        Assert.False(ChatDrawerPolicy.ShouldUseSavedFloatingRect());
        Assert.False(ChatDrawerPolicy.ShouldHideWindowWhenClosed());
        Assert.False(ChatDrawerPolicy.ShouldQuitWhenClosed());
        Assert.False(ChatDrawerPolicy.ShouldHideOnOpen());

        Assert.Equal(168, RadialTabChrome.FillColor.A);
        Assert.Equal(206, RadialTabChrome.FillColor.R);
        Assert.Equal(228, RadialTabChrome.FillColor.G);
        Assert.Equal(255, RadialTabChrome.FillColor.B);
        Assert.Equal(0x4B, RadialTabChrome.StrokeColor.R);
        Assert.Equal(0xA3, RadialTabChrome.StrokeColor.G);
        Assert.Equal(0xFF, RadialTabChrome.StrokeColor.B);
    }

    [Fact]
    public void NewFrame_ReplacesThePreviousStill_AndTheCaptionIsLive()
    {
        var slot = new ViewportStillSlot();
        Assert.False(slot.HasFrame);
        Assert.Equal(GuestPicture.NotReady, slot.Caption);
        Assert.Contains("not ready", slot.Caption, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("guest", slot.Caption, StringComparison.OrdinalIgnoreCase);

        var first = new byte[] { 0xFF, 0xD8, 0x01 };
        var second = new byte[] { 0xFF, 0xD8, 0x02 };

        Assert.False(slot.Show(first));
        Assert.True(slot.HasFrame);
        Assert.Equal(1, slot.Generation);
        Assert.Equal(first, slot.Current);
        Assert.True(GuestPicture.IsLiveCaption(slot.Caption));
        Assert.DoesNotContain("not a live guest", slot.Caption, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("still, not a live guest", slot.Caption, StringComparison.OrdinalIgnoreCase);

        Assert.True(slot.Show(second));
        Assert.Equal(2, slot.Generation);
        Assert.Equal(second, slot.Current);
        Assert.Equal(first, slot.Previous);
        Assert.NotSame(slot.Previous, slot.Current);
        Assert.NotEqual(slot.Previous, slot.Current);
        Assert.Equal(GuestPicture.LiveCaption, slot.Caption);

        slot.Clear();
        Assert.False(slot.HasFrame);
        Assert.Null(slot.Current);
        Assert.Equal(GuestPicture.NotReady, slot.Caption);

        Assert.Contains("still, not a live guest", ViewportStill.Label, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(ViewportStill.Label, GuestPicture.LiveCaption);
        Assert.False(ViewportStill.HostsGuestHwnd);
        Assert.False(ViewportStill.Frosted);
        Assert.False(ViewportStill.AllowsClickthroughOnStill);
        Assert.Equal(1d, ViewportStill.WindowOpacity);
        Assert.Equal(255, ViewportStill.FillColor.A);

        var mismatch = GuestEmbedFit.Decide(960, 540, 1920, 1080, false, false);
        Assert.False(mismatch.Allow);
        Assert.Equal(GuestEmbedFit.ScaleStop, mismatch.Detail);
        Assert.False(ViewportStill.MayParent(960, 540, 1920, 1080));

        Assert.False(GuestWindowGrab.ParentsHwnd);
        Assert.False(GuestWindowGrab.MovesWindow);
        Assert.False(GuestWindowGrab.InjectsClicks);
        Assert.Equal(5, GuestWindowGrab.FramesPerSecond);
        Assert.Equal(200, GuestWindowGrab.IntervalMs);
        Assert.True(ViewportMaterial.AllowsGuest(new[] { WindowTransparencyLevel.None }));
        Assert.False(ViewportMaterial.AllowsGuest(new[] { WindowTransparencyLevel.AcrylicBlur }));
    }

    [Fact]
    public void Grab_CopiesPixels_WithoutParentingTheWindow()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var probe = GuestWindowGrab.Probe();
        Assert.True(probe.Ran, probe.Detail);
        Assert.True(probe.ParentUnchanged, probe.Detail);
        Assert.Equal(probe.ParentBefore, probe.ParentAfter);
        Assert.True(probe.CopiedBmp, probe.Detail);
        Assert.True(probe.ByteLength > 54, probe.Detail);
        Assert.False(GuestWindowGrab.ParentsHwnd);
        Assert.False(GuestWindowGrab.InjectsClicks);
        Assert.False(GuestWindowGrab.MovesWindow);
    }
}
