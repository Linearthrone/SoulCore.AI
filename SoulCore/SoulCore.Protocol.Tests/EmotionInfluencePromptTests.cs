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
        Assert.Contains("Do not flatten your voice", a1);
        Assert.Contains("label=low", b);
        Assert.Contains("withdrawn", b);
        Assert.DoesNotContain("Respond with neutral, balanced wording", a1);
        Assert.NotEqual(a1, b);
    }

    [Fact]
    public void High_arousal_mid_valence_is_alert_not_flat()
    {
        Assert.Equal("alert", EmotionInfluencePrompt.DescribeLabel(0.04, 0.80));
        var preamble = EmotionInfluencePrompt.BuildPreamble(new Dictionary<string, double>
        {
            ["valence"] = 0.04,
            ["arousal"] = 0.80,
            ["dominance"] = 0.44,
            ["focus"] = 0.42
        });
        Assert.Contains("label=alert", preamble);
        Assert.Contains("keyed up and direct", preamble);
        Assert.DoesNotContain("Respond with neutral", preamble);
    }

    [Fact]
    public void Speech_moves_a_stale_reading()
    {
        var stale = new Dictionary<string, double>
        {
            ["valence"] = 0.04,
            ["arousal"] = 0.80,
            ["dominance"] = 0.44,
            ["focus"] = 0.42
        };
        var next = EmotionInfluencePrompt.ReflectSpeech(
            "The emotion channel is lying. It is reporting neutral and telling me to keep my wording flat while I am actually keyed up about the lesson and the file on disk.",
            stale);
        Assert.True(next["valence"] < 0);
        Assert.True(next["focus"] > 0.5);
    }

    [Fact]
    public void Mojibake_from_latin1_and_oem_repairs()
    {
        var latin = "\u00E2\u20AC\u201D";
        var oem = "\u0393\u00C7\u00AA";
        Assert.Equal("\u2014", Utf8TextRepair.Repair(latin));
        Assert.Equal("\u2026", Utf8TextRepair.Repair(oem));
        Assert.Equal("already \u2014 fine", Utf8TextRepair.Repair("already \u2014 fine"));
    }
}
