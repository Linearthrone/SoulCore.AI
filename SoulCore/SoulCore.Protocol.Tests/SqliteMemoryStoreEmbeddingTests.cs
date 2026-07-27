using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

public class SqliteMemoryStoreEmbeddingTests
{
    [Fact]
    public async Task StoreAndRecallSimilar_RanksByCosine()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-emb-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);

            var idExact = await store.WriteEpisodicAsync("exact match episode about cats", "chat");
            var idNear = await store.WriteEpisodicAsync("near match episode about felines", "chat");
            var idFar = await store.WriteEpisodicAsync("unrelated episode about rockets", "chat");

            float[] exact = [1f, 0f, 0f];
            float[] near = [0.9f, 0.1f, 0f];
            float[] far = [0f, 1f, 0f];

            await store.StoreEmbeddingAsync(idExact, exact, "test-model");
            await store.StoreEmbeddingAsync(idNear, near, "test-model");
            await store.StoreEmbeddingAsync(idFar, far, "test-model");

            var hits = await store.RecallSimilarAsync([1f, 0f, 0f], limit: 2);

            Assert.Equal(2, hits.Count);
            Assert.Contains("cats", hits[0]);
            Assert.Contains("felines", hits[1]);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task RecallSimilar_EmptyStore_ReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-emb-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var hits = await store.RecallSimilarAsync([1f, 0f], limit: 5);
            Assert.Empty(hits);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task WriteEpisodic_ReturnsPositiveId()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-emb-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var id = await store.WriteEpisodicAsync("hello episodic", "system");
            Assert.True(id > 0);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task ListEpisodicsMissingEmbeddings_ExcludesRowsWithVectorsAndQuarantined()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-emb-miss-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);

            var idMissing = await store.WriteEpisodicAsync("missing vector episode", "chat");
            var idFilled = await store.WriteEpisodicAsync("already embedded episode", "chat");
            var idImported = await store.WriteEpisodicAsync("quarantined imported episode", "imported");

            await store.StoreEmbeddingAsync(idFilled, [0.1f, 0.2f, 0.3f], "test-model");

            var missing = await store.ListEpisodicsMissingEmbeddingsAsync(limit: 50);

            Assert.Contains(missing, r => r.Id == idMissing && r.Content.Contains("missing vector"));
            Assert.DoesNotContain(missing, r => r.Id == idFilled);
            Assert.DoesNotContain(missing, r => r.Id == idImported);

            // Idempotent: after fill, list is empty for that id.
            await store.StoreEmbeddingAsync(idMissing, [0.5f, 0.5f, 0f], "test-model");
            var after = await store.ListEpisodicsMissingEmbeddingsAsync(limit: 50);
            Assert.DoesNotContain(after, r => r.Id == idMissing);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task ListEpisodicsMissingEmbeddings_RespectsLimit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-emb-lim-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            for (var i = 0; i < 5; i++)
                await store.WriteEpisodicAsync($"episode {i}", "chat");

            var missing = await store.ListEpisodicsMissingEmbeddingsAsync(limit: 2);
            Assert.Equal(2, missing.Count);
            Assert.True(missing[0].Id < missing[1].Id);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }
}
