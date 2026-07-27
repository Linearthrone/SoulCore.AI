using SoulCore.Core;

namespace SoulCore.Protocol.Tests;

public class EpisodicMemoryPromptTests
{
    [Fact]
    public void BuildUserPayload_RespectsTotalInputCharBudget()
    {
        var user = new string('u', 600);
        var reply = new string('r', 600);

        var payload = EpisodicMemoryPrompt.BuildUserPayload(user, reply);

        Assert.True(payload.Length <= EpisodicMemoryPrompt.TotalInputCharBudget);
        Assert.StartsWith("User said:\n", payload);
        Assert.Contains("\n\nI replied:\n", payload);
    }

    [Fact]
    public void BuildUserPayload_GivesLeftoverBudgetToLongReplyWhenUserIsShort()
    {
        var user = "hi";
        var reply = new string('r', 900);

        var payload = EpisodicMemoryPrompt.BuildUserPayload(user, reply);
        var replySection = payload.Split("\n\nI replied:\n", 2)[1];

        Assert.True(payload.Length <= EpisodicMemoryPrompt.TotalInputCharBudget);
        Assert.True(replySection.Length > EpisodicMemoryPrompt.TotalInputCharBudget / 2);
        Assert.DoesNotContain("I heard the user say:", payload);
    }

    [Fact]
    public void Truncate_ReturnsEmptyWhenMaxNonPositive()
    {
        Assert.Equal(string.Empty, EpisodicMemoryPrompt.Truncate("abc", 0));
        Assert.Equal(string.Empty, EpisodicMemoryPrompt.Truncate("abc", -1));
    }

    [Fact]
    public void Truncate_LeavesShortTextUnchanged()
    {
        Assert.Equal("short", EpisodicMemoryPrompt.Truncate("short", 10));
        Assert.Equal("abcd", EpisodicMemoryPrompt.Truncate("abcdef", 4));
    }

    [Fact]
    public void BuildTemplateFallback_MatchesLegacyBriefingShape()
    {
        var fallback = EpisodicMemoryPrompt.BuildTemplateFallback(
            "  hello there  ",
            "  warm reply  ");

        Assert.Equal(
            "I heard the user say: hello there\nI replied: warm reply",
            fallback);
    }

    [Fact]
    public void SystemInstruction_IsFirstPersonPastTenseGuidance()
    {
        Assert.Contains("first-person", EpisodicMemoryPrompt.SystemInstruction);
        Assert.Contains("Past tense", EpisodicMemoryPrompt.SystemInstruction);
        Assert.Contains("Victoria", EpisodicMemoryPrompt.SystemInstruction);
        Assert.Equal(96, EpisodicMemoryPrompt.AuthorMaxTokens);
    }

    [Fact]
    public void SampleAuthoredMemory_Fixture_IsPlainFirstPersonNotTemplate()
    {
        // Dry-run / QA fixture: shape expected from a successful memory-author call.
        const string sample =
            "Kurt asked about the garden plans. I told him the roses still needed pruning this weekend.";

        Assert.DoesNotContain("I heard the user say:", sample);
        Assert.DoesNotContain("[Reflection]", sample);
        Assert.True(sample.Split('.', StringSplitOptions.RemoveEmptyEntries).Length <= 3);
    }
}
