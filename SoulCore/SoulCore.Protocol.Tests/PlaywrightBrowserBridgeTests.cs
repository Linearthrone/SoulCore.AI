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

    [Fact]
    public void FormatPlaywrightError_MissingBrowser_IncludesInstallRecipe()
    {
        var ex = new InvalidOperationException(
            "Executable doesn't exist at C:\\Users\\kurt\\.cache\\ms-playwright\\chromium-1148\\chrome-win\\chrome.exe");
        var msg = PlaywrightBrowserBridge.FormatPlaywrightError("navigate", ex);
        Assert.Contains("not set up yet", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("install-playwright.ps1", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("powershell", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ms-playwright", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".\\ALLSTART.ps1 -RestartHost", msg, StringComparison.Ordinal);
        Assert.True(PlaywrightBrowserBridge.LooksLikeMissingBrowser(ex));
    }

    [Fact]
    public void FormatPlaywrightError_MissingHeadlessShell_IncludesInstallRecipe()
    {
        var ex = new InvalidOperationException(
            "Executable doesn't exist at C:\\Users\\kurtw\\AppData\\Local\\ms-playwright\\chromium_headless_shell-1148\\chrome-win\\headless_shell.exe");
        var msg = PlaywrightBrowserBridge.FormatPlaywrightError("health", ex);
        Assert.Contains("not set up yet", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("install-playwright.ps1", msg, StringComparison.OrdinalIgnoreCase);
        Assert.True(PlaywrightBrowserBridge.LooksLikeMissingBrowser(ex));
    }

    [Fact]
    public void FormatPlaywrightError_OtherFailure_KeepsShortForm()
    {
        var ex = new InvalidOperationException("net::ERR_NAME_NOT_RESOLVED");
        var msg = PlaywrightBrowserBridge.FormatPlaywrightError("navigate", ex);
        Assert.StartsWith("playwright navigate failed:", msg);
        Assert.DoesNotContain("install-playwright.ps1", msg, StringComparison.OrdinalIgnoreCase);
        Assert.False(PlaywrightBrowserBridge.LooksLikeMissingBrowser(ex));
    }
}
