using SoulCore.Inference.Tools.Browser;
using SoulCore.Inference.Tools.Desktop;
using Xunit;

namespace SoulCore.Protocol.Tests;

public class VictoriaBrowserViewHubTests
{
    [Fact]
    public void Publish_SetsBackend_AndPreservesImage()
    {
        var hub = new VictoriaBrowserViewHub();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 };

        hub.Publish(png, null, "victoria-sandbox", "desktop_screenshot", backend: VictoriaBrowserViewHub.BackendVboxGuest);

        var snap = hub.GetSnapshot();
        Assert.True(snap.HasImage);
        Assert.Equal(VictoriaBrowserViewHub.BackendVboxGuest, snap.Backend);
        Assert.Equal("victoria-sandbox", snap.Title);
        Assert.Equal("desktop_screenshot", snap.LastAction);
        Assert.True(hub.TryGetImageBytes(out var bytes, out var ct));
        Assert.Equal(png, bytes);
        Assert.Equal("image/png", ct);
    }

    [Fact]
    public void TryPublishFromToolData_MirrorsGuestBytes()
    {
        var hub = new VictoriaBrowserViewHub();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 9, 9, 9 };

        Assert.True(VictoriaBrowserViewHub.TryPublishFromToolData(
            hub,
            new { bytes = png, width = 2, height = 2, format = "png" },
            "desktop_screenshot"));

        var snap = hub.GetSnapshot();
        Assert.Equal(VictoriaBrowserViewHub.BackendVboxGuest, snap.Backend);
        Assert.Equal(2, snap.FrameWidth);
        Assert.Equal(2, snap.FrameHeight);
        // Soft-cursor seeded at frame center so Her screen shows pink before first click.
        Assert.Equal(1, snap.CursorX);
        Assert.Equal(1, snap.CursorY);
        Assert.Equal(VictoriaBrowserViewHub.CursorIdle, snap.CursorState);
        Assert.True(hub.TryGetImageBytes(out var bytes, out _));
        Assert.Equal(png, bytes);
    }

    [Fact]
    public void RecordCursor_ExposesPinkTealState()
    {
        var hub = new VictoriaBrowserViewHub();
        hub.RecordCursor(40, 30, VictoriaBrowserViewHub.CursorIdle);
        var idle = hub.GetSnapshot();
        Assert.Equal(40, idle.CursorX);
        Assert.Equal(30, idle.CursorY);
        Assert.Equal(VictoriaBrowserViewHub.CursorIdle, idle.CursorState);
        Assert.NotNull(idle.CursorAt);

        hub.RecordCursor(80, 60, VictoriaBrowserViewHub.CursorClick);
        var click = hub.GetSnapshot();
        Assert.Equal(80, click.CursorX);
        Assert.Equal(60, click.CursorY);
        Assert.Equal(VictoriaBrowserViewHub.CursorClick, click.CursorState);
    }

    [Theory]
    [InlineData("clicked left at (10,20)", "click")]
    [InlineData("agent cursor → (10,20)", "idle")]
    [InlineData("dragged left from (1,1) to (2,2)", "click")]
    [InlineData("typed 3 character(s)", "idle")]
    [InlineData("browser click (10,20)", "click")]
    public void InferSoftCursorState_FromDesktopAction(string action, string expected) =>
        Assert.Equal(expected, DesktopViewHub.InferSoftCursorState(action));

    [Theory]
    [InlineData("vbox-guest", "none", true)]
    [InlineData("playwright", "vm", true)]
    [InlineData("playwright", "playwright", false)]
    [InlineData("playwright", "none", false)]
    public void WantsPresenceSoftCursor_OnlyVmOrGuest(string backend, string surface, bool expected) =>
        Assert.Equal(expected, VictoriaBrowserViewHub.WantsPresenceSoftCursor(backend, surface));

    [Fact]
    public void DesktopViewHub_MirrorsCursorToBrowserHub()
    {
        var browser = new VictoriaBrowserViewHub();
        var desktop = new DesktopViewHub(mirrorCursor: (x, y, state) => browser.RecordCursor(x, y, state));
        desktop.RecordAction("clicked left at (12,34)", 12, 34);
        var snap = browser.GetSnapshot();
        Assert.Equal(12, snap.CursorX);
        Assert.Equal(34, snap.CursorY);
        Assert.Equal(VictoriaBrowserViewHub.CursorClick, snap.CursorState);
    }

    [Fact]
    public async Task DesktopClick_PublishesAimThenClickToPresenceHub()
    {
        var browser = new VictoriaBrowserViewHub();
        var desktop = new DesktopViewHub(mirrorCursor: (x, y, state) => browser.RecordCursor(x, y, state));
        var backend = new ClickOnlyBackend();
        var gate = new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true);
        // Zero dwell so the test stays fast — SoftCursorPresenceFeedback still records.
        var prevAim = SoftCursorPresenceFeedback.AimLeadMsForTests;
        var prevClick = SoftCursorPresenceFeedback.ClickDwellMsForTests;
        SoftCursorPresenceFeedback.AimLeadMsForTests = 0;
        SoftCursorPresenceFeedback.ClickDwellMsForTests = 0;
        try
        {
            var tool = new DesktopClickTool(gate, backend, desktop);
            var result = await tool.ExecuteAsync(
                System.Text.Json.JsonDocument.Parse("""{"x":55,"y":66}""").RootElement);

            Assert.True(result.Success);
            Assert.Single(backend.Clicks);
            Assert.Equal((55, 66), backend.Clicks[0]);
            var snap = browser.GetSnapshot();
            Assert.Equal(55, snap.CursorX);
            Assert.Equal(66, snap.CursorY);
            Assert.Equal(VictoriaBrowserViewHub.CursorClick, snap.CursorState);
        }
        finally
        {
            SoftCursorPresenceFeedback.AimLeadMsForTests = prevAim;
            SoftCursorPresenceFeedback.ClickDwellMsForTests = prevClick;
        }
    }

    private sealed class ClickOnlyBackend : IDesktopControlBackend
    {
        public List<(int x, int y)> Clicks { get; } = new();

        public Task<DesktopOpResult> ScreenshotAsync(int monitor, CancellationToken ct = default) =>
            Task.FromResult(new DesktopOpResult(true, "shot", null));

        public Task<DesktopOpResult> ClickAsync(
            int x, int y, string button, int clicks = 1, CancellationToken ct = default)
        {
            Clicks.Add((x, y));
            return Task.FromResult(new DesktopOpResult(true, $"clicked {button} at ({x},{y})", null));
        }

        public Task<DesktopOpResult> DragAsync(
            int x1, int y1, int x2, int y2, string button, CancellationToken ct = default) =>
            Task.FromResult(new DesktopOpResult(true, "drag", null));

        public Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default) =>
            Task.FromResult(new DesktopOpResult(true, "type", null));

        public Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(new DesktopOpResult(true, "key", null));

        public Task<DesktopOpResult> ScrollAsync(
            int x, int y, int deltaY, int deltaX = 0, CancellationToken ct = default) =>
            Task.FromResult(new DesktopOpResult(true, "scroll", null));

        public Task<DesktopOpResult> OpenAppAsync(
            string app, string? arguments = null, CancellationToken ct = default) =>
            Task.FromResult(new DesktopOpResult(true, "open", null));

        public Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default) =>
            Task.FromResult(new DesktopOpResult(true, "windows", null));

        public Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default) =>
            Task.FromResult(new DesktopOpResult(true, "focus", null));
    }
}
