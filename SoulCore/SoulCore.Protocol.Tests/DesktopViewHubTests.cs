using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

public class DesktopViewHubTests
{
    [Fact]
    public void RecordScreenshot_TracksSourceEyes()
    {
        var dir = NewTempGallery();
        try
        {
            var hub = new DesktopViewHub(() => true, dir);
            var bytes = new byte[] { 1, 2, 3, 4, 5 };
            hub.RecordScreenshot(bytes, "png", 64, 48, null, DesktopViewHub.SourceEyes, "eye_capture");
            var snap = hub.GetSnapshot();
            Assert.True(snap.HasImage);
            Assert.Equal(DesktopViewHub.SourceEyes, snap.Source);
            Assert.Equal("eye_capture", snap.LastAction);
            Assert.Equal(bytes, hub.TryGetImageBytes());
            Assert.NotNull(snap.ImagePath);
            Assert.True(File.Exists(snap.ImagePath!));
            Assert.Equal(dir, snap.GalleryDir);
            Assert.NotNull(snap.Recent);
            Assert.Single(snap.Recent!);
            Assert.Equal(DesktopViewHub.SourceEyes, snap.Recent![0].Source);
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Fact]
    public void TryRecordFromToolData_ReadsAnonymousBytes()
    {
        var dir = NewTempGallery();
        try
        {
            var hub = new DesktopViewHub(() => true, dir);
            var data = new { bytes = new byte[] { 9, 8, 7 }, format = "png", width = 10, height = 12 };
            Assert.True(DesktopViewHub.TryRecordFromToolData(
                hub, data, DesktopViewHub.SourceDesktop, "desktop_screenshot"));
            var snap = hub.GetSnapshot();
            Assert.Equal(10, snap.Width);
            Assert.Equal(12, snap.Height);
            Assert.Equal(DesktopViewHub.SourceDesktop, snap.Source);
            Assert.True(File.Exists(snap.ImagePath!));
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Fact]
    public void RecordScreenshot_GalleryRingBuffer_PrunesOldest()
    {
        var dir = NewTempGallery();
        try
        {
            var hub = new DesktopViewHub(() => true, dir);
            for (var i = 0; i < DesktopViewHub.MaxGalleryItems + 3; i++)
            {
                hub.RecordScreenshot(
                    new byte[] { (byte)(i % 250), 2, 3 },
                    "png",
                    8,
                    8,
                    null,
                    DesktopViewHub.SourceDesktop,
                    $"shot-{i}");
            }

            var snap = hub.GetSnapshot();
            Assert.NotNull(snap.Recent);
            Assert.Equal(DesktopViewHub.MaxGalleryItems, snap.Recent!.Count);
            Assert.Equal($"shot-{DesktopViewHub.MaxGalleryItems + 2}", snap.Recent[0].Action);
            Assert.Equal(DesktopViewHub.MaxGalleryItems, Directory.GetFiles(dir).Length);
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Fact]
    public void TryGetGalleryImageBytes_RejectsPathTraversal()
    {
        var dir = NewTempGallery();
        try
        {
            var hub = new DesktopViewHub(() => true, dir);
            hub.RecordScreenshot(new byte[] { 1, 2, 3 }, "png", 1, 1, null, "desktop", "a");
            Assert.Null(hub.TryGetGalleryImageBytes("../secret.png"));
            Assert.Null(hub.TryGetGalleryImageBytes("..\\secret.png"));
            var name = hub.GetSnapshot().Recent![0].FileName;
            Assert.Equal(new byte[] { 1, 2, 3 }, hub.TryGetGalleryImageBytes(name));
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static string NewTempGallery()
    {
        var dir = Path.Combine(Path.GetTempPath(), "soulcore-gallery-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // ignore
        }
    }
}
