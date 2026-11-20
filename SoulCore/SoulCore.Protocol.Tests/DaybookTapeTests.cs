using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Charter;
using SoulCore.Host.Ws;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tools;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Memory;
using SoulCore.Memory.Repositories;

namespace SoulCore.Protocol.Tests;

/// <summary>PROP-18.2: [Daybook] inject + recall_timeline.</summary>
public class DaybookTapeTests
{
    [Fact]
    public void FormatLine_ExposesProvenanceAndOccurredAt()
    {
        var line = new DaybookTape.DaybookLine(
            "journal:feeling",
            "2026-11-19T08:00:00.000Z",
            "I felt restless after the paper.",
            Preferred: true);

        var formatted = DaybookTape.FormatLine(line);

        Assert.Equal(
            "journal:feeling | 2026-11-19T08:00:00.000Z | I felt restless after the paper.",
            formatted);
    }

    [Fact]
    public void FromJournal_TagsLeisureSourceInProvenance()
    {
        var entry = new VictoriaJournalEntry(
            Id: 1,
            BookId: "life_hacks",
            Body: "When a site is down, park the lesson.",
            MoodJson: null,
            TagsJson: "[]",
            OccurredAt: "2026-11-19T09:00:00.000Z",
            CreatedAt: "2026-11-19T09:00:00.000Z",
            Source: "leisure");

        var line = DaybookTape.FromJournal(entry);

        Assert.Equal("journal:life_hacks/leisure", line.Provenance);
        Assert.Equal(
            ChronologyText.FormatJournalProvenance(entry.BookId, entry.Source),
            line.Provenance);
        Assert.True(line.Preferred);
        Assert.Contains("park the lesson", DaybookTape.FormatLine(line), StringComparison.Ordinal);
    }

    [Fact]
    public void FromActivity_EmptyFields_MatchChronologyText()
    {
        var entry = new VictoriaActivityLogEntry(
            EntryNo: 1,
            OccurredAt: "2026-11-19T12:00:00.000Z",
            Activity: "research",
            Subject: "",
            SessionDesc: "",
            JournalBookId: null,
            JournalEntryId: null,
            CreatedAt: "2026-11-19T12:00:00.000Z");

        var line = DaybookTape.FromActivity(entry);
        var shared = ChronologyText.FormatActivityText(
            entry.Activity, entry.Subject, entry.SessionDesc, DaybookTape.BodyCharLimit);

        Assert.Equal(ChronologyText.ActivityLogProvenance, line.Provenance);
        Assert.Equal("research", line.Text);
        Assert.Equal(shared, line.Text);
        Assert.DoesNotContain(" / ", line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(" — ", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ChronologyText_Truncate_RespectsLimitParams()
    {
        var body = "one\r\ntwo  three " + new string('x', 500);
        var desk = ChronologyText.TruncateBody(body, DaybookTape.BodyCharLimit);
        var tool = ChronologyText.TruncateBody(body, RecallTimelineTool.BodyCharLimit);

        Assert.True(desk.Length <= DaybookTape.BodyCharLimit + 1);
        Assert.True(tool.Length <= RecallTimelineTool.BodyCharLimit + 1);
        Assert.StartsWith("one two three", desk, StringComparison.Ordinal);
        Assert.Equal(desk, DaybookTape.TruncateBody(body));
        Assert.Equal(
            ChronologyText.TryParseOccurredAt("2026-11-19T08:00:00.000Z"),
            MemoryTape.TryParseOccurredAt("2026-11-19T08:00:00.000Z"));
    }

    [Fact]
    public void BuildDaybookBlock_TodayPreferredFirst_ActivityCrumbsTagged()
    {
        var now = new DateTimeOffset(2026, 11, 19, 18, 0, 0, TimeSpan.Zero);
        var lines = new[]
        {
            new DaybookTape.DaybookLine(
                "journal:animation",
                "2026-11-19T10:00:00.000Z",
                "want a soft smile",
                Preferred: false),
            new DaybookTape.DaybookLine(
                "journal:feeling",
                "2026-11-10T09:00:00.000Z",
                "older feeling note",
                Preferred: true),
            new DaybookTape.DaybookLine(
                "journal:feeling",
                "2026-11-19T08:00:00.000Z",
                "today feeling note",
                Preferred: true),
            new DaybookTape.DaybookLine(
                "activity-log",
                "2026-11-19T11:00:00.000Z",
                "research / phase-noise — re-read paragraph",
                Preferred: false),
        };

        var (block, dropped) = DaybookTape.BuildDaybookBlock(lines, budget: 4000, utcNow: now);

        Assert.Equal(0, dropped);
        Assert.Contains(DaybookTape.Marker, block, StringComparison.Ordinal);
        Assert.Contains("provenance-tagged", block, StringComparison.OrdinalIgnoreCase);

        var feelingToday = block.IndexOf(
            "journal:feeling | 2026-11-19T08:00:00.000Z | today feeling note",
            StringComparison.Ordinal);
        var feelingOlder = block.IndexOf(
            "journal:feeling | 2026-11-10T09:00:00.000Z | older feeling note",
            StringComparison.Ordinal);
        var activity = block.IndexOf(
            "activity-log | 2026-11-19T11:00:00.000Z | research / phase-noise — re-read paragraph",
            StringComparison.Ordinal);
        var animation = block.IndexOf(
            "journal:animation | 2026-11-19T10:00:00.000Z | want a soft smile",
            StringComparison.Ordinal);

        Assert.True(feelingToday >= 0);
        Assert.True(feelingOlder >= 0);
        Assert.True(activity >= 0);
        Assert.True(animation >= 0);
        Assert.True(feelingToday < feelingOlder);
        Assert.True(feelingToday < animation);
    }

    [Fact]
    public void BuildDaybookBlock_TightBudget_DropsLeisureTailKeepsHeaderProvenance()
    {
        var lines = Enumerable.Range(0, 8)
            .Select(i => new DaybookTape.DaybookLine(
                "journal:feeling/leisure",
                $"2026-11-19T{i:00}:00:00.000Z",
                "Leisure note " + i + " — " + new string('x', 200),
                Preferred: true))
            .ToArray();

        var (block, dropped) = DaybookTape.BuildDaybookBlock(
            lines,
            budget: 700,
            utcNow: new DateTimeOffset(2026, 11, 19, 20, 0, 0, TimeSpan.Zero));

        Assert.True(dropped >= 1);
        Assert.Contains(DaybookTape.Marker, block, StringComparison.Ordinal);
        Assert.Contains("journal:feeling/leisure", block, StringComparison.Ordinal);
        Assert.True(block.Length <= 700);
    }

    [Fact]
    public void BuildContextPreamble_InjectsDaybookBeforeMemoryInsideBudget()
    {
        var now = new DateTimeOffset(2026, 11, 19, 18, 0, 0, TimeSpan.Zero);
        var floors = new[]
        {
            new VictoriaMindBlock.FloorLine("Honesty", "I do not lie to Kayleigh.")
        };
        var memories = new[]
        {
            new MemoryRecallLine("episodic tea plan", "2026-11-19T12:00:00.000Z")
        };
        var daybook = new[]
        {
            new DaybookTape.DaybookLine(
                "journal:feeling",
                "2026-11-19T08:00:00.000Z",
                "felt curious about chronology",
                Preferred: true)
        };

        var preamble = ChatContextBuilder.BuildContextPreamble(
            floors,
            memories,
            "[SoulCore emotion]\nvalence=0.5\n",
            daybookLines: daybook,
            utcNow: now);

        Assert.Contains(DaybookTape.Marker, preamble, StringComparison.Ordinal);
        Assert.Contains(
            "journal:feeling | 2026-11-19T08:00:00.000Z | felt curious about chronology",
            preamble,
            StringComparison.Ordinal);
        Assert.Contains("[Memory]", preamble, StringComparison.Ordinal);
        Assert.Contains("2026-11-19T12:00:00.000Z | episodic tea plan", preamble, StringComparison.Ordinal);
        Assert.True(
            preamble.IndexOf(DaybookTape.Marker, StringComparison.Ordinal)
            < preamble.IndexOf("[Memory]", StringComparison.Ordinal));
        Assert.True(
            preamble.IndexOf("[Memory]", StringComparison.Ordinal)
            < preamble.IndexOf("[SoulCore emotion]", StringComparison.Ordinal));

        var emotionIdx = preamble.IndexOf("[SoulCore emotion]", StringComparison.Ordinal);
        var budgeted = preamble[..emotionIdx];
        Assert.True(budgeted.Length <= ChatContextBuilder.ContextPreambleCharLimit + MemorySteeringGuidance.Block.Length + 8);
    }

    [Fact]
    public async Task BuildAsync_InjectsDaybookFromJournalAndActivityLog()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-daybook-{Guid.NewGuid():N}.db");
        try
        {
            await using var session = new SqliteMemorySession(path);
            var journals = new SqliteVictoriaJournalRepository(session);
            var activities = new SqliteVictoriaActivitiesLogRepository(session);

            await journals.WriteEntryAsync(
                "feeling",
                "I spent the morning turning over dated recall.",
                source: "self",
                occurredAt: "2026-11-19T08:30:00.000Z");
            await activities.AppendAsync(
                activity: "research",
                subject: "PROP-18.2",
                sessionDesc: "Sketched daybook provenance tags so leisure notes stay short.",
                occurredAt: "2026-11-19T09:15:00.000Z",
                journalBookId: "feeling");

            var builder = new ChatContextBuilder(
                new EmptyMemoryStore(),
                new NullEmbeddingClient(),
                new EmptyCharter(),
                new FlatEmotion(),
                new ComputerControlGate(Options.Create(new ToolsOptions { BrowserBackend = "native" })),
                new LoggerFactory().CreateLogger<ChatContextBuilder>(),
                journals: journals,
                activitiesLog: activities);

            var ctx = await builder.BuildAsync(
                "what have you been up to?",
                useToolLoop: false,
                null,
                CancellationToken.None);

            Assert.Contains(DaybookTape.Marker, ctx.Preamble, StringComparison.Ordinal);
            Assert.Contains("journal:feeling", ctx.Preamble, StringComparison.Ordinal);
            Assert.Contains("dated recall", ctx.Preamble, StringComparison.Ordinal);
            Assert.Contains("activity-log", ctx.Preamble, StringComparison.Ordinal);
            Assert.Contains("PROP-18.2", ctx.Preamble, StringComparison.Ordinal);
            Assert.Contains("provenance tags", ctx.Preamble, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task RecallTimelineTool_ReturnsProvenanceTaggedRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-timeline-{Guid.NewGuid():N}.db");
        try
        {
            await using var session = new SqliteMemorySession(path);
            var journals = new SqliteVictoriaJournalRepository(session);
            var activities = new SqliteVictoriaActivitiesLogRepository(session);
            var memory = new SqliteEpisodicMemoryRepository(session);

            await journals.WriteEntryAsync(
                "thoughts",
                "Chronology should feel lived, not invented.",
                occurredAt: "2026-11-19T10:00:00.000Z");
            await activities.AppendAsync(
                "journal",
                "thoughts",
                "Wrote two sentences about lived chronology.",
                occurredAt: "2026-11-19T10:05:00.000Z");
            await memory.WriteEpisodicAsync(
                "Kayleigh asked about the daybook bridge.",
                "chat");

            var tool = new RecallTimelineTool(journals, activities, memory);
            using var doc = JsonDocument.Parse("""{"window":"week","limit":10}""");
            var result = await tool.ExecuteAsync(doc.RootElement, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("Timeline", result.Content, StringComparison.Ordinal);
            Assert.Contains("journal:thoughts", result.Content, StringComparison.Ordinal);
            Assert.Contains("activity-log", result.Content, StringComparison.Ordinal);
            Assert.Contains("episodic", result.Content, StringComparison.Ordinal);
            Assert.Contains("daybook bridge", result.Content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    private sealed class EmptyMemoryStore : IMemoryStore
    {
        public bool IsDatabaseOpen => true;
        public string DatabasePath => ":memory:";
        public Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);
        public Task StoreEmbeddingAsync(long episodicId, float[] vector, string model, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(long, string)>>(Array.Empty<(long, string)>());
        public Task<IReadOnlyList<string>> RecallSimilarAsync(float[] queryVector, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<IReadOnlyList<MemoryRecallLine>> RecallSimilarDatedAsync(float[] queryVector, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MemoryRecallLine>>(Array.Empty<MemoryRecallLine>());
        public Task<IReadOnlyList<MemoryRecallLine>> RecallRecentDatedAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MemoryRecallLine>>(Array.Empty<MemoryRecallLine>());
    }

    private sealed class EmptyCharter : ICharter
    {
        public Task<IReadOnlyList<string>> GetAnchorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<IReadOnlyList<string>> GetAnchorsByKindAsync(string kind, bool? lockedOnly = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<IReadOnlyList<CharterAnchorInfo>> ListAnchorDetailsAsync(
            string? kind = null, string? book = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CharterAnchorInfo>>(Array.Empty<CharterAnchorInfo>());
        public Task<int> SeedAsync(IReadOnlyList<CharterAnchorSeed> seeds, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class FlatEmotion : IEmotionState
    {
        public Task<IReadOnlyDictionary<string, double>> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, double>>(
                new Dictionary<string, double>
                {
                    ["valence"] = 0.2,
                    ["arousal"] = 0.4,
                    ["dominance"] = 0.5,
                    ["focus"] = 0.6
                });
        public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) => Task.FromResult(1L);
        public Task SetAsync(IReadOnlyDictionary<string, double> components, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
