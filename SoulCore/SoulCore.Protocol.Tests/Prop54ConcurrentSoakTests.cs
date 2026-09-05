using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Charter;
using SoulCore.Core.Safety;
using SoulCore.Host.Loop;
using SoulCore.Memory;
using System.Collections.Concurrent;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// PROP-5.4 QA soak: overlapping memory + charter + SoulLoop on one SQLite path.
/// </summary>
public class Prop54ConcurrentSoakTests
{
    private const int SoakRounds = 40;
    private const int Parallelism = 12;

    [Fact]
    public async Task MemoryCharterAndSoulLoop_ConcurrentSoak_NoSqliteConcurrencyErrors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-prop54-{Guid.NewGuid():N}.db");
        var errors = new ConcurrentBag<Exception>();

        try
        {
            await using var store = new SqliteMemoryStore(path);
            await using var charter = new CharterService(path);

            await charter.SeedAsync(new[]
            {
                new CharterAnchorSeed("identity", "Name", "I am Victoria.", 10, true)
            });

            var hub = new PresenceWsHub(NullLogger<PresenceWsHub>.Instance);
            var loop = new SoulLoopScaffold(
                store,
                store,
                hub,
                Options.Create(new SoulLoopOptions
                {
                    Enabled = true,
                    EpisodicRecallLimit = 3,
                    ReflectionIntervalTicks = 0,
                    ProactiveChatEnabled = false
                }),
                new DriftWatcher(sloMinutes: 15),
                NullLogger<SoulLoopScaffold>.Instance);

            var soak = Enumerable.Range(0, SoakRounds * Parallelism).Select(i => Task.Run(async () =>
            {
                try
                {
                    switch (i % 6)
                    {
                        case 0:
                            await store.WriteEpisodicAsync($"chat-{i}", "chat");
                            break;
                        case 1:
                            await store.WriteEpisodicAsync($"observation-{i}", "observation");
                            break;
                        case 2:
                            _ = await charter.GetAnchorsAsync();
                            break;
                        case 3:
                            _ = await charter.GetLockCountsAsync();
                            break;
                        case 4:
                            _ = await store.RecallRecentAsync(3);
                            break;
                        default:
                            await loop.TickAsync();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            })).ToArray();

            await Task.WhenAll(soak);

            Assert.Empty(errors);
            Assert.True(await store.CountEpisodicAsync() >= SoakRounds);
            _ = await charter.GetAnchorsAsync();
        }
        finally
        {
            TryDeleteDb(path);
        }
    }

    [Fact]
    public async Task SoulLoop_OverlappingTicksDuringDbStorm_SingleFlightSkipsWithoutSqliteErrors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-prop54-tick-{Guid.NewGuid():N}.db");
        var errors = new ConcurrentBag<Exception>();
        var emotion = new SlowEmotionStub(delayMs: 150);

        try
        {
            await using var store = new SqliteMemoryStore(path);
            await using var charter = new CharterService(path);

            var hub = new PresenceWsHub(NullLogger<PresenceWsHub>.Instance);
            var loop = new SoulLoopScaffold(
                emotion,
                store,
                hub,
                Options.Create(new SoulLoopOptions
                {
                    Enabled = true,
                    EpisodicRecallLimit = 2,
                    ReflectionIntervalTicks = 0,
                    ProactiveChatEnabled = false
                }),
                new DriftWatcher(sloMinutes: 15),
                NullLogger<SoulLoopScaffold>.Instance);

            var tickStorm = Task.Run(async () =>
            {
                var ticks = Enumerable.Range(0, 24)
                    .Select(_ => loop.TickAsync())
                    .ToArray();
                try
                {
                    await Task.WhenAll(ticks);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            });

            var dbStorm = Task.Run(async () =>
            {
                var ops = Enumerable.Range(0, 80).Select(async i =>
                {
                    try
                    {
                        if (i % 3 == 0)
                            await store.WriteEpisodicAsync($"storm-{i}", "chat");
                        else if (i % 3 == 1)
                            _ = await charter.GetLockCountsAsync();
                        else
                            _ = await store.RecallRecentAsync(2);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                });
                await Task.WhenAll(ops);
            });

            await Task.WhenAll(tickStorm, dbStorm);

            Assert.Empty(errors);
            Assert.InRange(emotion.GetCallCount, 1, 4);
        }
        finally
        {
            TryDeleteDb(path);
        }
    }

    private static void TryDeleteDb(string path)
    {
        try { File.Delete(path); } catch { /* best-effort */ }
        try { File.Delete(path + "-journal"); } catch { }
        try { File.Delete(path + "-wal"); } catch { }
        try { File.Delete(path + "-shm"); } catch { }
    }

    private sealed class SlowEmotionStub : IEmotionState
    {
        private int _getCallCount;
        private readonly int _delayMs;

        public SlowEmotionStub(int delayMs) => _delayMs = delayMs;

        public int GetCallCount => _getCallCount;

        public async Task<IReadOnlyDictionary<string, double>> GetAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _getCallCount);
            await Task.Delay(_delayMs, cancellationToken);
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
}
