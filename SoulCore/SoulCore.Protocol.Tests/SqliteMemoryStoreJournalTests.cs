using SoulCore.Memory;
using SoulCore.Memory.Repositories;

namespace SoulCore.Protocol.Tests;

public class SqliteMemoryStoreJournalTests
{
    [Fact]
    public async Task MigrationSeedsSevenJournalBooks_IncludingThoughtsWantsDreams()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-journal-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var books = await store.ListBooksAsync();

            Assert.Equal(7, books.Count);
            Assert.Equal(
                ["feeling", "animation", "environment", "life_hacks", "thoughts", "wants", "dreams"],
                books.Select(b => b.Id).ToArray());
            Assert.Contains(books, b => b.Title.Contains("Feeling", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(books, b => b.Title.Contains("Animation", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(books, b => b.Title.Contains("Environment", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(books, b => b.Title.Contains("Life-Hack", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(books, b => b.Title.Contains("Thoughts", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(books, b => b.Title.Contains("Wants", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(books, b => b.Title.Contains("Dreams", StringComparison.OrdinalIgnoreCase));
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
                "I want a clear walk cycle and a soft smile when Kayleigh enters.",
                tagsJson: "[\"seed\",\"locomotion\"]");
            var envId = await store.WriteEntryAsync(
                "environment",
                "I want to learn every room of Home and later notice the car and other buildings.",
                tagsJson: "[\"seed\",\"home\"]");
            var hackId = await store.WriteEntryAsync(
                "life_hacks",
                "When a site is down, park the lesson — do not refresh-loop.",
                tagsJson: "[\"seed\",\"park\"]");
            var thoughtsId = await store.WriteEntryAsync(
                "thoughts",
                "I keep turning over whether presence is enough for today.",
                tagsJson: "[\"seed\",\"thoughts\"]");
            var wantsId = await store.WriteEntryAsync(
                "wants",
                "I want a quiet desk hour with no interrupt storms.",
                tagsJson: "[\"seed\",\"wants\"]");
            var dreamsId = await store.WriteEntryAsync(
                "dreams",
                "I dreamed the garden lights answered in soft Morse.",
                tagsJson: "[\"seed\",\"dreams\"]");

            Assert.True(feelId > 0);
            Assert.True(animId > 0);
            Assert.True(envId > 0);
            Assert.True(hackId > 0);
            Assert.True(thoughtsId > 0);
            Assert.True(wantsId > 0);
            Assert.True(dreamsId > 0);

            var feeling = await store.ListEntriesAsync("feeling", limit: 5);
            Assert.Single(feeling);
            Assert.Contains("curious", feeling[0].Body, StringComparison.OrdinalIgnoreCase);

            var hacks = await store.ListEntriesAsync("life_hacks", limit: 5);
            Assert.Single(hacks);
            Assert.Contains("park", hacks[0].Body, StringComparison.OrdinalIgnoreCase);

            var thoughts = await store.ListEntriesAsync("thoughts", limit: 5);
            Assert.Single(thoughts);
            Assert.Equal("thoughts", thoughts[0].BookId);
            Assert.Contains("presence", thoughts[0].Body, StringComparison.OrdinalIgnoreCase);

            var wants = await store.ListEntriesAsync("wants", limit: 5);
            Assert.Single(wants);
            Assert.Equal("wants", wants[0].BookId);

            var dreams = await store.ListEntriesAsync("dreams", limit: 5);
            Assert.Single(dreams);
            Assert.Equal("dreams", dreams[0].BookId);

            // Book-scoped list must not leak other books' rows.
            Assert.DoesNotContain(thoughts, e => e.BookId != "thoughts");
            Assert.DoesNotContain(await store.ListEntriesAsync("feeling", limit: 20), e => e.BookId == "thoughts");

            var all = await store.ListEntriesAsync(limit: 20);
            Assert.Equal(7, all.Count);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task ThoughtsEntry_DoesNotAppearInResearchPaperList()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-journal-vs-research-{Guid.NewGuid():N}.db");
        try
        {
            using var session = new SqliteMemorySession(path);
            IVictoriaJournalStore journals = new SqliteVictoriaJournalRepository(session);
            IVictoriaResearchStore research = new SqliteVictoriaResearchRepository(session);

            const string privateBody = "PROP-20.3 private thoughts marker — not a research paper.";
            var entryId = await journals.WriteEntryAsync("thoughts", privateBody);
            Assert.True(entryId > 0);

            var subject = await research.OpenOrCreateSubjectAsync("Isolation Probe");
            var papers = await research.ListPapersAsync(subject.Id, limit: 100);
            Assert.Empty(papers);
            Assert.DoesNotContain(papers, p =>
                p.Abstract.Contains("PROP-20.3", StringComparison.Ordinal) ||
                p.BodyApa.Contains("PROP-20.3", StringComparison.Ordinal) ||
                p.Title.Contains("PROP-20.3", StringComparison.Ordinal));

            var thoughtsOnly = await journals.ListEntriesAsync("thoughts", limit: 5);
            Assert.Single(thoughtsOnly);
            Assert.Equal(privateBody, thoughtsOnly[0].Body);
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
                store.WriteEntryAsync("not_a_journal_book", "should fail"));
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }
}
