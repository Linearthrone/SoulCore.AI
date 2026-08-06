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
    public void ComposeProactiveText_engage_is_user_facing_sms()
    {
        var text = CompanionOutboundMessenger.ComposeProactiveText(
            "engage",
            "excited",
            "want[engage]: lean in with bright, curious presence (holding 1 recent beat) (emotion=excited v=0.8 a=0.7 d=0.5 f=0.5); recent=(none)");
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.DoesNotContain("want[", text);
        Assert.DoesNotContain("holding", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lean in with bright", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("emotion=", text);
        Assert.Contains("Hey", text);
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
            want);

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.DoesNotContain("holding 3 recent beats", text);
        Assert.DoesNotContain("weave it into presence", text);
        Assert.DoesNotContain("want[", text);
        Assert.DoesNotContain("This came back to me:", text);
        Assert.False(CompanionOutboundMessenger.ContainsScaffoldLeak(text));
    }

    [Theory]
    [InlineData(SoulLoopWantProposal.CategoryEngage)]
    [InlineData(SoulLoopWantProposal.CategoryReconnect)]
    [InlineData(SoulLoopWantProposal.CategoryClarify)]
    [InlineData(SoulLoopWantProposal.CategorySavor)]
    [InlineData(SoulLoopWantProposal.CategoryRecall)]
    [InlineData(SoulLoopWantProposal.CategoryExplore)]
    [InlineData(SoulLoopWantProposal.CategoryNotice)]
    [InlineData(SoulLoopWantProposal.CategorySettle)]
    [InlineData(SoulLoopWantProposal.CategoryReflect)]
    public void ComposeProactiveText_all_categories_are_natural_or_empty(string category)
    {
        var scaffoldWant =
            $"want[{category}]: recall the recent thread and weave it into presence (holding 3 recent beats) (emotion=calm v=0.00 a=0.20 d=0.40 f=0.40); recent=x";
        var text = CompanionOutboundMessenger.ComposeProactiveText(category, "calm", scaffoldWant);
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.False(CompanionOutboundMessenger.ContainsScaffoldLeak(text));
        Assert.DoesNotContain("holding", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weave it into presence", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("want[", text);
    }

    [Fact]
    public void ComposeProactiveText_unknown_category_skips_push()
    {
        var text = CompanionOutboundMessenger.ComposeProactiveText(
            "not-a-real-category",
            "calm",
            "want[mystery]: something (holding 3 recent beats)");
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void ContainsScaffoldLeak_detects_recall_idioms()
    {
        Assert.True(CompanionOutboundMessenger.ContainsScaffoldLeak(
            "This came back to me: recall the recent thread and weave it into presence (holding 3 recent beats)"));
        Assert.True(CompanionOutboundMessenger.ContainsScaffoldLeak("want[recall]: hello"));
        Assert.False(CompanionOutboundMessenger.ContainsScaffoldLeak(
            "Something from earlier came back to me. Miss talking it through with you."));
    }
}
