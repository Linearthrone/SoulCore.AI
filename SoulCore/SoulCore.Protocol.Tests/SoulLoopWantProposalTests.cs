using SoulCore.Core;

namespace SoulCore.Protocol.Tests;

public class SoulLoopWantProposalTests
{
    [Fact]
    public void Propose_IsDeterministic_AndVariesByEmotion()
    {
        var fieldsCalm = new EmotionInfluencePrompt.EmotionFields(0.0, 0.10, 0.3, 0.3);
        var fieldsExcited = new EmotionInfluencePrompt.EmotionFields(0.8, 0.75, 0.6, 0.5);
        var recent = new[] { "quiet morning at the desk" };

        var a1 = SoulLoopWantProposal.Propose("calm", fieldsCalm, recent);
        var a2 = SoulLoopWantProposal.Propose("calm", fieldsCalm, recent);
        var b = SoulLoopWantProposal.Propose("excited", fieldsExcited, recent);

        Assert.Equal(a1, a2);
        Assert.Contains("want[notice]:", a1);
        Assert.Contains("want[engage]:", b);
        Assert.NotEqual(a1, b);
    }

    [Theory]
    [InlineData("tense", -0.5, 0.7, SoulLoopWantProposal.CategorySettle)]
    [InlineData("low", -0.6, 0.15, SoulLoopWantProposal.CategoryReconnect)]
    [InlineData("content", 0.5, 0.3, SoulLoopWantProposal.CategorySavor)]
    [InlineData("excited", 0.8, 0.75, SoulLoopWantProposal.CategoryEngage)]
    public void Classify_MapsEmotionLabels(string label, double v, double a, string expected)
    {
        var fields = new EmotionInfluencePrompt.EmotionFields(v, a, 0.4, 0.4);
        var cat = SoulLoopWantProposal.Classify(label, fields, new[] { "desk note" });
        Assert.Equal(expected, cat);
    }

    [Fact]
    public void Classify_EpisodicClarify_OverridesEmotion()
    {
        var fields = new EmotionInfluencePrompt.EmotionFields(0.5, 0.3, 0.5, 0.4);
        var recent = new[] { "correction: that was misunderstood — you meant the other path" };
        var cat = SoulLoopWantProposal.Classify("content", fields, recent);
        Assert.Equal(SoulLoopWantProposal.CategoryClarify, cat);
        Assert.StartsWith("want[clarify]:", SoulLoopWantProposal.Propose("content", fields, recent));
    }

    [Fact]
    public void Classify_QuestionMarkAlone_DoesNotForceClarify()
    {
        // Ordinary chat questions used to lock every tick into clarify.
        var fields = new EmotionInfluencePrompt.EmotionFields(0.0, 0.2, 0.4, 0.4);
        var recent = new[] { "User: can you open Chrome?" };
        var cat = SoulLoopWantProposal.Classify("calm", fields, recent);
        Assert.NotEqual(SoulLoopWantProposal.CategoryClarify, cat);
    }

    [Fact]
    public void Classify_EpisodicRecall_OverridesEmotion()
    {
        var fields = new EmotionInfluencePrompt.EmotionFields(0.0, 0.2, 0.4, 0.4);
        var recent = new[] { "remember earlier soak conversation" };
        var cat = SoulLoopWantProposal.Classify("calm", fields, recent);
        Assert.Equal(SoulLoopWantProposal.CategoryRecall, cat);
    }

    [Fact]
    public void Classify_ExploreCueInRecent_ReturnsExplore()
    {
        var fields = new EmotionInfluencePrompt.EmotionFields(0.2, 0.3, 0.5, 0.3);
        var recent = new[] { "I want to explore every room of Home with open curiosity." };
        var cat = SoulLoopWantProposal.Classify("calm", fields, recent);
        Assert.Equal(SoulLoopWantProposal.CategoryExplore, cat);
        var want = SoulLoopWantProposal.Propose("calm", fields, recent);
        Assert.StartsWith("want[explore]:", want);
        Assert.Contains("Home", want, StringComparison.Ordinal);
    }

    [Fact]
    public void Propose_NeverRequestsExternalActs()
    {
        var fields = new EmotionInfluencePrompt.EmotionFields(0.9, 0.9, 0.9, 0.9);
        var want = SoulLoopWantProposal.Propose(
            "excited",
            fields,
            new[] { "desk note about the soak window" });

        // Phrase stays low-agency engage language (no tool directives).
        Assert.Contains("want[engage]:", want);
        Assert.Contains("engage", want, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("want: open", want, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("execute", want, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("launch", want, StringComparison.OrdinalIgnoreCase);
    }
}
