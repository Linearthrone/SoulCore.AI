using SoulCore.Inference.Tooling;

namespace SoulCore.Protocol.Tests;

public class ToolCallTextRecoveryTests
{
    private static readonly HashSet<string> Tools = new(StringComparer.Ordinal)
    {
        "list_desktop_windows",
        "browser_navigate",
        "desktop_screenshot",
    };

    [Fact]
    public void TryRecover_ExecuteToolTag_EmptyArgs()
    {
        Assert.True(ToolCallTextRecovery.TryRecover(
            "<execute_tool> list_desktop_windows{} </execute_tool>",
            Tools,
            out var calls));
        Assert.Single(calls);
        Assert.Equal("list_desktop_windows", calls[0].Name);
    }

    [Fact]
    public void TryRecover_ExecuteToolTag_WithJsonArgs()
    {
        Assert.True(ToolCallTextRecovery.TryRecover(
            """<execute_tool>browser_navigate{"url":"https://example.com"}</execute_tool>""",
            Tools,
            out var calls));
        Assert.Equal("browser_navigate", calls[0].Name);
        Assert.Equal("https://example.com", calls[0].Arguments!.Value.GetProperty("url").GetString());
    }

    [Fact]
    public void LooksLikeToolLeak_DetectsExecuteTool()
    {
        Assert.True(ToolCallTextRecovery.LooksLikeToolLeak(
            "<execute_tool> list_desktop_windows{} </execute_tool>",
            Tools));
    }
}
