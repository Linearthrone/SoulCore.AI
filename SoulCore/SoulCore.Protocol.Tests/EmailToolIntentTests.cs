using SoulCore.Inference.Tools.Email;

namespace SoulCore.Protocol.Tests;

public class EmailToolIntentTests
{
    [Theory]
    [InlineData("check my email", "email_inbox")]
    [InlineData("check your inbox", "email_inbox")]
    [InlineData("what's in my inbox?", "email_inbox")]
    [InlineData("sort my email", "email_inbox")]
    [InlineData("go through victoria's email", "email_inbox")]
    [InlineData("list email accounts", "email_accounts")]
    [InlineData("which mailboxes do you have?", "email_accounts")]
    [InlineData("search email for invoice", "email_search")]
    [InlineData("find that email about the lease", "email_search")]
    [InlineData("call email_inbox", "email_inbox")]
    public void TryMatch_HighConfidence_ForcesReadSafeTool(string text, string expectedTool)
    {
        Assert.True(EmailToolIntent.TryMatch(text, out var match));
        Assert.Equal(expectedTool, match.ToolName);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("what's my MT4 status?")]
    [InlineData("open chrome and go to gmail")]
    [InlineData("send that email now")]
    [InlineData("delete that email")]
    public void TryMatch_DoesNotForceWriteOrUnrelated(string text)
    {
        Assert.False(EmailToolIntent.TryMatch(text, out _));
    }

    [Theory]
    [InlineData("check your inbox", "victoria")]
    [InlineData("check my email", "personal")]
    [InlineData("search business email for invoice", "business")]
    [InlineData("go through victoria's email", "victoria")]
    public void InferAccount_MapsMailboxOwner(string text, string expected)
    {
        Assert.Equal(expected, EmailToolIntent.InferAccount(text));
    }

    [Fact]
    public void EmailGuidance_TellsHerNotToBrowseGmail()
    {
        Assert.Contains("do NOT open Gmail", EmailGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("confirmed=true", EmailGuidance.Block, StringComparison.Ordinal);
        Assert.Contains("victoria", EmailGuidance.Block, StringComparison.OrdinalIgnoreCase);
    }
}
