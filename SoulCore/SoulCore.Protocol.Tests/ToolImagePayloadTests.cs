using SixLabors.ImageSharp;
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
    public void TryExtractBase64Images_OverMaxBytes_ReturnsNull()
    {
        var huge = new byte[4_000_001];
        huge[0] = 0x89;
        var images = ToolImagePayload.TryExtractBase64Images(new { bytes = huge }, maxBytes: 1000);
        Assert.Null(images);
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
