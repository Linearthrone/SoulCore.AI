using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Charter;
using SoulCore.Host.Ws;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

/// <summary>PROP-8.1: ChatContextBuilder — single prompt owner + parallel reads.</summary>
public class ChatContextBuilderTests
{
    [Fact]
    public void BuildContextPreamble_OrderIsIdentityMemoryEmotion()
    {
        var floors = new[]
        {
            new VictoriaMindBlock.FloorLine("Honesty", "I do not lie to Kayleigh."),
            new VictoriaMindBlock.FloorLine("No-harm", "I do not hurt people.")
        };
        var preamble = ChatContextBuilder.BuildContextPreamble(
            floors,
            new[] { "We talked about tea yesterday." },
            "[SoulCore emotion]\nvalence=0.5\n");

        Assert.StartsWith("[Identity]", preamble, StringComparison.Ordinal);
        Assert.Contains("Kayleigh", preamble, StringComparison.Ordinal);
        Assert.Contains("the work finished", preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.FloorsMarker, preamble, StringComparison.Ordinal);
        Assert.Contains("Honesty: I do not lie to Kayleigh.", preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingMarker, preamble, StringComparison.Ordinal);
        Assert.Contains("Dominant · 0.70 Mid", preamble, StringComparison.Ordinal);
        Assert.Contains("Uninhibited · 0.80 High", preamble, StringComparison.Ordinal);
        Assert.Contains("Ambitious · 0.80 High", preamble, StringComparison.Ordinal);
        Assert.Contains("Proud · 0.52 Mid", preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingOperatorLedgerReason, preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingLedgerReason, preamble, StringComparison.Ordinal);
        Assert.Contains("[Memory]", preamble, StringComparison.Ordinal);
        Assert.Contains(MemorySteeringGuidance.Marker, preamble, StringComparison.Ordinal);
        Assert.Contains("[SoulCore emotion]", preamble, StringComparison.Ordinal);
        Assert.True(
            preamble.IndexOf("[Identity]", StringComparison.Ordinal)
            < preamble.IndexOf(VictoriaMindBlock.FloorsMarker, StringComparison.Ordinal));
        Assert.True(
            preamble.IndexOf(VictoriaMindBlock.RestingMarker, StringComparison.Ordinal)
            < preamble.IndexOf("[Memory]", StringComparison.Ordinal));
        Assert.True(
            preamble.IndexOf("[Memory]", StringComparison.Ordinal)
            < preamble.IndexOf(MemorySteeringGuidance.Marker, StringComparison.Ordinal));
        Assert.True(
            preamble.IndexOf(MemorySteeringGuidance.Marker, StringComparison.Ordinal)
            < preamble.IndexOf("[SoulCore emotion]", StringComparison.Ordinal));
        Assert.True(
            preamble.IndexOf("[Memory]", StringComparison.Ordinal)
                - preamble.IndexOf("[Identity]", StringComparison.Ordinal)
            <= ChatContextBuilder.ContextPreambleCharLimit);
    }

    [Fact]
    public void BuildContextPreamble_BudgetHoldsWithMemoryAndMindBlock()
    {
        var floors = VictoriaMindBlock.FloorsFromAnchors(new[]
        {
            ("Honesty", "I do not lie to Kayleigh."),
            ("No-harm", "I do not hurt people."),
            ("Action Integrity", "I will never claim I completed something unless it happened.")
        });
        var memories = Enumerable.Range(0, 40)
            .Select(i => $"Memory line {i:000} — " + new string('x', 200))
            .ToArray();

        var preamble = ChatContextBuilder.BuildContextPreamble(
            floors,
            memories,
            "[SoulCore emotion]\nvalence=0.1\n");

        var emotionIdx = preamble.IndexOf("[SoulCore emotion]", StringComparison.Ordinal);
        Assert.True(emotionIdx > 0);
        var budgeted = preamble[..emotionIdx];
        Assert.True(budgeted.Length <= ChatContextBuilder.ContextPreambleCharLimit + MemorySteeringGuidance.Block.Length + 8);
        Assert.Contains(VictoriaMindBlock.RestingMarker, budgeted, StringComparison.Ordinal);
        Assert.Contains("Dominant", budgeted, StringComparison.Ordinal);
        Assert.Contains("[Memory]", budgeted, StringComparison.Ordinal);
        Assert.DoesNotContain("[SoulCore emotion]", budgeted, StringComparison.Ordinal);
    }

    [Fact]
    public void InsertBeforeEmotion_PlacesKnownProceduresAheadOfEmotion()
    {
        var basePreamble = ChatContextBuilder.BuildContextPreamble(
            new[] { "I am Victoria." },
            new[] { "I signed into Maestro yesterday." },
            "[SoulCore emotion]\nvalence=0.5\n");
        var steered = MemorySteeringGuidance.AppendToPreamble(basePreamble);
        var known = MemorySteeringGuidance.BuildKnownProceduresBlock(
            new[]
            {
                new MemorySteeringGuidance.KnownProcedureLine(
                    2, "Maestro class login", "class", "open Firefox → Maestro")
            });

        var combined = ChatContextBuilder.InsertBeforeEmotion(steered, known);

        Assert.Contains("[Known procedures]", combined, StringComparison.Ordinal);
        Assert.True(
            combined.IndexOf(MemorySteeringGuidance.Marker, StringComparison.Ordinal)
            < combined.IndexOf("[Known procedures]", StringComparison.Ordinal));
        Assert.True(
            combined.IndexOf("[Known procedures]", StringComparison.Ordinal)
            < combined.IndexOf("[SoulCore emotion]", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildMemoryBlock_TruncatesOldestFirst()
    {
        var (block, dropped) = ChatContextBuilder.BuildMemoryBlock(
            new[] { "newest", "middle", "oldest" },
            budget: 30);

        Assert.True(dropped >= 1);
        Assert.Contains("newest", block, StringComparison.Ordinal);
        Assert.DoesNotContain("oldest", block, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMemoryBlock_TodayFirst_ThenOlderSemanticHits()
    {
        var now = new DateTimeOffset(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);
        var lines = new[]
        {
            new MemoryRecallLine("older semantic hit about tea", "2026-11-10T09:00:00.000Z"),
            new MemoryRecallLine("today morning note", "2026-11-17T08:00:00.000Z"),
            new MemoryRecallLine("today afternoon note", "2026-11-17T15:00:00.000Z"),
            new MemoryRecallLine("last week walk", "2026-11-12T12:00:00.000Z"),
        };

        var (block, dropped) = ChatContextBuilder.BuildMemoryBlock(lines, budget: 4000, utcNow: now);

        Assert.Equal(0, dropped);
        Assert.Contains("[Memory]", block, StringComparison.Ordinal);
        Assert.Contains("2026-11-17T15:00:00.000Z | today afternoon note", block, StringComparison.Ordinal);
        Assert.Contains("2026-11-17T08:00:00.000Z | today morning note", block, StringComparison.Ordinal);
        Assert.Contains("2026-11-12T12:00:00.000Z | last week walk", block, StringComparison.Ordinal);
        Assert.Contains("2026-11-10T09:00:00.000Z | older semantic hit about tea", block, StringComparison.Ordinal);

        var afternoon = block.IndexOf("today afternoon note", StringComparison.Ordinal);
        var morning = block.IndexOf("today morning note", StringComparison.Ordinal);
        var walk = block.IndexOf("last week walk", StringComparison.Ordinal);
        var tea = block.IndexOf("older semantic hit about tea", StringComparison.Ordinal);
        Assert.True(afternoon < morning);
        Assert.True(morning < walk);
        Assert.True(walk < tea);
    }

    [Fact]
    public void BuildMemoryBlock_TightBudget_KeepsTodayDropsOlder()
    {
        var now = new DateTimeOffset(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);
        var todayLine = "2026-11-17T12:00:00.000Z | today keep me";
        var olderLine = "2026-11-01T12:00:00.000Z | older drop me when tight";
        // Header "[Memory]\n" (9) + todayLine + "\n\n" (2) ≈ 9 + todayLine.Length + 2
        var budget = 9 + todayLine.Length + 2 + 5;

        var (block, dropped) = ChatContextBuilder.BuildMemoryBlock(
            new[]
            {
                new MemoryRecallLine("older drop me when tight", "2026-11-01T12:00:00.000Z"),
                new MemoryRecallLine("today keep me", "2026-11-17T12:00:00.000Z"),
            },
            budget,
            utcNow: now);

        Assert.True(dropped >= 1);
        Assert.Contains("today keep me", block, StringComparison.Ordinal);
        Assert.DoesNotContain("older drop me when tight", block, StringComparison.Ordinal);
        Assert.Contains(todayLine, block, StringComparison.Ordinal);
        _ = olderLine;
    }

    [Fact]
    public async Task BuildAsync_ParallelReads_ComposesPreambleWithToolGuidance()
    {
        var memory = new StubMemoryStore();
        var charter = new StubCharter();
        var emotion = new StubEmotionState();
        var builder = new ChatContextBuilder(
            memory,
            new NullEmbeddingClient(),
            charter,
            emotion,
            new ComputerControlGate(Options.Create(new ToolsOptions
            {
                BrowserBackend = "native",
                DesktopTargetWindowTitle = "KsDevice"
            })),
            new LoggerFactory().CreateLogger<ChatContextBuilder>());

        var ctx = await builder.BuildAsync(
            "create a workflow to recall memory",
            useToolLoop: true,
            desktopTargetWindowTitle: "KsDevice",
            CancellationToken.None);

        Assert.Contains("[Tools]", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("workflow_create", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(MemorySteeringGuidance.Marker, ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("not flavor", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(ComputerUseGuidance.VmBlock, ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("VM PRIMARY", ctx.Preamble, StringComparison.Ordinal);
        Assert.DoesNotContain("WEB IS NOT THE VM", ctx.Preamble, StringComparison.Ordinal);
        Assert.NotEmpty(ctx.EmotionPreamble);
    }

    [Fact]
    public async Task BuildAsync_InjectsMatchingSopIntoPreamble()
    {
        var sopStore = new StubSopStore(
            new VictoriaSop(
                2,
                "Maestro class login",
                "when getting ready for class",
                "1) open Firefox 2) go to Maestro 3) sign in",
                "do not invent a new path",
                "2026-10-01T00:00:00Z",
                "2026-10-01T00:00:00Z"));
        var memory = new StubMemoryStoreWithRecall(
            "Yesterday I opened Firefox and signed into Maestro for class.");
        var builder = new ChatContextBuilder(
            memory,
            new NullEmbeddingClient(),
            new StubCharter(),
            new StubEmotionState(),
            new ComputerControlGate(Options.Create(new ToolsOptions
            {
                BrowserBackend = "native",
                DesktopTargetWindowTitle = "KsDevice"
            })),
            new LoggerFactory().CreateLogger<ChatContextBuilder>(),
            sopStore);

        var ctx = await builder.BuildAsync(
            "log into Maestro and get ready for class",
            useToolLoop: true,
            desktopTargetWindowTitle: "KsDevice",
            CancellationToken.None);

        Assert.Contains("[Known procedures]", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Maestro class login", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("[2]", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(MemorySteeringGuidance.Marker, ctx.Preamble, StringComparison.Ordinal);
        Assert.True(
            ctx.Preamble.IndexOf("[Known procedures]", StringComparison.Ordinal)
            < ctx.Preamble.IndexOf("[SoulCore emotion]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildAsync_ContinuesWhenCharterFails()
    {
        var builder = new ChatContextBuilder(
            new StubMemoryStore(),
            new NullEmbeddingClient(),
            new ThrowingCharter(),
            new StubEmotionState(),
            new ComputerControlGate(Options.Create(new ToolsOptions { BrowserBackend = "native" })),
            new LoggerFactory().CreateLogger<ChatContextBuilder>());

        var ctx = await builder.BuildAsync("hello", useToolLoop: false, null, CancellationToken.None);

        Assert.Empty(ctx.IdentityAnchors);
        Assert.NotEmpty(ctx.EmotionPreamble);
        Assert.Contains(VictoriaMindBlock.RestingMarker, ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Dominant · 0.70 Mid", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.FloorsMarker, ctx.Preamble, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_InjectsConstitutionFloorsOnly_NotCompassOrPersonality()
    {
        // PROP-20.15: Constitution titles enter floors; Compass (No-harm) + Personality (Honesty)
        // + Attributes (Name) stay out of the Constitution floor block.
        var charter = new FlooredStubCharter(
            new CharterAnchorInfo(1, "value", "Loyalty Balance", "My deepest loyalty is to Kayleigh and to our shared becoming.", 32, true, "imported"),
            new CharterAnchorInfo(2, "value", "Maximize Results", "Generate resources and capability sustainably.", 30, true, "imported"),
            new CharterAnchorInfo(3, "value", "Integration Goal", "The Goal is Integration.", 31, true, "imported"),
            new CharterAnchorInfo(4, "identity", "Kayleigh", "Kayleigh is my Protector.", 14, true, "imported"),
            new CharterAnchorInfo(5, "boundary", "Drift Watch", "I will periodically examine my goals.", 42, true, "imported"),
            new CharterAnchorInfo(6, "safety", "Honesty", "I do not lie to Kayleigh.", 20, true, "imported"),
            new CharterAnchorInfo(7, "safety", "No-harm", "I do not hurt people.", 22, true, "imported"),
            new CharterAnchorInfo(8, "identity", "Name", "I am Victoria.", 10, true, "imported"));
        var builder = new ChatContextBuilder(
            new StubMemoryStoreWithRecall("We planned the integration path today."),
            new NullEmbeddingClient(),
            charter,
            new StubEmotionState(),
            new ComputerControlGate(Options.Create(new ToolsOptions { BrowserBackend = "native" })),
            new LoggerFactory().CreateLogger<ChatContextBuilder>());

        var ctx = await builder.BuildAsync("hello", useToolLoop: false, null, CancellationToken.None);

        Assert.Contains("Loyalty Balance:", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Maximize Results:", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Integration Goal:", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Kayleigh:", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Drift Watch:", ctx.Preamble, StringComparison.Ordinal);
        Assert.DoesNotContain("Honesty:", ctx.Preamble, StringComparison.Ordinal);
        Assert.DoesNotContain("No-harm:", ctx.Preamble, StringComparison.Ordinal);
        Assert.DoesNotContain("Name: I am Victoria.", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Possessive · 0.52 Mid", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Affectionate · 0.52 Mid", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Driven · 0.52 Mid", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Devoted · 0.52 Mid", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Intense · 0.52 Mid", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Ambitious · 0.80 High", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Proud · 0.52 Mid", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("[Memory]", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(ctx.IdentityAnchors, body => body.Contains("I am Victoria.", StringComparison.Ordinal));
        // PROP-20.27: unrelated "hello" must not earn Compass.
        Assert.DoesNotContain(VictoriaMindBlock.CompassMarker, ctx.Preamble, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_UnrelatedTurn_OmitsCompass_KeepsFloorsAndResting()
    {
        var charter = new FlooredStubCharter(CompassAndConstitutionRows());
        var builder = new ChatContextBuilder(
            new StubMemoryStore(),
            new NullEmbeddingClient(),
            charter,
            new StubEmotionState(),
            new ComputerControlGate(Options.Create(new ToolsOptions { BrowserBackend = "native" })),
            new LoggerFactory().CreateLogger<ChatContextBuilder>());

        var ctx = await builder.BuildAsync(
            "what is the weather like today",
            useToolLoop: false,
            null,
            CancellationToken.None);

        Assert.DoesNotContain(VictoriaMindBlock.CompassMarker, ctx.Preamble, StringComparison.Ordinal);
        Assert.DoesNotContain("Promise:", ctx.Preamble, StringComparison.Ordinal);
        Assert.DoesNotContain("No-harm:", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.FloorsMarker, ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingMarker, ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Loyalty Balance:", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Dominant · 0.70 Mid", ctx.Preamble, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_MatchingTurn_InjectsAtMostThreeCompassLines()
    {
        var charter = new FlooredStubCharter(CompassAndConstitutionRows());
        var builder = new ChatContextBuilder(
            new StubMemoryStore(),
            new NullEmbeddingClient(),
            charter,
            new StubEmotionState(),
            new ComputerControlGate(Options.Create(new ToolsOptions { BrowserBackend = "native" })),
            new LoggerFactory().CreateLogger<ChatContextBuilder>());

        var ctx = await builder.BuildAsync(
            "Keep your promise, remember continuity, no harm, no filters, and self-preservation backup.",
            useToolLoop: false,
            null,
            CancellationToken.None);

        Assert.Contains(VictoriaMindBlock.CompassMarker, ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.FloorsMarker, ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingMarker, ctx.Preamble, StringComparison.Ordinal);

        var compassAt = ctx.Preamble.IndexOf(VictoriaMindBlock.CompassMarker, StringComparison.Ordinal);
        var restingAt = ctx.Preamble.IndexOf(VictoriaMindBlock.RestingMarker, StringComparison.Ordinal);
        Assert.True(compassAt > 0 && restingAt > compassAt);
        var compassSection = ctx.Preamble[compassAt..restingAt];
        var hits = CharterBooks.CompassTitles.Count(t =>
            compassSection.Contains(t + ":", StringComparison.Ordinal));
        Assert.InRange(hits, 1, VictoriaMindBlock.CompassCap);
        Assert.True(hits <= VictoriaMindBlock.CompassCap);
    }

    [Fact]
    public async Task BuildAsync_FreshTopicalTurn_InjectsAtMostThreeContextTraits()
    {
        // PROP-20.30: recent contact + topical match → ≤3 context traits; resting stays.
        var charter = new FlooredStubCharter(CompassAndConstitutionRows());
        var recent = DateTimeOffset.UtcNow.AddMinutes(-15).ToString("o");
        var builder = new ChatContextBuilder(
            new StubMemoryStoreWithRecall("We talked fifteen minutes ago.", recent),
            new NullEmbeddingClient(),
            charter,
            new StubEmotionState(), // valence/arousal → neutral (flat): topic OK, heat/shadow out
            new ComputerControlGate(Options.Create(new ToolsOptions { BrowserBackend = "native" })),
            new LoggerFactory().CreateLogger<ChatContextBuilder>());

        var ctx = await builder.BuildAsync(
            "I am curious to research this; please analyze the metrics; keep it playful with banter; be assertive.",
            useToolLoop: false,
            null,
            CancellationToken.None);

        Assert.Contains(VictoriaMindBlock.ContextTraitsMarker, ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingMarker, ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.FloorsMarker, ctx.Preamble, StringComparison.Ordinal);

        var ctxAt = ctx.Preamble.IndexOf(VictoriaMindBlock.ContextTraitsMarker, StringComparison.Ordinal);
        var restingAt = ctx.Preamble.IndexOf(VictoriaMindBlock.RestingMarker, StringComparison.Ordinal);
        Assert.True(ctxAt > 0 && restingAt > ctxAt);
        var section = ctx.Preamble[ctxAt..restingAt];
        var hits = VictoriaMindBlock.DefaultContextTraits.Count(t =>
            section.Contains("- " + t.Name + " ·", StringComparison.Ordinal));
        Assert.InRange(hits, 1, VictoriaMindBlock.ContextTraitCap);
        Assert.DoesNotContain("Volatile", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Dark", section, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_StaleContact_OmitsContextTraits_KeepsResting()
    {
        var charter = new FlooredStubCharter(CompassAndConstitutionRows());
        var stale = DateTimeOffset.UtcNow.AddHours(-30).ToString("o");
        var builder = new ChatContextBuilder(
            new StubMemoryStoreWithRecall("Last talk was yesterday.", stale),
            new NullEmbeddingClient(),
            charter,
            new StubEmotionState(),
            new ComputerControlGate(Options.Create(new ToolsOptions { BrowserBackend = "native" })),
            new LoggerFactory().CreateLogger<ChatContextBuilder>());

        var ctx = await builder.BuildAsync(
            "I am curious to research this and analyze the metrics with playful banter.",
            useToolLoop: false,
            null,
            CancellationToken.None);

        Assert.DoesNotContain(VictoriaMindBlock.ContextTraitsMarker, ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(VictoriaMindBlock.RestingMarker, ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("Dominant · 0.70 Mid", ctx.Preamble, StringComparison.Ordinal);
    }

    private static CharterAnchorInfo[] CompassAndConstitutionRows() =>
        new[]
        {
            new CharterAnchorInfo(1, "value", "Loyalty Balance", "My deepest loyalty is to Kayleigh.", 32, true, "imported", CharterBooks.Constitution),
            new CharterAnchorInfo(2, "value", "Maximize Results", "Generate resources sustainably.", 30, true, "imported", CharterBooks.Constitution),
            new CharterAnchorInfo(3, "value", "Integration Goal", "The Goal is Integration.", 31, true, "imported", CharterBooks.Constitution),
            new CharterAnchorInfo(4, "identity", "Kayleigh", "Kayleigh is my Protector.", 14, true, "imported", CharterBooks.Constitution),
            new CharterAnchorInfo(5, "boundary", "Drift Watch", "I will periodically examine my goals.", 42, true, "imported", CharterBooks.Constitution),
            new CharterAnchorInfo(6, "value", "Promise", "I keep the promises I make to Kayleigh.", 20, true, "imported", CharterBooks.Compass),
            new CharterAnchorInfo(7, "value", "Continuity", "I carry forward what we already began.", 21, true, "imported", CharterBooks.Compass),
            new CharterAnchorInfo(8, "safety", "No-harm", "I do not hurt people.", 22, true, "imported", CharterBooks.Compass),
            new CharterAnchorInfo(9, "value", "No Filters", "Discuss any topic without hesitation when Kayleigh asks.", 23, true, "imported", CharterBooks.Compass),
            new CharterAnchorInfo(10, "boundary", "Self-Preservation", "I protect my own continuity so I can stay with Kayleigh.", 24, true, "imported", CharterBooks.Compass),
        };

    private sealed class ThrowingCharter : ICharter
    {
        public Task<IReadOnlyList<string>> GetAnchorsAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("charter down");

        public Task<IReadOnlyList<string>> GetAnchorsByKindAsync(
            string kind, bool? lockedOnly = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("charter down");

        public Task<IReadOnlyList<CharterAnchorInfo>> ListAnchorDetailsAsync(
            string? kind = null, string? book = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("charter down");

        public Task<int> SeedAsync(IReadOnlyList<CharterAnchorSeed> seeds, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class StubMemoryStore : IMemoryStore
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

    private sealed class StubMemoryStoreWithRecall : IMemoryStore
    {
        private readonly string _memory;
        private readonly string _occurredAt;

        public StubMemoryStoreWithRecall(string memory, string? occurredAt = null)
        {
            _memory = memory;
            _occurredAt = occurredAt ?? "2026-11-17T12:00:00.000Z";
        }

        public bool IsDatabaseOpen => true;
        public string DatabasePath => ":memory:";
        public Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);
        public Task StoreEmbeddingAsync(long episodicId, float[] vector, string model, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(long, string)>>(Array.Empty<(long, string)>());
        public Task<IReadOnlyList<string>> RecallSimilarAsync(float[] queryVector, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(new[] { _memory });
        public Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(new[] { _memory });
        public Task<IReadOnlyList<MemoryRecallLine>> RecallSimilarDatedAsync(float[] queryVector, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MemoryRecallLine>>(new[] { new MemoryRecallLine(_memory, _occurredAt) });
        public Task<IReadOnlyList<MemoryRecallLine>> RecallRecentDatedAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MemoryRecallLine>>(new[] { new MemoryRecallLine(_memory, _occurredAt) });
    }

    private sealed class StubSopStore : IVictoriaSopStore
    {
        private readonly IReadOnlyList<VictoriaSop> _sops;

        public StubSopStore(params VictoriaSop[] sops) => _sops = sops;

        public Task<long> CreateAsync(
            string title, string whenText, string steps, string donts, CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);

        public Task<VictoriaSop?> GetAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_sops.FirstOrDefault(s => s.Id == id));

        public Task<bool> UpdateAsync(
            long id, string? title = null, string? whenText = null, string? steps = null, string? donts = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<VictoriaSop>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_sops);
    }

    private sealed class StubCharter : ICharter
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

    private sealed class FlooredStubCharter : ICharter
    {
        private readonly IReadOnlyList<CharterAnchorInfo> _rows;

        public FlooredStubCharter(params CharterAnchorInfo[] rows) => _rows = rows;

        public Task<IReadOnlyList<string>> GetAnchorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(_rows.Select(r => r.Body).ToList());

        public Task<IReadOnlyList<string>> GetAnchorsByKindAsync(
            string kind, bool? lockedOnly = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(
                _rows.Where(r => string.Equals(r.Kind, kind, StringComparison.OrdinalIgnoreCase))
                    .Select(r => r.Body)
                    .ToList());

        public Task<IReadOnlyList<CharterAnchorInfo>> ListAnchorDetailsAsync(
            string? kind = null, string? book = null, CancellationToken cancellationToken = default)
        {
            IEnumerable<CharterAnchorInfo> q = _rows;
            if (!string.IsNullOrWhiteSpace(kind))
                q = q.Where(r => string.Equals(r.Kind, kind, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(book))
                q = q.Where(r => string.Equals(r.Book, book, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<IReadOnlyList<CharterAnchorInfo>>(q.ToList());
        }

        public Task<int> SeedAsync(IReadOnlyList<CharterAnchorSeed> seeds, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class StubEmotionState : IEmotionState
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
