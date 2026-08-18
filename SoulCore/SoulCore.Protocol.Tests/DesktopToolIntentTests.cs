using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

public class DesktopToolIntentTests
{
    [Theory]
    [InlineData("look at my screen", "desktop_screenshot")]
    [InlineData("what's on my desktop?", "desktop_screenshot")]
    [InlineData("use the computer and draw a line", "desktop_screenshot")]
    [InlineData("click on the Chrome window", "desktop_screenshot")]
    [InlineData("click the login button", "desktop_screenshot")]
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
    }

    [Fact]
    public void ComputerUseGuidance_ScopedBlock_LocksVmTitle()
    {
        var once = ComputerUseGuidance.AppendToPreamble("hello", "victoria-sandbox");
        Assert.Contains(ComputerUseGuidance.Marker, once, StringComparison.Ordinal);
        Assert.Contains(ComputerUseGuidance.Block, once, StringComparison.Ordinal);
        Assert.Contains(ComputerUseGuidance.ScopedBlock("victoria-sandbox"), once, StringComparison.Ordinal);
        Assert.Contains("Preferred workflow", once, StringComparison.Ordinal);
        Assert.Contains("DESKTOP SCOPE", once, StringComparison.Ordinal);
        Assert.Contains("victoria-sandbox", once, StringComparison.Ordinal);
        Assert.Contains("browser_navigate", once, StringComparison.Ordinal);
        Assert.Contains("browser_click_text", once, StringComparison.Ordinal);
        Assert.Contains("never Process.Start", once, StringComparison.Ordinal);
        // Full playbook stays; scoped text is appended after it.
        Assert.True(
            once.IndexOf(ComputerUseGuidance.Block, StringComparison.Ordinal)
            < once.IndexOf("DESKTOP SCOPE", StringComparison.Ordinal));
        Assert.Equal(once, ComputerUseGuidance.AppendToPreamble(once, "victoria-sandbox"));
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
}
