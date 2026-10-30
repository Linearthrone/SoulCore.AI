using SoulCore.Inference.Tools.Browser;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

public class DesktopToolIntentTests
{
    [Theory]
    [InlineData("look at my screen", "desktop_screenshot")]
    [InlineData("what's on my desktop?", "desktop_screenshot")]
    [InlineData("use the computer and draw a line", "desktop_screenshot")]
    [InlineData("click on the Chrome window", "desktop_screenshot")]
    // Default / null backend = VM-primary desk → screenshot-first (not click_text).
    [InlineData("click the login button", "desktop_screenshot")]
    [InlineData("sign in on the page", "desktop_screenshot")]
    [InlineData("what windows are open?", "desktop_screenshot")]
    [InlineData("call list_desktop_windows", "list_desktop_windows")]
    [InlineData("open a Google Chrome window on my desktop", "desktop_open_app")]
    [InlineData("open Google Chrome", "desktop_open_app")]
    [InlineData("launch chrome", "desktop_open_app")]
    [InlineData("start notepad", "desktop_open_app")]
    [InlineData("open file explorer", "desktop_open_app")]
    [InlineData("open edge on my desktop", "desktop_open_app")]
    [InlineData("call desktop_open_app", "desktop_open_app")]
    public void TryMatch_HighConfidence_ForcesTool(string text, string expectedTool)
    {
        Assert.True(DesktopToolIntent.TryMatch(text, out var match));
        Assert.Equal(expectedTool, match.ToolName);
    }

    [Theory]
    [InlineData("click the login button", "browser_click_text")]
    [InlineData("sign in on the page", "browser_click_text")]
    public void TryMatch_PlaywrightBackend_LoginForcesClickText(string text, string expectedTool)
    {
        Assert.True(DesktopToolIntent.TryMatch(text, "playwright", out var match));
        Assert.Equal(expectedTool, match.ToolName);
    }

    [Theory]
    [InlineData("click the login button", "desktop_screenshot")]
    [InlineData("sign in on the page", "desktop_screenshot")]
    [InlineData("click (488, 559)", "desktop_screenshot")]
    public void TryMatch_NativeBackend_LoginForcesScreenshot(string text, string expectedTool)
    {
        Assert.True(DesktopToolIntent.TryMatch(text, "native", out var match));
        Assert.Equal(expectedTool, match.ToolName);
    }

    [Fact]
    public void ComputerUseGuidance_Block_PrefersLabeledBrowserOverScreenshotFirst()
    {
        Assert.Contains("browser_click_text", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("goal_complete=false", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("After EVERY browser_click_text", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.DoesNotContain("call desktop_screenshot first and click from the PNG", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("Pixel clicks are a FALLBACK", ComputerUseGuidance.Block, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserResultHonesty_LaunchOnly_IsNotGoalComplete()
    {
        var o = BrowserResultHonesty.LaunchOnly("https://example.com", "vbox-guest");
        var json = System.Text.Json.JsonSerializer.Serialize(o);
        Assert.Contains("\"goal_complete\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"action_ok\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"load_verified\":false", json, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserResultHonesty_RedactSecrets_ScrubsPasswordAssignments()
    {
        var scrubbed = BrowserResultHonesty.RedactSecrets("filled password=hunter2 ok");
        Assert.DoesNotContain("hunter2", scrubbed, StringComparison.Ordinal);
        Assert.Contains("[redacted]", scrubbed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryMatch_OpenChrome_IsOpenApp_NotListWindows()
    {
        Assert.True(DesktopToolIntent.TryMatch(
            "open a Google Chrome window on my desktop", out var match));
        Assert.Equal(DesktopToolIntent.Kind.OpenApp, match.Intent);
        Assert.Equal("desktop_open_app", match.ToolName);
        Assert.NotEqual("list_desktop_windows", match.ToolName);
    }

    [Theory]
    [InlineData("open chrome", "chrome")]
    [InlineData("open Google Chrome", "chrome")]
    [InlineData("launch the browser", "chrome")]
    [InlineData("start edge", "msedge")]
    [InlineData("open notepad", "notepad")]
    public void TryInferAliasFromUserText_MapsCommonPhrases(string text, string expected)
    {
        Assert.True(DesktopAppLauncher.TryInferAliasFromUserText(text, out var alias));
        Assert.Equal(expected, alias);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("what's the status of that task?")]
    [InlineData("create a workflow to recall then speak")]
    public void TryMatch_Unrelated_ReturnsFalse(string text)
    {
        Assert.False(DesktopToolIntent.TryMatch(text, out _));
    }

    [Theory]
    [InlineData("open the browser and take a screenshot")]
    [InlineData("open Chrome and take a screenshot")]
    [InlineData("launch chrome then screenshot please")]
    public void TryMatch_OpenPlusScreenshot_StillForcesOpenApp(string text)
    {
        // ForceTool=open_app; IsPureOpenPrompt=false so the tool-loop continues
        // for the screenshot follow-on (BED-180). Under VM scope the backend
        // injects into the guest instead of Process.Start on Windows.
        Assert.True(DesktopToolIntent.TryMatch(text, out var match));
        Assert.Equal("desktop_open_app", match.ToolName);
        Assert.False(DesktopToolIntent.IsPureOpenPrompt(text));
    }

    [Theory]
    [InlineData("take a screenshot", "desktop_screenshot")]
    [InlineData("screenshot please", "desktop_screenshot")]
    public void TryMatch_BareScreenshot_ForcesScreenshot(string text, string expectedTool)
    {
        Assert.True(DesktopToolIntent.TryMatch(text, out var match));
        Assert.Equal(expectedTool, match.ToolName);
    }

    [Fact]
    public void ComputerUseGuidance_Append_IsIdempotent()
    {
        var once = ComputerUseGuidance.AppendToPreamble("hello");
        Assert.Contains(ComputerUseGuidance.Marker, once, StringComparison.Ordinal);
        var twice = ComputerUseGuidance.AppendToPreamble(once);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void ComputerUseGuidance_Block_ForbidsInventedHermesTools()
    {
        Assert.Contains("desktop_open_app", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("Do NOT invent Hermes", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("or terminal", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("browser_snapshot", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("browser_click_text", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("computer_use", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("ONLY asked to open/launch", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("NEVER tell Kayleigh you can only text/chat", ComputerUseGuidance.Block, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerUseGuidance_VmBlock_ForbidsChatOnlyClaim()
    {
        Assert.Contains("NEVER tell Kayleigh you can only text/chat", ComputerUseGuidance.VmBlock, StringComparison.Ordinal);
        Assert.Contains("Her screen", ComputerUseGuidance.VmBlock, StringComparison.Ordinal);
        Assert.Contains("victoria-sandbox", ComputerUseGuidance.VmBlock, StringComparison.Ordinal);
        Assert.Contains("When Kayleigh gives click", ComputerUseGuidance.VmBlock, StringComparison.Ordinal);
        Assert.Contains("browser_click_text-only limitation", ComputerUseGuidance.VmBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputerUseGuidance_ScopedBlock_LocksVmTitle_PlaywrightMode()
    {
        var once = ComputerUseGuidance.AppendToPreamble("hello", "victoria-sandbox", "playwright");
        Assert.Contains(ComputerUseGuidance.Marker, once, StringComparison.Ordinal);
        Assert.Contains(ComputerUseGuidance.Block, once, StringComparison.Ordinal);
        Assert.Contains(ComputerUseGuidance.ScopedBlock("victoria-sandbox", "playwright"), once, StringComparison.Ordinal);
        Assert.Contains("Preferred workflow", once, StringComparison.Ordinal);
        Assert.Contains("DESKTOP SCOPE", once, StringComparison.Ordinal);
        Assert.Contains("victoria-sandbox", once, StringComparison.Ordinal);
        Assert.Contains("browser_navigate", once, StringComparison.Ordinal);
        Assert.Contains("browser_click_text", once, StringComparison.Ordinal);
        Assert.Contains("WEB IS NOT THE VM", once, StringComparison.Ordinal);
        Assert.Contains("hard error", once, StringComparison.Ordinal);
        Assert.Contains("Do NOT mention VirtualBox", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("Do not tell Kayleigh to start VirtualBox", once, StringComparison.Ordinal);
        Assert.True(
            once.IndexOf(ComputerUseGuidance.Block, StringComparison.Ordinal)
            < once.IndexOf("DESKTOP SCOPE", StringComparison.Ordinal));
        Assert.Equal(once, ComputerUseGuidance.AppendToPreamble(once, "victoria-sandbox", "playwright"));
    }

    [Fact]
    public void ComputerUseGuidance_ScopedBlock_VmPrimary_WhenNotPlaywright()
    {
        var once = ComputerUseGuidance.AppendToPreamble("hello", "victoria-sandbox", "native");
        Assert.Contains(ComputerUseGuidance.VmBlock, once, StringComparison.Ordinal);
        Assert.Contains("VM PRIMARY", once, StringComparison.Ordinal);
        Assert.Contains("victoria-sandbox", once, StringComparison.Ordinal);
        Assert.Contains("desktop_open_app firefox", once, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WEB IS NOT THE VM", once, StringComparison.Ordinal);
        Assert.Contains(ComputerUseGuidance.BlockFor("native"), once, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("use the vm")]
    [InlineData("look inside the sandbox")]
    [InlineData("drive victoria-sandbox")]
    public void TryMatch_VmPhrases_ForcesDesktopTool(string text)
    {
        Assert.True(DesktopToolIntent.TryMatch(text, out var match));
        Assert.True(
            match.ToolName is "list_desktop_windows" or "desktop_screenshot",
            match.ToolName);
    }

    // ---------------------------------------------------------------------
    // BED-180: resolve open-app args + pure-open early-exit classification
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("open Google Chrome", "chrome", null)]
    [InlineData("open my browser", "chrome", null)]
    [InlineData("bring up chrome", "chrome", null)]
    [InlineData("launch edge", "edge", null)]
    [InlineData("start notepad", "notepad", null)]
    [InlineData("open chrome to https://example.com", "chrome", "https://example.com")]
    [InlineData("open browser at www.google.com", "chrome", "www.google.com")]
    public void TryResolveOpenAppLaunch_ExtractsAliasAndOptionalUrl(
        string text, string expectedApp, string? expectedArgs)
    {
        Assert.True(DesktopToolIntent.TryResolveOpenAppLaunch(text, out var app, out var args));
        Assert.Equal(expectedApp, app);
        Assert.Equal(expectedArgs, args);
    }

    [Theory]
    [InlineData("open Google Chrome", true)]
    [InlineData("open my browser", true)]
    [InlineData("open chrome to https://example.com", true)]
    [InlineData("open chrome and click the first link", false)]
    [InlineData("open chrome then type hello", false)]
    [InlineData("open chrome and search for cats", false)]
    [InlineData("open my browser and check my email", false)]
    [InlineData("open chrome and go to gmail", false)]
    public void IsPureOpenPrompt_ClassifiesFollowOnActions(string text, bool expected)
    {
        Assert.Equal(expected, DesktopToolIntent.IsPureOpenPrompt(text));
    }

    [Fact]
    public void BuildOpenedReply_FormatsConfirm()
    {
        Assert.Equal("Opened Chrome.", DesktopToolIntent.BuildOpenedReply("chrome", null));
        Assert.Equal(
            "Opened Chrome to https://example.com.",
            DesktopToolIntent.BuildOpenedReply("chrome", "https://example.com"));
    }

    [Fact]
    public void BuildOpenedReply_GuestControl_SaysFirefoxInVm()
    {
        Assert.Equal(
            "Opened Firefox in the Ubuntu VM.",
            DesktopToolIntent.BuildOpenedReply(
                "chrome",
                null,
                "Opened firefox in the Ubuntu VM via guestcontrol (host VirtualBox window can stay minimized)."));
        Assert.Equal(
            "Opened Firefox in the Ubuntu VM to https://example.com.",
            DesktopToolIntent.BuildOpenedReply(
                "chrome",
                "https://example.com",
                "Opened firefox in the Ubuntu VM via guestcontrol"));
    }

    [Theory]
    [InlineData("open Google Chrome", "browser_navigate")]
    [InlineData("open the browser", "browser_navigate")]
    [InlineData("launch chrome", "browser_navigate")]
    [InlineData("open chrome to https://example.com", "browser_navigate")]
    [InlineData("open edge", "browser_navigate")]
    [InlineData("start notepad", "desktop_open_app")]
    [InlineData("open firefox in the vm", "browser_navigate")]
    [InlineData("open chrome in virtualbox", "browser_navigate")]
    [InlineData("open your playwright and capture a frame", "browser_navigate")]
    [InlineData("open playwright to https://example.com", "browser_navigate")]
    [InlineData("capture a frame of the playwright browser", "browser_navigate")]
    public void TryMatch_PlaywrightBackend_RoutesBrowserToNavigate(string text, string expectedTool)
    {
        Assert.True(DesktopToolIntent.TryMatch(text, "playwright", out var match));
        Assert.Equal(expectedTool, match.ToolName);
    }

    [Fact]
    public void TryMatch_PlaywrightBackend_OpenChrome_IsBrowserNavigate_NotOpenApp()
    {
        Assert.True(DesktopToolIntent.TryMatch("open Google Chrome", "playwright", out var match));
        Assert.Equal(DesktopToolIntent.Kind.BrowserNavigate, match.Intent);
        Assert.Equal("browser_navigate", match.ToolName);
    }

    [Theory]
    [InlineData("open Google Chrome", "desktop_open_app")]
    [InlineData("open the browser", "desktop_open_app")]
    [InlineData("open chrome to https://example.com", "desktop_open_app")]
    [InlineData("open firefox in the vm", "desktop_open_app")]
    [InlineData("start notepad", "desktop_open_app")]
    public void TryMatch_NativeBackend_RoutesBrowserToGuestOpenApp(string text, string expectedTool)
    {
        Assert.True(DesktopToolIntent.TryMatch(text, "native", out var match));
        Assert.Equal(expectedTool, match.ToolName);
    }

    // ---------------------------------------------------------------------
    // PROP-13: URL extract + capture-only pure open + missing-URL ask
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("open https://example.com", "https://example.com")]
    [InlineData("visit https://example.com/path", "https://example.com/path")]
    [InlineData("go to https://example.com", "https://example.com")]
    [InlineData("https://example.com alone", "https://example.com")]
    [InlineData("open www.example.com", "https://www.example.com")]
    public void TryExtractNavigateUrl_DoesNotSwallowVerb(string text, string expected)
    {
        Assert.True(DesktopToolIntent.TryExtractNavigateUrl(text, out var url));
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData("open your playwright and capture a frame", true)]
    [InlineData("open playwright to https://example.com and capture a frame", true)]
    [InlineData("open chrome and take a screenshot", true)]
    [InlineData("open chrome and click the login button", false)]
    [InlineData("open chrome then type hello", false)]
    public void IsCaptureOnlyBrowserAsk_ClassifiesFollowOns(string text, bool expected)
    {
        Assert.Equal(expected, DesktopToolIntent.IsCaptureOnlyBrowserAsk(text));
    }

    [Theory]
    [InlineData("open playwright to https://example.com and capture a frame", true)]
    [InlineData("open your playwright and capture a frame of https://example.com", true)]
    [InlineData("open chrome and click Login", false)]
    public void IsPureOpenPrompt_Playwright_CaptureOnlyIsPure(string text, bool expected)
    {
        Assert.Equal(expected, DesktopToolIntent.IsPureOpenPrompt(text, "playwright"));
    }

    [Fact]
    public void MissingNavigateUrlReply_IsPlainLanguage()
    {
        Assert.Contains("http", DesktopToolIntent.MissingNavigateUrlReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("execute_tool", DesktopToolIntent.MissingNavigateUrlReply, StringComparison.OrdinalIgnoreCase);
    }

}
