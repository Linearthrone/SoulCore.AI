using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Safety;
using SoulCore.Host.Loop;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

public class SoulLoopScaffoldSingleFlightTests
{
    [Fact]
    public async Task TickAsync_OverlappingCallers_OnlyOneExecutesBody()
    {
        var emotion = new DelayedEmotionStub(delayMs: 200);
        var memory = new CountingMemoryStub();
        var hub = new PresenceWsHub(NullLogger<PresenceWsHub>.Instance);
        var options = Options.Create(new SoulLoopOptions
        {
            Enabled = true,
            EpisodicRecallLimit = 1,
            ReflectionIntervalTicks = 0,
            ProactiveChatEnabled = false
        });
        var loop = new SoulLoopScaffold(
            emotion,
            memory,
            hub,
            options,
            new DriftWatcher(sloMinutes: 15),
            NullLogger<SoulLoopScaffold>.Instance);

        var first = loop.TickAsync();
        await Task.Delay(25);
        var second = loop.TickAsync();

        await Task.WhenAll(first, second);

        Assert.Equal(1, emotion.GetCallCount);
        Assert.Equal(1, memory.RecallCallCount);
        Assert.NotNull(loop.LastWant);
    }

    [Fact]
    public async Task TickAsync_ParallelInvokes_TickCounterNotLost()
    {
        var emotion = new DelayedEmotionStub(delayMs: 50);
        var memory = new CountingMemoryStub();
        var hub = new PresenceWsHub(NullLogger<PresenceWsHub>.Instance);
        var options = Options.Create(new SoulLoopOptions
        {
            Enabled = true,
            EpisodicRecallLimit = 0,
            ReflectionIntervalTicks = 1,
            ProactiveChatEnabled = false
        });
        var loop = new SoulLoopScaffold(
            emotion,
            memory,
            hub,
            options,
            new DriftWatcher(sloMinutes: 15),
            NullLogger<SoulLoopScaffold>.Instance);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => loop.TickAsync())
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.InRange(memory.WriteCallCount, 0, 1);
        Assert.InRange(emotion.GetCallCount, 0, 1);
    }

    private sealed class DelayedEmotionStub : IEmotionState
    {
        private int _getCallCount;
        private readonly int _delayMs;

        public DelayedEmotionStub(int delayMs) => _delayMs = delayMs;

        public int GetCallCount => _getCallCount;

        public async Task<IReadOnlyDictionary<string, double>> GetAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _getCallCount);
            await Task.Delay(_delayMs, cancellationToken).ConfigureAwait(false);
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["valence"] = 0.1,
                ["arousal"] = 0.2,
                ["dominance"] = 0.4
            };
        }

        public Task SetAsync(IReadOnlyDictionary<string, double> components, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0L);
    }

    private sealed class CountingMemoryStub : IMemoryStore
    {
        public int RecallCallCount;
        public int WriteCallCount;

        public bool IsDatabaseOpen => true;

        public string DatabasePath => ":memory:";

        public Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref WriteCallCount);
            return Task.FromResult(1L);
        }

        public Task StoreEmbeddingAsync(long episodicId, float[] vector, string model, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<(long Id, string Content)>>(Array.Empty<(long, string)>());

        public Task<IReadOnlyList<string>> RecallSimilarAsync(float[] queryVector, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref RecallCallCount);
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }
}
