using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

public class DesktopViewHubTests
{
    [Fact]
    public void RecordScreenshot_TracksSourceEyes()
    {
        var hub = new DesktopViewHub(() => true);
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        hub.RecordScreenshot(bytes, "png", 64, 48, null, DesktopViewHub.SourceEyes, "eye_capture");
        var snap = hub.GetSnapshot();
        Assert.True(snap.HasImage);
        Assert.Equal(DesktopViewHub.SourceEyes, snap.Source);
        Assert.Equal("eye_capture", snap.LastAction);
        Assert.Equal(bytes, hub.TryGetImageBytes());
    }

    [Fact]
    public void TryRecordFromToolData_ReadsAnonymousBytes()
    {
        var hub = new DesktopViewHub();
        var data = new { bytes = new byte[] { 9, 8, 7 }, format = "png", width = 10, height = 12 };
        Assert.True(DesktopViewHub.TryRecordFromToolData(
            hub, data, DesktopViewHub.SourceDesktop, "desktop_screenshot"));
        var snap = hub.GetSnapshot();
        Assert.Equal(10, snap.Width);
        Assert.Equal(12, snap.Height);
        Assert.Equal(DesktopViewHub.SourceDesktop, snap.Source);
    }
}
