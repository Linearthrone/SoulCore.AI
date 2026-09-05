using Microsoft.Extensions.Logging;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Host.Ws;
using SoulCore.Inference;
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
        Assert.Contains("[Memory]", preamble, StringComparison.Ordinal);
        Assert.Contains("[SoulCore emotion]", preamble, StringComparison.Ordinal);
        Assert.True(
            preamble.IndexOf("[Identity]", StringComparison.Ordinal)
            < preamble.IndexOf("[Memory]", StringComparison.Ordinal));
        Assert.True(
            preamble.IndexOf("[Memory]", StringComparison.Ordinal)
            < preamble.IndexOf("[SoulCore emotion]", StringComparison.Ordinal));
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
            new LoggerFactory().CreateLogger<ChatContextBuilder>());

        var ctx = await builder.BuildAsync(
            "create a workflow to recall memory",
            useToolLoop: true,
            desktopTargetWindowTitle: "victoria-sandbox",
            CancellationToken.None);

        Assert.Contains("[Tools]", ctx.Preamble, StringComparison.Ordinal);
        Assert.Contains("workflow_create", ctx.Preamble, StringComparison.Ordinal);
        Assert.NotEmpty(ctx.EmotionPreamble);
    }

    [Fact]
    public async Task BuildAsync_ContinuesWhenCharterFails()
    {
        var builder = new ChatContextBuilder(
            new StubMemoryStore(),
            new NullEmbeddingClient(),
            new ThrowingCharter(),
            new StubEmotionState(),
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
