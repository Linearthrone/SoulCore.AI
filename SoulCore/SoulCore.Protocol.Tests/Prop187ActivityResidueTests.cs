using Microsoft.Extensions.Logging.Abstractions;
using SoulCore.Inference.Presence;
using SoulCore.Memory;
using SoulCore.Memory.Repositories;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// PROP-18.7: non-generic activities Desc + topic journal depth law + dual-session distinctness.
/// </summary>
public class Prop187ActivityResidueTests
{
    [Fact]
    public void Composer_TwoSessionsSameSubject_DifferentFindings_DifferentDescAndJournal()
    {
        const string subject = "diffraction gratings";
        var findingsA =
            "The 600-line grating split the sodium D lines more cleanly than yesterday's cheap film; "
            + "I could finally see the orange doublet without squinting.";
        var findingsB =
            "Tonight the blaze angle mattered — tilting the mount by two degrees killed the ghost order "
            + "that had been smearing green into the blue channel.";

        var descA = ActivityResidueComposer.BuildSessionDesc(subject, findingsA);
        var descB = ActivityResidueComposer.BuildSessionDesc(subject, findingsB);
        Assert.NotEqual(descA, descB);
        Assert.False(ActivityResidueComposer.IsGenericDesc(descA, subject));
        Assert.False(ActivityResidueComposer.IsGenericDesc(descB, subject));
        Assert.Contains("sodium", descA, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blaze", descB, StringComparison.OrdinalIgnoreCase);

        var journalA = ActivityResidueComposer.BuildTopicJournalBody(subject, findingsA, "leisure");
        var journalB = ActivityResidueComposer.BuildTopicJournalBody(subject, findingsB, "leisure");
        Assert.NotEqual(journalA, journalB);
        Assert.True(ParagraphCount(journalA) >= ActivityResidueComposer.MinJournalParagraphs);
        Assert.True(ParagraphCount(journalB) >= ActivityResidueComposer.MinJournalParagraphs);
        Assert.Contains("sodium", journalA, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blaze", journalB, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Composer_RejectsGenericDescTemplates()
    {
        Assert.True(ActivityResidueComposer.IsGenericDesc("Read paper on optics", "optics"));
        Assert.True(ActivityResidueComposer.IsGenericDesc("Continued research on optics", "optics"));
        Assert.True(ActivityResidueComposer.IsGenericDesc("Leisure session", "optics"));
        Assert.False(ActivityResidueComposer.IsGenericDesc(
            "On optics: the sodium D lines finally resolved on the 600-line grating.",
            "optics"));
    }

    [Fact]
    public void Composer_JournalFloor_NeverOneLinerTheater()
    {
        var body = ActivityResidueComposer.BuildTopicJournalBody(
            "coastal fog",
            "The advection fog rolled in twenty minutes earlier than the buoy said it would.",
            "research");
        Assert.True(ParagraphCount(body) >= 2);
        Assert.Contains("I ", body, StringComparison.Ordinal);
        // No upper cap: long findings stay long.
        var longFindings = string.Join(
            " ",
            Enumerable.Range(0, 40).Select(i =>
                $"Observation {i}: the fog bank thickened near buoy {i} with a distinct cold smell."));
        var longBody = ActivityResidueComposer.BuildTopicJournalBody("coastal fog", longFindings, "research");
        Assert.True(longBody.Length > 500);
        Assert.True(ParagraphCount(longBody) >= 2);
    }

    [Fact]
    public async Task Writer_TwoPasses_PersistDistinctDescAndLinkedJournal()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-prop187-{Guid.NewGuid():N}.db");
        try
        {
            await using var session = new SqliteMemorySession(path);
            IVictoriaActivitiesLogStore log = new SqliteVictoriaActivitiesLogRepository(session);
            IVictoriaJournalStore journals = new SqliteVictoriaJournalRepository(session);
            var writer = new ActivityResidueWriter(log, journals, NullLogger<ActivityResidueWriter>.Instance);

            const string subject = "diffraction gratings";
            var a = await writer.TryWriteAsync(
                "leisure",
                subject,
                "The 600-line grating split the sodium D lines more cleanly; the orange doublet was sharp.");
            var b = await writer.TryWriteAsync(
                "leisure",
                subject,
                "Tilting the blaze by two degrees killed the ghost order smearing green into blue.");

            Assert.NotNull(a);
            Assert.NotNull(b);
            Assert.NotEqual(a!.SessionDesc, b!.SessionDesc);
            Assert.NotEqual(a.JournalBody, b.JournalBody);
            Assert.True(ParagraphCount(a.JournalBody) >= 2);
            Assert.True(ParagraphCount(b.JournalBody) >= 2);

            var recent = await log.ListRecentAsync(subject, 10);
            Assert.Equal(2, recent.Count);
            Assert.NotEqual(recent[0].SessionDesc, recent[1].SessionDesc);
            Assert.All(recent, r => Assert.Equal("thoughts", r.JournalBookId));
            Assert.All(recent, r => Assert.True(r.JournalEntryId is > 0));

            var thoughts = await journals.ListEntriesAsync("thoughts", 10);
            Assert.True(thoughts.Count >= 2);
            Assert.Contains(thoughts, e => e.Id == a.JournalEntryId);
            Assert.Contains(thoughts, e => e.Id == b.JournalEntryId);
            Assert.All(
                thoughts.Where(e => e.Id == a.JournalEntryId || e.Id == b.JournalEntryId),
                e => Assert.Contains(subject, e.TagsJson, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDeleteDb(path);
        }
    }

    [Fact]
    public async Task Store_ListSince_FiltersWhileYouWereGoneWindow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-prop187-since-{Guid.NewGuid():N}.db");
        try
        {
            await using var session = new SqliteMemorySession(path);
            IVictoriaActivitiesLogStore log = new SqliteVictoriaActivitiesLogRepository(session);

            await log.AppendAsync(
                "leisure",
                "old topic",
                "Earlier hour: I catalogued a faint fringe pattern on the cheap film grating.",
                occurredAt: "2026-11-19T01:00:00.000Z");
            await log.AppendAsync(
                "research",
                "new topic",
                "Later hour: the sodium doublet finally resolved after I cleaned the slit.",
                occurredAt: "2026-11-19T10:00:00.000Z");

            var since = await log.ListSinceAsync("2026-11-19T08:00:00.000Z", 20);
            Assert.Single(since);
            Assert.Equal("new topic", since[0].Subject);
            Assert.Contains("sodium", since[0].SessionDesc, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDb(path);
        }
    }

    [Fact]
    public async Task Writer_SkipsThinFindings_NoTheaterRow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-prop187-thin-{Guid.NewGuid():N}.db");
        try
        {
            await using var session = new SqliteMemorySession(path);
            IVictoriaActivitiesLogStore log = new SqliteVictoriaActivitiesLogRepository(session);
            IVictoriaJournalStore journals = new SqliteVictoriaJournalRepository(session);
            var writer = new ActivityResidueWriter(log, journals, NullLogger<ActivityResidueWriter>.Instance);

            var skipped = await writer.TryWriteAsync("leisure", "optics", "ok");
            Assert.Null(skipped);
            Assert.Empty(await log.ListRecentAsync(limit: 10));
        }
        finally
        {
            TryDeleteDb(path);
        }
    }

    private static int ParagraphCount(string body) =>
        body.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Length;

    private static void TryDeleteDb(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort temp cleanup
        }
    }
}
