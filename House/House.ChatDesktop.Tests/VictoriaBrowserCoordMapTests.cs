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
}
