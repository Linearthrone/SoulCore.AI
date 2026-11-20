using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Host.Companion;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

public class SmsOutboundComposerTests
{
    [Fact]
    public void Compose_AnswerPlusPublicUrl_Within480()
    {
        var shape = SmsOutboundComposer.Compose(
            "Rain tonight in Austin.",
            new[] { "https://weather.gov/austin" });

        Assert.Contains("Rain tonight", shape.OutboundText, StringComparison.Ordinal);
        Assert.Contains("https://weather.gov/austin", shape.OutboundText, StringComparison.Ordinal);
        Assert.True(shape.OutboundText.Length <= SmsOutboundComposer.MaxSmsChars);
        Assert.Null(shape.LongFindingsForPaper);
        Assert.Equal(new[] { "https://weather.gov/austin" }, shape.PublicUrlsIncluded);
    }

    [Fact]
    public void Compose_LongAnswer_TruncatesAndFlagsPaper()
    {
        var longAnswer = new string('a', 600);
        var shape = SmsOutboundComposer.Compose(
            longAnswer,
            new[] { "https://example.com/article" });

        Assert.True(shape.OutboundText.Length <= SmsOutboundComposer.MaxSmsChars);
        Assert.Contains("https://example.com/article", shape.OutboundText, StringComparison.Ordinal);
        Assert.Contains('…', shape.OutboundText);
        Assert.Equal(longAnswer, shape.LongFindingsForPaper);
    }

    [Fact]
    public void Compose_StripsInternalPaperAddress_KeepsPublicUrl()
    {
        var shape = SmsOutboundComposer.Compose(
            "See http://127.0.0.1:7700/research/papers/9 and the story.",
            new[] { "https://nytimes.com/story" });

        Assert.DoesNotContain("127.0.0.1", shape.OutboundText, StringComparison.Ordinal);
        Assert.DoesNotContain("/research/papers", shape.OutboundText, StringComparison.Ordinal);
        Assert.Contains("https://nytimes.com/story", shape.OutboundText, StringComparison.Ordinal);
    }

    [Fact]
    public void IsPublicHttpUrl_RejectsLoopbackAndPrivate()
    {
        Assert.True(SmsOutboundComposer.IsPublicHttpUrl("https://example.com/x"));
        Assert.False(SmsOutboundComposer.IsPublicHttpUrl("http://localhost/x"));
        Assert.False(SmsOutboundComposer.IsPublicHttpUrl("http://127.0.0.1:7700/research/papers/1"));
        Assert.False(SmsOutboundComposer.IsPublicHttpUrl("https://192.168.1.5/p"));
        Assert.False(SmsOutboundComposer.IsPublicHttpUrl("ftp://example.com/x"));
    }

    [Fact]
    public void CollectPublicUrlsFromToolTrace_TakesBrowserNavigate()
    {
        var args = JsonDocument.Parse("""{"url":"https://bbc.com/news"}""").RootElement;
        IEnumerable<(string Name, JsonElement? Arguments, string? Content)> trace =
        [
            ("browser_navigate", args, "navigated ok https://bbc.com/news"),
            ("memory_recall", null, "no url here")
        ];
        var urls = SmsOutboundComposer.CollectPublicUrlsFromToolTrace(trace);

        Assert.Equal(new[] { "https://bbc.com/news" }, urls);
    }

    [Fact]
    public void Compose_480HardCeiling()
    {
        var urls = Enumerable.Range(0, 20)
            .Select(i => $"https://example.com/very/long/path/segment/{i}/article")
            .ToArray();
        var shape = SmsOutboundComposer.Compose(new string('x', 400), urls);
        Assert.True(shape.OutboundText.Length <= SmsOutboundComposer.MaxSmsChars);
    }
}

public class SmsInboundProp207Tests
{
    private const string AllowlistedPhone = "+15551234567";

    [Fact]
    public async Task Inbound_ToolNavigate_OutboundIncludesPublicUrl()
    {
        var outbound = CreateOutbound();
        var research = new RecordingResearch();
        var inference = new NavigateThenAnswerInference { Reply = "High of 72 and clear." };
        var inbound = CreateInbound(inference, outbound, new NavRegistry(), research);

        var result = await inbound.HandleAsync(
            new SmsInboundRequest(AllowlistedPhone, "weather in Austin?", null, null));

        Assert.True(result.Ok);
        Assert.Equal(1, inference.ToolLoopCalls);
        Assert.Contains("https://weather.gov/austin", result.ReplyText!, StringComparison.Ordinal);
        Assert.Contains("High of 72", result.ReplyText!, StringComparison.Ordinal);
        Assert.True(result.ReplyText!.Length <= 480);
        var job = Assert.Single(outbound.ListPending());
        Assert.Contains("https://weather.gov/austin", job.Text!, StringComparison.Ordinal);
        Assert.Empty(research.Papers);
    }

    [Fact]
    public async Task Inbound_LongFindings_PersistsResearchPaper_NotJournal()
    {
        var outbound = CreateOutbound();
        var research = new RecordingResearch();
        var longBody = "Findings: " + new string('b', 520);
        var inference = new NavigateThenAnswerInference { Reply = longBody };
        var inbound = CreateInbound(inference, outbound, new NavRegistry(), research);

        var result = await inbound.HandleAsync(
            new SmsInboundRequest(AllowlistedPhone, "Deep dive on neural lace papers", null, null));

        Assert.True(result.Ok);
        Assert.True(result.ReplyText!.Length <= 480);
        Assert.Contains("https://weather.gov/austin", result.ReplyText, StringComparison.Ordinal);
        var paper = Assert.Single(research.Papers);
        Assert.Contains("Findings:", paper.BodyApa, StringComparison.Ordinal);
        Assert.Equal("review", paper.Kind);
        Assert.Contains("https://weather.gov/austin", paper.BibliographyApa, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSmsPreamble_ToolLoop_HasAllowList_NoPrivateJournals()
    {
        var p = SmsInboundService.BuildSmsPreamble(
            new[] { "prior" },
            useToolLoop: true,
            mindBlock: "[floors]\nBe honest.");

        Assert.Contains("browser_navigate", p, StringComparison.Ordinal);
        Assert.Contains("research_paper_write", p, StringComparison.Ordinal);
        Assert.Contains("Never call desktop_open_app", p, StringComparison.Ordinal);
        Assert.Contains("[Mind]", p, StringComparison.Ordinal);
        Assert.Contains("Be honest.", p, StringComparison.Ordinal);
        Assert.DoesNotContain("thoughts", p, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dreams", p, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Kayleigh", p, StringComparison.Ordinal);
    }

    private static SmsOutboundService CreateOutbound() =>
        new(
            Options.Create(new SmsOptions
            {
                AllowlistE164 = AllowlistedPhone,
                OutboundEnabled = true,
                MinSecondsBetweenSms = 0,
                MinSecondsBetweenMms = 0,
                MaxSmsPerHour = 30
            }),
            NullLogger<SmsOutboundService>.Instance);

    private static SmsInboundService CreateInbound(
        IInferenceClient inference,
        ISmsOutboundService outbound,
        IToolRegistry registry,
        IVictoriaResearchStore research) =>
        new(
            Options.Create(new SmsOptions
            {
                AllowlistE164 = AllowlistedPhone,
                StubWhenModelDown = false,
                OutboundEnabled = true,
                AutoReplySmsEnabled = true,
                MinSecondsBetweenSms = 0,
                ConversationSessionId = "presence-local"
            }),
            Options.Create(new InferenceOptions { Enabled = true }),
            Options.Create(new ChatWsOptions()),
            Options.Create(new CompanionOptions()),
            inference,
            new Prop207Memory(),
            new ChatSessionHistoryStore(32),
            new Prop207Media(),
            new PresenceWsHub(NullLogger<PresenceWsHub>.Instance),
            outbound,
            NullLogger<SmsInboundService>.Instance,
            toolRegistry: registry,
            research: research);

    private sealed class NavigateThenAnswerInference : IInferenceClient
    {
        public string Reply { get; set; } = "ok";
        public int ToolLoopCalls { get; private set; }

        public Task<string> CompleteAsync(
            string prompt,
            string? systemPreamble = null,
            CancellationToken cancellationToken = default,
            int? maxTokens = null) =>
            Task.FromResult("SHOULD_NOT_RUN");

        public async Task<string> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            IToolRegistry registry,
            CancellationToken cancellationToken = default,
            ToolLoopOptions? loopOptions = null)
        {
            ToolLoopCalls++;
            await registry
                .ExecuteAsync(
                    "browser_navigate",
                    JsonDocument.Parse("""{"url":"https://weather.gov/austin"}""").RootElement,
                    cancellationToken)
                .ConfigureAwait(false);
            return Reply;
        }
    }

    private sealed class NavRegistry : IToolRegistry
    {
        public IReadOnlyList<ToolDefinition> GetDefinitions()
        {
            var empty = JsonDocument.Parse("{}").RootElement;
            return new[]
            {
                new ToolDefinition("browser_navigate", "nav", empty),
                new ToolDefinition("desktop_open_app", "bad", empty)
            };
        }

        public Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default) =>
            Task.FromResult(new ToolResult(true, $"ok:{name}", null));
    }

    private sealed class RecordingResearch : IVictoriaResearchStore
    {
        public List<VictoriaResearchPaper> Papers { get; } = new();
        private long _nextSubject = 1;
        private long _nextPaper = 1;

        public Task<IReadOnlyList<VictoriaResearchSubject>> ListSubjectsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VictoriaResearchSubject>>(Array.Empty<VictoriaResearchSubject>());

        public Task<VictoriaResearchSubject?> GetSubjectAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult<VictoriaResearchSubject?>(null);

        public Task<VictoriaResearchSubject?> GetSubjectBySlugAsync(
            string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult<VictoriaResearchSubject?>(null);

        public Task<VictoriaResearchSubject> OpenOrCreateSubjectAsync(
            string title, string? summary = null, CancellationToken cancellationToken = default)
        {
            var id = _nextSubject++;
            return Task.FromResult(new VictoriaResearchSubject(
                id, VictoriaResearchRules.Slugify(title), title, summary ?? "", "now", "now"));
        }

        public Task<bool> TouchSubjectAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<VictoriaResearchPaper>> ListPapersAsync(
            long? subjectId = null, int limit = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VictoriaResearchPaper>>(Papers);

        public Task<VictoriaResearchPaper?> GetPaperAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Papers.FirstOrDefault(p => p.Id == id));

        public Task<VictoriaResearchPaper> UpsertPaperAsync(
            long subjectId,
            string title,
            string kind,
            string? abstractText,
            string? bodyApa,
            string? bibliographyApa,
            string? status,
            long? paperId = null,
            CancellationToken cancellationToken = default)
        {
            var paper = new VictoriaResearchPaper(
                paperId ?? _nextPaper++,
                subjectId,
                VictoriaResearchRules.NormalizeKind(kind),
                title,
                abstractText ?? "",
                bodyApa ?? "",
                bibliographyApa ?? "",
                VictoriaResearchRules.NormalizeStatus(status),
                "now",
                "now");
            Papers.RemoveAll(p => p.Id == paper.Id);
            Papers.Add(paper);
            return Task.FromResult(paper);
        }
    }

    private sealed class Prop207Memory : IMemoryStore
    {
        public bool IsDatabaseOpen => true;
        public string DatabasePath => ":memory:";

        public Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);

        public Task StoreEmbeddingAsync(
            long episodicId, float[] vector, string model, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(
            int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(long, string)>>(Array.Empty<(long, string)>());

        public Task<IReadOnlyList<string>> RecallSimilarAsync(
            float[] queryVector, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<MemoryRecallLine>> RecallSimilarDatedAsync(
            float[] queryVector, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MemoryRecallLine>>(Array.Empty<MemoryRecallLine>());

        public Task<IReadOnlyList<MemoryRecallLine>> RecallRecentDatedAsync(
            int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MemoryRecallLine>>(Array.Empty<MemoryRecallLine>());
    }

    private sealed class Prop207Media : ICompanionMediaService
    {
        public Task<IReadOnlyList<object>> ListModelsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());

        public Task<CompanionMediaAsset> GenerateAsync(
            string positivePrompt, string? negativePrompt, string? model, string? contactId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<CompanionMediaAsset> StoreInboundAsync(
            byte[] bytes, string contentType, string? contactId, CancellationToken ct = default) =>
            Task.FromResult(new CompanionMediaAsset(
                "m", "victoria", "x.png", contentType, bytes.LongLength, DateTimeOffset.UtcNow, null));

        public bool TryGetFile(string mediaId, out string fullPath, out CompanionMediaAsset? meta)
        {
            fullPath = "";
            meta = null;
            return false;
        }

        public Task PushGeneratedToChatAsync(string mediaId, string? caption, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
