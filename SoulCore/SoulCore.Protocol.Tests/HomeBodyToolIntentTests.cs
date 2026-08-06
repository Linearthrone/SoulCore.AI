using SoulCore.Inference.Tools.Body;

namespace SoulCore.Protocol.Tests;

public class HomeBodyToolIntentTests
{
    [Theory]
    [InlineData("use your eyes and look around", "victoria_eye_capture")]
    [InlineData("look around the room", "victoria_eye_capture")]
    [InlineData("find my avatar on the ground", "victoria_eye_capture")]
    [InlineData("what do you see outside?", "victoria_eye_capture")]
    [InlineData("call victoria_eye_capture", "victoria_eye_capture")]
    [InlineData("go outside and explore", "loco")]
    [InlineData("walk forward toward the balcony", "loco")]
    [InlineData("find your way outside", "victoria_eye_capture")]
    public void TryMatch_HomeIntents_ForceTool(string text, string expectedTool)
    {
        Assert.True(HomeBodyToolIntent.TryMatch(text, out var match));
        Assert.Equal(expectedTool, match.ToolName);
    }

    [Theory]
    [InlineData("look at my screen")]
    [InlineData("what's on my desktop?")]
    [InlineData("hello")]
    public void TryMatch_ScreenOrUnrelated_ReturnsFalse(string text)
    {
        Assert.False(HomeBodyToolIntent.TryMatch(text, out _));
    }

    [Fact]
    public void HomeBodyGuidance_Append_IsIdempotent()
    {
        var once = HomeBodyGuidance.AppendToPreamble("hello");
        Assert.Contains(HomeBodyGuidance.Marker, once, StringComparison.Ordinal);
        var twice = HomeBodyGuidance.AppendToPreamble(once);
        Assert.Equal(once, twice);
        Assert.Contains("victoria_eye_capture", once, StringComparison.Ordinal);
        Assert.Contains("loco", once, StringComparison.Ordinal);
    }
}
