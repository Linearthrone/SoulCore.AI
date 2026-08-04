using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

public class SqliteMemoryStoreJournalTests
{
    [Fact]
    public async Task MigrationSeedsThreeJournalBooks()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-journal-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var books = await store.ListBooksAsync();

            Assert.Equal(3, books.Count);
            Assert.Equal(["feeling", "animation", "environment"], books.Select(b => b.Id).ToArray());
            Assert.Contains(books, b => b.Title.Contains("Feeling", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(books, b => b.Title.Contains("Animation", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(books, b => b.Title.Contains("Environment", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task WriteAndListEntries_ByBook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-journal-e-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);

            var feelId = await store.WriteEntryAsync(
                "feeling",
                "Right now I feel curious and a little excited.",
                moodJson: "{\"valence\":0.4,\"arousal\":0.55}",
                tagsJson: "[\"seed\"]");
            var animId = await store.WriteEntryAsync(
                "animation",
                "I want a clear walk cycle and a soft smile when Kurt enters.",
                tagsJson: "[\"seed\",\"locomotion\"]");
            var envId = await store.WriteEntryAsync(
                "environment",
                "I want to learn every room of Home and later notice the car and other buildings.",
                tagsJson: "[\"seed\",\"home\"]");

            Assert.True(feelId > 0);
            Assert.True(animId > 0);
            Assert.True(envId > 0);

            var feeling = await store.ListEntriesAsync("feeling", limit: 5);
            Assert.Single(feeling);
            Assert.Contains("curious", feeling[0].Body, StringComparison.OrdinalIgnoreCase);

            var all = await store.ListEntriesAsync(limit: 10);
            Assert.Equal(3, all.Count);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task WriteEntry_UnknownBook_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-journal-bad-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                store.WriteEntryAsync("dreams", "should fail"));
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }
}
