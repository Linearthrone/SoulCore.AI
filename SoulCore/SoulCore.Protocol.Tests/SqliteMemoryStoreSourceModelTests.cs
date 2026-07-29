using Microsoft.Data.Sqlite;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// TASK-157 / ISSUE-002 — episodic source='model' must pass CHECK + AllowedSources.
/// </summary>
public class SqliteMemoryStoreSourceModelTests
{
    [Fact]
    public async Task WriteEpisodicAsync_SourceModel_PersistsLiterally()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-src-model-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            var id = await store.WriteEpisodicAsync("store_memory style note from model", "model");

            Assert.True(id > 0);
            Assert.Equal("model", await ReadSourceAsync(path, id));
            Assert.True(await IsMigrationAppliedAsync(path, "003"));
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task WriteEpisodicAsync_LegacySources_StillAccepted()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-src-legacy-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            string[] sources = ["self", "chat", "imported", "observation", "correction", "system"];
            foreach (var source in sources)
            {
                var id = await store.WriteEpisodicAsync($"episode via {source}", source);
                Assert.Equal(source, await ReadSourceAsync(path, id));
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task Migration003_UpgradesPreModelCheck_ThenAcceptsModel()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-src-mig003-{Guid.NewGuid():N}.db");
        try
        {
            // Simulate a pre-003 DB: CHECK without 'model', ledger at 001+002 only.
            await using (var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString()))
            {
                await conn.OpenAsync();
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        """
                        PRAGMA foreign_keys = ON;
                        CREATE TABLE schema_migrations (
                            version TEXT NOT NULL PRIMARY KEY,
                            name TEXT NOT NULL,
                            applied_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                        );
                        CREATE TABLE episodic_memories (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            content TEXT NOT NULL,
                            occurred_at TEXT NOT NULL,
                            created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                            updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                            source TEXT NOT NULL
                                CHECK (source IN (
                                    'self', 'chat', 'imported', 'observation', 'correction', 'system'
                                )),
                            source_ref TEXT NULL,
                            labels_json TEXT NOT NULL DEFAULT '[]',
                            importance REAL NOT NULL DEFAULT 0.5,
                            is_quarantined INTEGER NOT NULL DEFAULT 0,
                            embedding_id INTEGER NULL,
                            CONSTRAINT episodic_content_nonempty CHECK (length(trim(content)) > 0)
                        );
                        CREATE TABLE episodic_embedding_vectors (
                            episodic_id INTEGER PRIMARY KEY
                                REFERENCES episodic_memories (id) ON DELETE CASCADE,
                            model TEXT NOT NULL DEFAULT 'nomic-embed-text',
                            dims INTEGER NOT NULL,
                            vector BLOB NOT NULL,
                            created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                        );
                        INSERT INTO schema_migrations (version, name) VALUES ('001', 'initial');
                        INSERT INTO schema_migrations (version, name) VALUES ('002', 'embedding_vectors');
                        INSERT INTO episodic_memories (content, occurred_at, source)
                        VALUES ('pre-migration chat row', '2026-07-26T00:00:00.000Z', 'chat');
                        """;
                    await cmd.ExecuteNonQueryAsync();
                }

                // Prove pre-003 CHECK rejects 'model' at the SQL layer.
                await using (var bad = conn.CreateCommand())
                {
                    bad.CommandText =
                        """
                        INSERT INTO episodic_memories (content, occurred_at, source)
                        VALUES ('should fail', '2026-07-26T00:00:00.000Z', 'model');
                        """;
                    await Assert.ThrowsAsync<SqliteException>(async () =>
                        await bad.ExecuteNonQueryAsync());
                }
            }

            await using var store = new SqliteMemoryStore(path);
            Assert.True(await IsMigrationAppliedAsync(path, "003"));

            var modelId = await store.WriteEpisodicAsync("post-migration model row", "model");
            Assert.Equal("model", await ReadSourceAsync(path, modelId));

            // Existing 'chat' row preserved (no rewrite to 'model').
            await using var verify = new SqliteConnection($"Data Source={path}");
            await verify.OpenAsync();
            await using var countCmd = verify.CreateCommand();
            countCmd.CommandText =
                "SELECT COUNT(*) FROM episodic_memories WHERE source = 'chat' AND content LIKE 'pre-migration%';";
            var chatCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
            Assert.Equal(1, chatCount);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    private static async Task<string> ReadSourceAsync(string dbPath, long id)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT source FROM episodic_memories WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        var result = await cmd.ExecuteScalarAsync();
        Assert.NotNull(result);
        return Convert.ToString(result)!;
    }

    private static async Task<bool> IsMigrationAppliedAsync(string dbPath, string version)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM schema_migrations WHERE version = $v LIMIT 1;";
        cmd.Parameters.AddWithValue("$v", version);
        var result = await cmd.ExecuteScalarAsync();
        return result is not null and not DBNull;
    }
}
