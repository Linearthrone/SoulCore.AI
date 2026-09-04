using SoulCore.Protocol;
using Xunit;

namespace SoulCore.Protocol.Tests;

public sealed class QuotedChatTextTests
{
    [Fact]
    public void BuildUserText_WithoutQuote_ReturnsOriginal()
    {
        Assert.Equal("hello", QuotedChatText.BuildUserText("hello", null));
        Assert.Equal("hello", QuotedChatText.BuildUserText("hello", "  "));
    }

    [Fact]
    public void BuildUserText_WithQuote_PrefixesExcerpt()
    {
        var result = QuotedChatText.BuildUserText(
            "got it — use the second option",
            "here are three options:\n1) A\n2) B");

        Assert.Contains("Kurt is replying to this excerpt", result);
        Assert.Contains("> here are three options:", result);
        Assert.Contains("> 1) A", result);
        Assert.EndsWith("got it — use the second option", result);
    }

    [Fact]
    public void NormalizeQuoted_TruncatesLongExcerpts()
    {
        var longText = new string('a', QuotedChatText.MaxQuotedChars + 50);
        var normalized = QuotedChatText.NormalizeQuoted(longText);
        Assert.NotNull(normalized);
        Assert.EndsWith("…", normalized);
        Assert.Equal(QuotedChatText.MaxQuotedChars + 1, normalized!.Length);
    }
}
