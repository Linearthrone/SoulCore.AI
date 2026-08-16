using SoulCore.Core;
using SoulCore.Host.Companion;

namespace SoulCore.Protocol.Tests;

public class CompanionOutboundMessengerTests
{
    [Fact]
    public void ExtractPhrase_strips_want_prefix_and_emotion_suffix()
    {
        var want = "want[engage]: reach out and share a thought (emotion=excited v=0.80 a=0.75 d=0.60 f=0.50); recent=(none)";
        var phrase = CompanionOutboundMessenger.ExtractPhrase(want);
        Assert.Equal("reach out and share a thought", phrase);
    }

    [Fact]
    public void ComposeProactiveText_without_beat_is_empty()
    {
        var text = CompanionOutboundMessenger.ComposeProactiveText(
            "engage",
            "excited",
            "want[engage]: lean in with bright, curious presence (holding 1 recent beat)");
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void ComposeProactiveText_engage_grounds_in_beat()
    {
        var text = CompanionOutboundMessenger.ComposeProactiveText(
            "engage",
            "excited",
            "want[engage]: lean in with bright, curious presence (holding 1 recent beat)",
            new[] { "User: open the browser and take a screenshot of Maestro" });
        Assert.Contains("open the browser", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("want[", text);
        Assert.DoesNotContain("lean in with bright", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(CompanionOutboundMessenger.ContainsScaffoldLeak(text));
    }

    [Fact]
    public void ComposeProactiveText_recall_never_leaks_scaffold_want()
    {
        var fields = new EmotionInfluencePrompt.EmotionFields(0.0, 0.2, 0.4, 0.4);
        var recent = new[] { "remember earlier soak conversation", "desk note", "quiet morning" };
        var want = SoulLoopWantProposal.Propose("calm", fields, recent);
        Assert.Contains("holding 3 recent beats", want);
        Assert.Contains("weave it into presence", want);

        var text = CompanionOutboundMessenger.ComposeProactiveText(
            SoulLoopWantProposal.CategoryRecall,
            "calm",
            want,
            recent);

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("soak conversation", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("holding 3 recent beats", text);
        Assert.DoesNotContain("weave it into presence", text);
        Assert.DoesNotContain("want[", text);
        Assert.False(CompanionOutboundMessenger.ContainsScaffoldLeak(text));
    }

    [Fact]
    public void ComposeProactiveText_skips_reflection_and_proactive_rows()
    {
        var text = CompanionOutboundMessenger.ComposeProactiveText(
            "notice",
            "calm",
            "want[notice]: x",
            new[]
            {
                "[Reflection] I am feeling calm. want[notice]: stay present",
                "[Proactive] Victoria → Kurt: Soft moment over here.",
                "User: the eye capture looked good indoors"
            });
        Assert.Contains("eye capture", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[Reflection]", text);
        Assert.DoesNotContain("[Proactive]", text);
    }

    [Fact]
    public void ComposeProactiveText_settle_and_reflect_stay_silent()
    {
        var beats = new[] { "User: long chat about the shadow GPU plan" };
        Assert.Equal(string.Empty,
            CompanionOutboundMessenger.ComposeProactiveText("settle", "tense", "want[settle]: x", beats));
        Assert.Equal(string.Empty,
            CompanionOutboundMessenger.ComposeProactiveText("reflect", "calm", "want[reflect]: x", beats));
    }

    [Fact]
    public void ComposeProactiveText_unknown_category_skips_push()
    {
        var text = CompanionOutboundMessenger.ComposeProactiveText(
            "not-a-real-category",
            "calm",
            "want[mystery]: something",
            new[] { "User: hello there friend" });
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void ContainsScaffoldLeak_detects_recall_idioms()
    {
        Assert.True(CompanionOutboundMessenger.ContainsScaffoldLeak(
            "This came back to me: recall the recent thread and weave it into presence (holding 3 recent beats)"));
        Assert.True(CompanionOutboundMessenger.ContainsScaffoldLeak("want[recall]: hello"));
        Assert.False(CompanionOutboundMessenger.ContainsScaffoldLeak(
            "Still with me: remember earlier soak conversation. Want to pick that up?"));
    }
}
