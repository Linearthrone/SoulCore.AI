using SoulCore.Core;

namespace SoulCore.Protocol.Tests;

public class WakeContinuityTests
{
    [Fact]
    public void BuildLeftOffBlock_EmptyTail_IsEmpty()
    {
        Assert.Equal(string.Empty, WakeContinuity.BuildLeftOffBlock(Array.Empty<WakeContinuity.ExchangeLine>()));
    }

    [Fact]
    public void BuildLeftOffBlock_QuotesTheLatestExchange_NotAnOlderLesson()
    {
        var lines = new[]
        {
            new WakeContinuity.ExchangeLine("user", "open BE101 week 7", "2026-10-13T05:00:00Z"),
            new WakeContinuity.ExchangeLine("assistant", "BE101 week 7 is open.", "2026-10-13T05:01:00Z"),
            new WakeContinuity.ExchangeLine("user", "did you finish the video yet?", "2026-10-25T15:08:41Z"),
            new WakeContinuity.ExchangeLine("assistant", "I am watching the Tree Stories video.", "2026-10-25T15:10:11Z")
        };

        var block = WakeContinuity.BuildLeftOffBlock(lines);

        Assert.StartsWith("[Where we left off]", block, StringComparison.Ordinal);
        Assert.Contains("do not start as a beginner", block, StringComparison.Ordinal);
        Assert.Contains("Last exchange: 2026-10-25T15:10:11Z", block, StringComparison.Ordinal);
        Assert.Contains("Kayleigh: did you finish the video yet?", block, StringComparison.Ordinal);
        Assert.Contains("Victoria: I am watching the Tree Stories video.", block, StringComparison.Ordinal);
        Assert.DoesNotContain("BE101", block, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildLeftOffBlock_TruncatesALongReply()
    {
        var lines = new[]
        {
            new WakeContinuity.ExchangeLine("user", "continue", "2026-10-25T15:00:00Z"),
            new WakeContinuity.ExchangeLine("assistant", new string('v', WakeContinuity.LineCharLimit + 40), "2026-10-25T15:01:00Z")
        };

        var block = WakeContinuity.BuildLeftOffBlock(lines);
        var victoria = block.Split("Victoria: ", 2)[1].TrimEnd();

        Assert.Equal(WakeContinuity.LineCharLimit, victoria.Length);
    }
}
