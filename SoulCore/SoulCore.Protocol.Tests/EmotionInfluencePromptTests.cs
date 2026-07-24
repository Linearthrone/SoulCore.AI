using SoulCore.Core;

namespace SoulCore.Protocol.Tests;

public class EmotionInfluencePromptTests
{
    [Fact]
    public void BuildPreamble_IsDeterministic_AndDiffersByEmotion()
    {
        var excited = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["valence"] = 0.85,
            ["arousal"] = 0.80,
            ["dominance"] = 0.60,
            ["focus"] = 0.70
        };
        var low = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["valence"] = -0.70,
            ["arousal"] = 0.15,
            ["dominance"] = 0.20,
            ["focus"] = 0.10
        };

        var a1 = EmotionInfluencePrompt.BuildPreamble(excited);
        var a2 = EmotionInfluencePrompt.BuildPreamble(excited);
        var b = EmotionInfluencePrompt.BuildPreamble(low);

        Assert.Equal(a1, a2);
        Assert.Contains("label=excited", a1);
        Assert.Contains("focus=0.70", a1);
        Assert.Contains("energetic", a1);
        Assert.Contains("label=low", b);
        Assert.Contains("withdrawn", b);
        Assert.NotEqual(a1, b);
    }
}
