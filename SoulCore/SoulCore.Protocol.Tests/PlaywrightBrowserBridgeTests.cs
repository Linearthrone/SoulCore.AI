using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
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
            "Executable doesn't exist at %USERPROFILE%\\.cache\\ms-playwright\\chromium-1148\\chrome-win\\chrome.exe");
        var msg = PlaywrightBrowserBridge.FormatPlaywrightError("navigate", ex);
        Assert.Contains("not set up yet", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("install-playwright.ps1", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("powershell", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ms-playwright", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".\\ALLSTART.ps1 -RestartHost", msg, StringComparison.Ordinal);
        Assert.Contains("VirtualBox is NOT required", msg, StringComparison.Ordinal);
        Assert.True(PlaywrightBrowserBridge.LooksLikeMissingBrowser(ex));
    }

    [Fact]
    public void FormatPlaywrightError_MissingHeadlessShell_IncludesInstallRecipe()
    {
        var ex = new InvalidOperationException(
            "Executable doesn't exist at %USERPROFILE%\\AppData\\Local\\ms-playwright\\chromium_headless_shell-1148\\chrome-win\\headless_shell.exe");
        var msg = PlaywrightBrowserBridge.FormatPlaywrightError("health", ex);
        Assert.Contains("not set up yet", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("install-playwright.ps1", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VirtualBox is NOT required", msg, StringComparison.Ordinal);
        Assert.True(PlaywrightBrowserBridge.LooksLikeMissingBrowser(ex));
    }

    [Fact]
    public void ExpectedChromiumPaths_IncludeRevision1148()
    {
        var paths = PlaywrightBrowserBridge.ExpectedChromiumExecutablePaths().ToList();
        Assert.NotEmpty(paths);
        Assert.All(paths, p => Assert.Contains("chromium-1148", p, StringComparison.OrdinalIgnoreCase));
        Assert.All(paths, p => Assert.Contains("chrome.exe", p, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormatClickTextResult_Unchanged_WarnsNotToClaimNextScreen()
    {
        var msg = PlaywrightBrowserBridge.FormatClickTextResult(
            "Continue", 1, "button",
            "https://example.com/a", "https://example.com/a",
            pageChanged: false);
        Assert.Contains("did NOT change", msg, StringComparison.Ordinal);
        Assert.Contains("Do NOT claim the next screen", msg, StringComparison.Ordinal);
        Assert.Contains("browser_snapshot", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("goal_complete=false", msg, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatClickTextResult_Changed_RequiresSnapshotBeforeWaiting()
    {
        var msg = PlaywrightBrowserBridge.FormatClickTextResult(
            "Sign in", 1, "link",
            "https://example.com/login", "https://example.com/app",
            pageChanged: true);
        Assert.Contains("page changed", msg, StringComparison.Ordinal);
        Assert.Contains("browser_snapshot", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("before telling Kayleigh you are waiting", msg, StringComparison.Ordinal);
    }

    [Fact]
    public void AimDwellMs_ExceedsPresenceBrowserPollInterval()
    {
        // House Presence refreshes Her browser every ~200ms — aim must outlast several polls.
        Assert.True(PlaywrightBrowserBridge.AimDwellMs >= 600,
            $"AimDwellMs={PlaywrightBrowserBridge.AimDwellMs} must leave the aim frame up long enough to see");
    }

    [Theory]
    [InlineData("Login", true)]
    [InlineData("log in", true)]
    [InlineData("Sign in", true)]
    [InlineData("SIGN-IN", true)]
    [InlineData("Continue", false)]
    [InlineData("Next", false)]
    public void IsLoginFamilyLabel_DetectsCommonVariants(string label, bool expected)
    {
        Assert.Equal(expected, PlaywrightBrowserBridge.IsLoginFamilyLabel(label));
    }

    [Fact]
    public void ExpandClickLabels_Login_IncludesSignInAliases()
    {
        var labels = PlaywrightBrowserBridge.ExpandClickLabels("Login");
        Assert.Contains("Login", labels);
        Assert.Contains("Sign in", labels);
        Assert.Contains("Log in", labels);
        Assert.True(labels.Count >= 3);
    }

    [Fact]
    public void ExpandClickLabels_NonLogin_StaysSingle()
    {
        var labels = PlaywrightBrowserBridge.ExpandClickLabels("Accept cookies");
        Assert.Equal(new[] { "Accept cookies" }, labels);
    }

    [Fact]
    public void BurnInMarker_ClickState_UsesTealAccent()
    {
        using var img = new Image<Rgba32>(80, 60, new Rgba32(20, 20, 20));
        using var ms = new MemoryStream();
        img.Save(ms, new JpegEncoder { Quality = 80 });
        var original = ms.ToArray();

        var marked = PlaywrightClickCursor.BurnInMarker(
            original, 40, 30, PlaywrightClickCursor.MarkerState.Click);
        using var loaded = Image.Load<Rgba32>(marked);
        var sample = loaded[40, 30 - 15];
        // Teal #2ec4b6 → G and B elevated vs flat gray.
        Assert.True(sample.G > 100 || sample.B > 100,
            $"expected teal accent on ring, got {sample}");
    }

    [Fact]
    public void InitScript_DefinesIdlePinkAndClickTeal()
    {
        Assert.Contains(PlaywrightClickCursor.IdleHex, PlaywrightClickCursor.InitScript, StringComparison.Ordinal);
        Assert.Contains(PlaywrightClickCursor.ClickHex, PlaywrightClickCursor.InitScript, StringComparison.Ordinal);
        Assert.Contains("__scShowClick", PlaywrightClickCursor.InitScript, StringComparison.Ordinal);
        Assert.Contains("__scMoveCursor", PlaywrightClickCursor.InitScript, StringComparison.Ordinal);
        Assert.Contains(PlaywrightClickCursor.FlashMs.ToString(), PlaywrightClickCursor.InitScript, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"C:\Users\x\AppData\Local\ms-playwright\chromium-1148\chrome-win\chrome.exe", true)]
    [InlineData(@"C:\Program Files\Google\Chrome\Application\chrome.exe", false)]
    [InlineData(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", false)]
    public void IsVictoriaPlaywrightChromium_FiltersOperatorBrowsers(string path, bool expected)
    {
        Assert.Equal(expected, VictoriaChromiumWindowLocator.IsVictoriaPlaywrightChromium(path));
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

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://localhost:8080/")]
    [InlineData("http://10.0.0.1/")]
    [InlineData("http://192.168.1.1/")]
    [InlineData("http://172.16.5.1/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://metadata.google.internal/")]
    public void IsDisallowedNavigateHost_RejectsPrivateAndMetadata(string url)
    {
        Assert.True(PlaywrightBrowserBridge.IsDisallowedNavigateHost(url));
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("https://www.wikipedia.org/wiki/Test")]
    public void IsDisallowedNavigateHost_AllowsPublicHttps(string url)
    {
        Assert.False(PlaywrightBrowserBridge.IsDisallowedNavigateHost(url));
    }

    [Fact]
    public async Task Navigate_PrivateHost_RefusedWithoutPublish()
    {
        var opts = Options.Create(new ToolsOptions
        {
            BrowserBackend = ToolsOptions.BackendPlaywright,
            AllowBrowserCapture = true,
            PlaywrightHeaded = false,
            PlaywrightUserDataDir = Path.Combine(Path.GetTempPath(), "soulcore-pw-priv-" + Guid.NewGuid().ToString("N"))
        });
        var hub = new VictoriaBrowserViewHub();
        await using var bridge = new PlaywrightBrowserBridge(opts, log: null, view: hub);

        var nav = await bridge.NavigateAsync("http://127.0.0.1/");
        Assert.False(nav.Success);
        Assert.Contains("refused", nav.Content, StringComparison.OrdinalIgnoreCase);
        Assert.False(hub.GetSnapshot().HasImage);
    }
}
