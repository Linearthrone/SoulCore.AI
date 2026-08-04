using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

public class DesktopToolIntentTests
{
    [Theory]
    [InlineData("look at my screen", "desktop_screenshot")]
    [InlineData("what's on my desktop?", "desktop_screenshot")]
    [InlineData("use the computer and open notepad", "list_desktop_windows")]
    [InlineData("click on the Chrome window", "list_desktop_windows")]
    [InlineData("what windows are open?", "list_desktop_windows")]
    [InlineData("call list_desktop_windows", "list_desktop_windows")]
    public void TryMatch_HighConfidence_ForcesTool(string text, string expectedTool)
    {
        Assert.True(DesktopToolIntent.TryMatch(text, out var match));
        Assert.Equal(expectedTool, match.ToolName);
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
}
