using SoulCore.Core.Abstractions;
using SoulCore.Core.Charter;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

public class SqlitePathGateConcurrencyTests
{
    [Fact]
    public async Task MemoryAndCharter_ConcurrentOps_OnSamePath_DoNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-gate-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            await using var charter = new CharterService(path);

            var seeds = new[]
            {
                new CharterAnchorSeed("identity", "Name", "I am Victoria.", 10, true)
            };

            var work = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
            {
                if (i % 2 == 0)
                    await store.WriteEpisodicAsync($"episode-{i}", "chat");
                else
                    _ = await charter.GetAnchorsAsync();
            })).ToArray();

            await Task.WhenAll(work);
            await charter.SeedAsync(seeds);

            var anchors = await charter.GetAnchorsAsync();
            Assert.Single(anchors);
            Assert.True(await store.CountEpisodicAsync() >= 10);
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
}
