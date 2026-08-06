using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

public class DesktopToolIntentTests
{
    [Theory]
    [InlineData("look at my screen", "desktop_screenshot")]
    [InlineData("what's on my desktop?", "desktop_screenshot")]
    [InlineData("use the computer and draw a line", "list_desktop_windows")]
    [InlineData("click on the Chrome window", "list_desktop_windows")]
    [InlineData("what windows are open?", "list_desktop_windows")]
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
    [InlineData("hello")]
    [InlineData("what's the status of that task?")]
    [InlineData("create a workflow to recall then speak")]
    public void TryMatch_Unrelated_ReturnsFalse(string text)
    {
        Assert.False(DesktopToolIntent.TryMatch(text, out _));
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
    public void ComputerUseGuidance_Block_ForbidsHermesInventForLocalLaunch()
    {
        Assert.Contains("desktop_open_app", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("Do NOT invent Hermes", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("browser_navigate", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("computer_use", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("terminal", ComputerUseGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("ONLY asked to open/launch", ComputerUseGuidance.Block, StringComparison.Ordinal);
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
}
