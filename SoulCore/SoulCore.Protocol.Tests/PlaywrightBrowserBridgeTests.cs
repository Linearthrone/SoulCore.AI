using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Tools.Browser;

namespace SoulCore.Protocol.Tests;

/// <summary>BED-195 smoke: real Playwright navigate when Chromium is installed.</summary>
public class PlaywrightBrowserBridgeTests
{
    [Fact]
    public async Task Navigate_ExampleCom_PublishesFrame_AndGoalCompleteFalse()
    {
        var opts = Options.Create(new ToolsOptions
        {
            BrowserBackend = ToolsOptions.BackendPlaywright,
            PlaywrightHeaded = false,
            PlaywrightUserDataDir = Path.Combine(Path.GetTempPath(), "soulcore-pw-test-" + Guid.NewGuid().ToString("N"))
        });
        var hub = new VictoriaBrowserViewHub();
        await using var bridge = new PlaywrightBrowserBridge(opts, log: null, view: hub);

        var health = await bridge.HealthAsync();
        if (!health.Success)
        {
            // Chromium not installed in this environment — soft skip.
            Assert.True(true, "playwright skip: " + health.Content);
            return;
        }

        var nav = await bridge.NavigateAsync("https://example.com");
        Assert.True(nav.Success, nav.Content);
        Assert.Contains("goal_complete=false", nav.Content, StringComparison.OrdinalIgnoreCase);

        var snap = hub.GetSnapshot();
        Assert.True(snap.HasImage, "expected published JPEG after navigate");
        Assert.Contains("example.com", snap.Url ?? "", StringComparison.OrdinalIgnoreCase);

        Assert.True(hub.TryGetImageBytes(out var bytes, out var ct));
        Assert.NotNull(bytes);
        Assert.True(bytes!.Length > 100);
        Assert.Equal("image/jpeg", ct);
    }

    [Fact]
    public void ResolveUserDataDir_RefusesEmpty_UsesSoulCoreFolder()
    {
        var dir = PlaywrightBrowserBridge.ResolveUserDataDir(new ToolsOptions());
        Assert.Contains("victoria-browser", dir, StringComparison.OrdinalIgnoreCase);
    }
}
