using House.ChatDesktop.Services;
using Xunit;

namespace House.ChatDesktop.Tests;

public class VictoriaBrowserCoordMapTests
{
    [Fact]
    public void TryMap_CenterOfLetterboxedImage_MapsToImageCenter()
    {
        // 1280x800 frame in a 640x500 surface → scale=0.5, letterbox vertical (offsetY=50)
        var mapped = VictoriaBrowserCoordMap.TryMapPointerToPage(
            pointerX: 320,
            pointerY: 50 + 200, // middle of displayed image
            surfaceWidth: 640,
            surfaceHeight: 500,
            imagePixelWidth: 1280,
            imagePixelHeight: 800);

        Assert.NotNull(mapped);
        Assert.Equal(640, mapped!.Value.X);
        Assert.Equal(400, mapped.Value.Y);
    }

    [Fact]
    public void TryMap_OutsideLetterbox_ReturnsNull()
    {
        var mapped = VictoriaBrowserCoordMap.TryMapPointerToPage(
            pointerX: 10,
            pointerY: 10, // in top letterbox band
            surfaceWidth: 640,
            surfaceHeight: 500,
            imagePixelWidth: 1280,
            imagePixelHeight: 800);

        Assert.Null(mapped);
    }

    [Fact]
    public void TryMap_TopLeftOfImage_IsZeroZero()
    {
        var mapped = VictoriaBrowserCoordMap.TryMapPointerToPage(
            pointerX: 0,
            pointerY: 50,
            surfaceWidth: 640,
            surfaceHeight: 500,
            imagePixelWidth: 1280,
            imagePixelHeight: 800);

        Assert.NotNull(mapped);
        Assert.Equal(0, mapped!.Value.X);
        Assert.Equal(0, mapped.Value.Y);
    }

    [Fact]
    public void FormatClickHint_MatchesWhatKayleighSays()
    {
        Assert.Equal("click (412, 277)", VictoriaBrowserCoordMap.FormatClickHint(412, 277));
    }

    [Fact]
    public void FormatHerHint_ShowsAgentCursor()
    {
        Assert.Equal("her (100, 200)", VictoriaBrowserCoordMap.FormatHerHint(100, 200));
    }

    [Fact]
    public void FormatCoordBadge_OptionC_BothWhenHovering()
    {
        Assert.Equal(
            "her (100, 200) · you click (412, 277)",
            VictoriaBrowserCoordMap.FormatCoordBadge(100, 200, 412, 277));
    }

    [Fact]
    public void FormatCoordBadge_OptionC_HerOnlyWhenNotHovering()
    {
        Assert.Equal("her (100, 200)", VictoriaBrowserCoordMap.FormatCoordBadge(100, 200, null, null));
    }

    [Fact]
    public void FormatCoordBadge_HoverOnly_WhenNoAgentCursor()
    {
        Assert.Equal("click (412, 277)", VictoriaBrowserCoordMap.FormatCoordBadge(null, null, 412, 277));
    }

    [Fact]
    public void FormatCoordBadge_Null_WhenNeither()
    {
        Assert.Null(VictoriaBrowserCoordMap.FormatCoordBadge(null, null, null, null));
    }

    [Fact]
    public void TryMapStretchFill_Center_MapsToGuestCenter()
    {
        // 1280x800 in a 640x400 surface — stretch (no letterbox)
        var mapped = VictoriaBrowserCoordMap.TryMapPointerStretchFill(
            pointerX: 320,
            pointerY: 200,
            surfaceWidth: 640,
            surfaceHeight: 400,
            imagePixelWidth: 1280,
            imagePixelHeight: 800);

        Assert.NotNull(mapped);
        Assert.Equal(640, mapped!.Value.X);
        Assert.Equal(400, mapped.Value.Y);
    }

    [Fact]
    public void TryMapStretchFill_TopLeft_IsZeroZero()
    {
        var mapped = VictoriaBrowserCoordMap.TryMapPointerStretchFill(
            pointerX: 0,
            pointerY: 0,
            surfaceWidth: 640,
            surfaceHeight: 400,
            imagePixelWidth: 1280,
            imagePixelHeight: 800);

        Assert.NotNull(mapped);
        Assert.Equal(0, mapped!.Value.X);
        Assert.Equal(0, mapped.Value.Y);
    }

    [Fact]
    public void TryMapStretchFill_HalfGuestPixel_RoundsNearestNotFloor()
    {
        // 2× stretch: each guest pixel is 0.5 surface px. Midway (0.25) → 0.5 guest
        // → Round AwayFromZero = 1 (Floor would keep the up-left bias at 0).
        var mapped = VictoriaBrowserCoordMap.TryMapPointerStretchFill(
            pointerX: 0.25,
            pointerY: 0.25,
            surfaceWidth: 640,
            surfaceHeight: 400,
            imagePixelWidth: 1280,
            imagePixelHeight: 800);

        Assert.NotNull(mapped);
        Assert.Equal(1, mapped!.Value.X);
        Assert.Equal(1, mapped.Value.Y);
    }

    [Fact]
    public void TryMapStretchFill_Outside_ReturnsNull()
    {
        var mapped = VictoriaBrowserCoordMap.TryMapPointerStretchFill(
            pointerX: -1,
            pointerY: 10,
            surfaceWidth: 640,
            surfaceHeight: 400,
            imagePixelWidth: 1280,
            imagePixelHeight: 800);

        Assert.Null(mapped);
    }

    [Fact]
    public void TryMapGuestToSurfaceStretchFill_RoundTripsCenter()
    {
        var guest = VictoriaBrowserCoordMap.TryMapPointerStretchFill(
            320, 200, 640, 400, 1280, 800);
        Assert.NotNull(guest);

        var surface = VictoriaBrowserCoordMap.TryMapGuestToSurfaceStretchFill(
            guest!.Value.X, guest.Value.Y,
            640, 400, 1280, 800,
            cursorWidth: 0, cursorHeight: 0);
        Assert.NotNull(surface);
        Assert.InRange(surface!.Value.Left, 319.5, 320.5);
        Assert.InRange(surface.Value.Top, 199.5, 200.5);
    }

    /// <summary>
    /// PROP-19.3: Kurt inject + soft-cursor tip share one Guest0; Absolute is +1 only at COM.
    /// Slot 900×600 ≠ FB 1280×800 — center must not land at Absolute(450,300).
    /// </summary>
    [Fact]
    public void MapSlotToInject_SlotDiffersFromFramebuffer_CenterMapsToGuestCenterAndAbsolute()
    {
        var inject = VictoriaBrowserCoordMap.MapSlotToInject(
            pointerX: 450,
            pointerY: 300,
            surfaceWidth: 900,
            surfaceHeight: 600,
            framebufferWidth: 1280,
            framebufferHeight: 800);
        Assert.NotNull(inject);
        Assert.Equal(640, inject!.Value.GuestX);
        Assert.Equal(400, inject.Value.GuestY);
        Assert.Equal(641, inject.Value.AbsX);
        Assert.Equal(401, inject.Value.AbsY);

        var tip = VictoriaBrowserCoordMap.TryMapGuestTipToSurfaceStretchFill(
            inject.Value.GuestX, inject.Value.GuestY,
            900, 600, 1280, 800);
        Assert.NotNull(tip);
        Assert.InRange(tip!.Value.TipX, 449.5, 450.5);
        Assert.InRange(tip.Value.TipY, 299.5, 300.5);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 1, 1)]
    [InlineData(899, 599, 1279, 799, 1280, 800)] // near bottom-right → last guest px
    [InlineData(0, 599, 0, 799, 1, 800)]
    [InlineData(899, 0, 1279, 0, 1280, 1)]
    public void MapSlotToInject_Edges_MatchTipInverse(
        double slotX, double slotY,
        int expectGuestX, int expectGuestY,
        int expectAbsX, int expectAbsY)
    {
        var inject = VictoriaBrowserCoordMap.MapSlotToInject(
            slotX, slotY, 900, 600, 1280, 800);
        Assert.NotNull(inject);
        Assert.Equal(expectGuestX, inject!.Value.GuestX);
        Assert.Equal(expectGuestY, inject.Value.GuestY);
        Assert.Equal(expectAbsX, inject.Value.AbsX);
        Assert.Equal(expectAbsY, inject.Value.AbsY);

        var tip = VictoriaBrowserCoordMap.TryMapGuestTipToSurfaceStretchFill(
            inject.Value.GuestX, inject.Value.GuestY, 900, 600, 1280, 800);
        Assert.NotNull(tip);
        // Tip is the inject Guest0 surface point — remapping tip → guest recovers Guest0.
        var back = VictoriaBrowserCoordMap.TryMapPointerStretchFill(
            tip!.Value.TipX, tip.Value.TipY, 900, 600, 1280, 800);
        Assert.NotNull(back);
        Assert.Equal(inject.Value.GuestX, back!.Value.X);
        Assert.Equal(inject.Value.GuestY, back.Value.Y);
    }

    [Fact]
    public void ToAbsolute_MatchesVmPointerPlan()
    {
        Assert.Equal(
            VictoriaVmPointerPlan.ToAbsolute(0, 0),
            VictoriaBrowserCoordMap.ToAbsolute(0, 0));
        Assert.Equal(
            VictoriaVmPointerPlan.ToAbsolute(639, 399),
            VictoriaBrowserCoordMap.ToAbsolute(639, 399));
    }

    [Fact]
    public void ResolveGuestFramebuffer_NeverPrefersSlotWhenHubKnown()
    {
        var fb = VictoriaBrowserCoordMap.ResolveGuestFramebuffer(
            hubWidth: 1280,
            hubHeight: 800,
            displayWidth: null,
            displayHeight: null,
            surfaceWidth: 900,
            surfaceHeight: 600);
        Assert.Equal((1280, 800), fb);
    }
}
