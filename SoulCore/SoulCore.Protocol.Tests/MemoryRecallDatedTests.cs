using SoulCore.Host.Ws;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

/// <summary>PROP-20.2 / PROP-18.1: dated episodic recall + today-first tape.</summary>
public class MemoryRecallDatedTests
{
    [Fact]
    public async Task RecallRecentDatedAsync_ReturnsOccurredAtWithContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-dated-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var id = await store.WriteEpisodicAsync("dated episode about sunrise", "chat");
            Assert.True(id > 0);

            var hits = await store.RecallRecentDatedAsync(limit: 5);
            Assert.NotEmpty(hits);
            var hit = Assert.Single(hits.Where(h => h.Content.Contains("sunrise", StringComparison.Ordinal)));
            Assert.False(string.IsNullOrWhiteSpace(hit.OccurredAt));
            Assert.Contains("T", hit.OccurredAt, StringComparison.Ordinal);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task RecallSimilarDatedAsync_ReturnsOccurredAtWithContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-dated-sim-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var id = await store.WriteEpisodicAsync("semantic cats episode", "chat");
            await store.StoreEmbeddingAsync(id, [1f, 0f, 0f], "test-model");

            var hits = await store.RecallSimilarDatedAsync([1f, 0f, 0f], limit: 1);
            var hit = Assert.Single(hits);
            Assert.Contains("cats", hit.Content, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(hit.OccurredAt));
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void MemoryTape_OrderTodayFirst_TwoDatedEpisodes()
    {
        var now = new DateTimeOffset(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);
        var ordered = MemoryTape.OrderTodayFirst(
            new[]
            {
                new MemoryRecallLine("older semantic hit", "2026-10-01T12:00:00.000Z"),
                new MemoryRecallLine("today line", "2026-11-17T09:00:00.000Z"),
            },
            now);

        Assert.Equal(2, ordered.Count);
        Assert.Contains("today line", ordered[0].Content, StringComparison.Ordinal);
        Assert.Contains("older semantic hit", ordered[1].Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// PROP-18.1: two store-dated episodes keep visible stamps so the model can
    /// order by day without inventing a clock.
    /// </summary>
    [Fact]
    public void MemoryTape_FormatLine_ExposesOccurredAtForChronology()
    {
        var earlier = new MemoryRecallLine("tea with Kayleigh", "2026-11-10T09:00:00.000Z");
        var later = new MemoryRecallLine("coffee plan", "2026-11-19T08:00:00.000Z");
        var earlierLine = MemoryTape.FormatLine(earlier);
        var laterLine = MemoryTape.FormatLine(later);

        Assert.Equal("2026-11-10T09:00:00.000Z | tea with Kayleigh", earlierLine);
        Assert.Equal("2026-11-19T08:00:00.000Z | coffee plan", laterLine);

        var (block, dropped) = MemoryTape.BuildMemoryBlock(
            new[] { earlier, later },
            budget: 4000,
            utcNow: new DateTimeOffset(2026, 11, 19, 18, 0, 0, TimeSpan.Zero));

        Assert.Equal(0, dropped);
        Assert.Contains(laterLine, block, StringComparison.Ordinal);
        Assert.Contains(earlierLine, block, StringComparison.Ordinal);
        Assert.True(
            block.IndexOf(laterLine, StringComparison.Ordinal)
            < block.IndexOf(earlierLine, StringComparison.Ordinal));
    }

    /// <summary>PROP-18.12: optional linePrefix keeps one assembly path for SMS bullets.</summary>
    [Fact]
    public void MemoryTape_BuildMemoryBlock_LinePrefixAndMaxLines()
    {
        var now = new DateTimeOffset(2026, 11, 19, 18, 0, 0, TimeSpan.Zero);
        var (block, dropped) = MemoryTape.BuildMemoryBlock(
            new[]
            {
                new MemoryRecallLine("a", "2026-11-19T10:00:00.000Z"),
                new MemoryRecallLine("b", "2026-11-19T09:00:00.000Z"),
                new MemoryRecallLine("c", "2026-11-10T09:00:00.000Z"),
            },
            budget: 4000,
            utcNow: now,
            linePrefix: "- ",
            maxLines: 2);

        Assert.Equal(1, dropped);
        Assert.Contains("- 2026-11-19T10:00:00.000Z | a", block, StringComparison.Ordinal);
        Assert.Contains("- 2026-11-19T09:00:00.000Z | b", block, StringComparison.Ordinal);
        Assert.DoesNotContain("| c", block, StringComparison.Ordinal);
    }
}
