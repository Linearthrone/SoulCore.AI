using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

public class ToolImagePayloadTests
{
    [Fact]
    public void TryExtractBase64Images_FromAnonymousObject_ReturnsPng()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };
        var data = new { bytes = png, format = "png" };
        var images = ToolImagePayload.TryExtractBase64Images(data);
        Assert.NotNull(images);
        Assert.Single(images!);
        Assert.Equal(Convert.ToBase64String(png), images![0]);
    }

    [Fact]
    public void TryExtractBase64Images_OverMaxBytes_ReturnsNull()
    {
        var huge = new byte[4_000_001];
        huge[0] = 0x89;
        var images = ToolImagePayload.TryExtractBase64Images(new { bytes = huge });
        Assert.Null(images);
    }
}
