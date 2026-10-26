using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
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
        var preamble = ChatContextBuilder.BuildContextPreamble(
            new[] { "I am Victoria." },
            new[] { "We talked about tea yesterday." },
            "[SoulCore emotion]\nvalence=0.5\n");

        Assert.StartsWith("[Identity]", preamble, StringComparison.Ordinal);
        Assert.Contains("Kayleigh", preamble, StringComparison.Ordinal);
        Assert.Contains("[Memory]", preamble, StringComparison.Ordinal);
        Assert.Contains(MemorySteeringGuidance.Marker, preamble, StringComparison.Ordinal);
        Assert.Contains("[SoulCore emotion]", preamble, StringComparison.Ordinal);
        Assert.True(
            preamble.IndexOf("[Identity]", StringComparison.Ordinal)
            < preamble.IndexOf("[Memory]", StringComparison.Ordinal));
        Assert.True(
            preamble.IndexOf("[Memory]", StringComparison.Ordinal)
            < preamble.IndexOf(MemorySteeringGuidance.Marker, StringComparison.Ordinal));
        Assert.True(
            preamble.IndexOf(MemorySteeringGuidance.Marker, StringComparison.Ordinal)
            < preamble.IndexOf("[SoulCore emotion]", StringComparison.Ordinal));
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
                DesktopTargetWindowTitle = "victoria-sandbox"
            })),
            new LoggerFactory().CreateLogger<ChatContextBuilder>());

        var ctx = await builder.BuildAsync(
            "create a workflow to recall memory",
            useToolLoop: true,
            desktopTargetWindowTitle: "victoria-sandbox",
            CancellationToken.None);

        Assert.Contains("[Tools]", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("workflow_create", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains(MemorySteeringGuidance.Marker, ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("sop_list", ctx.Preamble, StringComparison.Ordinal);
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
                DesktopTargetWindowTitle = "victoria-sandbox"
            })),
            new LoggerFactory().CreateLogger<ChatContextBuilder>(),
            sopStore);

        var ctx = await builder.BuildAsync(
            "log into Maestro and get ready for class",
            useToolLoop: true,
            desktopTargetWindowTitle: "victoria-sandbox",
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
    }

    private sealed class ThrowingCharter : ICharter
    {
        public Task<IReadOnlyList<string>> GetAnchorsAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("charter down");

        public Task<IReadOnlyList<string>> GetAnchorsByKindAsync(
            string kind, bool? lockedOnly = null, CancellationToken cancellationToken = default) =>
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
    }

    private sealed class StubMemoryStoreWithRecall : IMemoryStore
    {
        private readonly string _memory;

        public StubMemoryStoreWithRecall(string memory) => _memory = memory;

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
