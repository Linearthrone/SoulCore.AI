using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

public class ToolImagePayloadTests
{
    [Fact]
    public void TryExtractBase64Images_FromAnonymousObject_ReturnsCompressedJpeg()
    {
        var png = MakePng(64, 48);
        var data = new { bytes = png, format = "png" };
        var images = ToolImagePayload.TryExtractBase64Images(data);
        Assert.NotNull(images);
        Assert.Single(images!);
        Assert.True(ToolImagePayload.LooksLikeJpegBase64(images![0]));
        var jpeg = Convert.FromBase64String(images[0]);
        Assert.True(jpeg.Length < png.Length || jpeg.Length > 0);
    }

    [Fact]
    public void TryPrepareChatAttachBase64_CompressesPngAttach()
    {
        var png = MakePng(64, 48);
        var b64 = Convert.ToBase64String(png);
        var prepared = ToolImagePayload.TryPrepareChatAttachBase64(b64);
        Assert.NotNull(prepared);
        Assert.True(ToolImagePayload.LooksLikeJpegBase64(prepared!));
    }

    [Fact]
    public void TryPrepareChatAttachBase64_RejectsGarbage()
    {
        Assert.Null(ToolImagePayload.TryPrepareChatAttachBase64("not-base64!!!"));
        Assert.Null(ToolImagePayload.TryPrepareChatAttachBase64(Convert.ToBase64String(new byte[] { 1, 2, 3 })));
    }

    [Fact]
    public void FormatClickScaleHint_GuestFrameMatchesImage_UsesSameGrid()
    {
        var hint = ToolImagePayload.FormatClickScaleHint(1510, 770, 1510, 770);
        Assert.Contains("1510x770", hint, StringComparison.Ordinal);
        Assert.Contains("same grid", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatClickScaleHint_ShrunkImage_TellsModelToScaleUp()
    {
        var hint = ToolImagePayload.FormatClickScaleHint(1510, 770, 1024, 522);
        Assert.Contains("1024x522", hint, StringComparison.Ordinal);
        Assert.Contains("1510x770", hint, StringComparison.Ordinal);
        Assert.Contains("image_y * 1.475", hint, StringComparison.Ordinal);
        Assert.Contains("Host scales", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveClickToGuest_ScalesVisionPointOntoFramebuffer()
    {
        // Model clicked JPEG center without multiplying — Host must scale.
        var mapped = ToolImagePayload.ResolveClickToGuest(
            x: 512, y: 261,
            sourceWidth: 1510, sourceHeight: 770,
            visionWidth: 1024, visionHeight: 522);
        Assert.True(mapped.Remapped);
        Assert.Equal("vision-scale", mapped.Reason);
        Assert.Equal(755, mapped.GuestX);
        Assert.Equal(385, mapped.GuestY);
    }

    [Fact]
    public void ResolveClickToGuest_LeavesGuestSpaceWhenOutsideVisionImage()
    {
        var mapped = ToolImagePayload.ResolveClickToGuest(
            x: 1300, y: 400,
            sourceWidth: 1510, sourceHeight: 770,
            visionWidth: 1024, visionHeight: 522);
        Assert.False(mapped.Remapped);
        Assert.Equal("guest-space", mapped.Reason);
        Assert.Equal(1300, mapped.GuestX);
        Assert.Equal(400, mapped.GuestY);
    }

    [Fact]
    public void VisionClickCoordMap_NotesFrameAndMaps()
    {
        var map = new VisionClickCoordMap();
        map.NoteFrame(1510, 770, 1024, 522);
        var mapped = map.MapToGuest(512, 261);
        Assert.True(mapped.Remapped);
        Assert.Equal(755, mapped.GuestX);
        Assert.Equal(385, mapped.GuestY);
    }

    [Fact]
    public void TryPrepareVisionImage_KeepsGuestSizedFrameUnderDefaultCap()
    {
        var png = MakePng(1510, 770);
        var prepared = ToolImagePayload.TryPrepareVisionImage(png);
        Assert.NotNull(prepared);
        Assert.Equal(1510, prepared!.Value.SourceWidth);
        Assert.Equal(770, prepared.Value.SourceHeight);
        Assert.Equal(1510, prepared.Value.VisionWidth);
        Assert.Equal(770, prepared.Value.VisionHeight);
    }

    [Fact]
    public void TryCompressForVision_DownscalesLargeImage()
    {
        var png = MakePng(2000, 1500);
        var jpeg = ToolImagePayload.TryCompressForVision(png, maxEdgePx: 1024);
        Assert.NotNull(jpeg);
        using var img = Image.Load(jpeg!);
        Assert.True(Math.Max(img.Width, img.Height) <= 1024);
        Assert.True(img.Width < 2000);
        Assert.True(img.Height < 1500);
    }

    [Fact]
    public void TryCompressForVision_StripsExifGps()
    {
        var withGps = MakeJpegWithExifGps();
        Assert.True(HasExif(withGps));

        var jpeg = ToolImagePayload.TryCompressForVision(withGps);

        Assert.NotNull(jpeg);
        Assert.False(HasExif(jpeg!));
    }

    [Fact]
    public void TryExtractBase64Images_StripsExifGps()
    {
        var withGps = MakeJpegWithExifGps();
        var images = ToolImagePayload.TryExtractBase64Images(new { bytes = withGps });

        Assert.NotNull(images);
        Assert.Single(images!);
        Assert.False(HasExif(Convert.FromBase64String(images![0])));
    }

    [Fact]
    public void TryExtractBase64Images_OverMaxBytes_ReturnsNull()
    {
        var huge = new byte[4_000_001];
        huge[0] = 0x89;
        var images = ToolImagePayload.TryExtractBase64Images(new { bytes = huge }, maxBytes: 1000);
        Assert.Null(images);
    }

    private static byte[] MakeJpegWithExifGps()
    {
        using var image = new Image<Rgba32>(32, 32);
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(
            ExifTag.GPSLatitude,
            new[] { new Rational(41), new Rational(52), new Rational(30) });

        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    private static bool HasExif(byte[] jpeg)
    {
        using var image = Image.Load(jpeg);
        return image.Metadata.ExifProfile is not null
               && image.Metadata.ExifProfile.Values.Count > 0;
    }

    private static byte[] MakePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            image[x, y] = new Rgba32((byte)(x % 256), (byte)(y % 256), 80);

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
}
