using Microsoft.Data.Sqlite;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// ISSUE-20260726-002 / TASK-157 — source='model' via migration 005
/// (003/004 reserved by BED-140/141).
/// </summary>
public class SqliteMemoryStoreSourceModelTests
{
    [Fact]
    public async Task WriteEpisodic_SourceModel_PersistsLiterally()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-src-model-{Guid.NewGuid():N}.db");
        try
        {
            long id;
            await using (var store = new SqliteMemoryStore(path))
            {
                id = await store.WriteEpisodicAsync("store_memory authored row", "model");
                Assert.True(id > 0);
            }

            await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path
            }.ToString());
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT source FROM episodic_memories WHERE id = $id;";
                cmd.Parameters.AddWithValue("$id", id);
                var source = (string?)await cmd.ExecuteScalarAsync();
                Assert.Equal("model", source);
            }

            await using (var ledger = conn.CreateCommand())
            {
                ledger.CommandText =
                    "SELECT name FROM schema_migrations WHERE version = '005';";
                var name = (string?)await ledger.ExecuteScalarAsync();
                Assert.Equal("episodic_source_model", name);
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task Migration005_UpgradesPre005Db_PreservesRowsAndAllowsModel()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-mig005-up-{Guid.NewGuid():N}.db");
        try
        {
            // Seed a pre-005 DB: old CHECK (no 'model'), ledger 001+002, one chat row + embedding.
            await using (var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path
            }.ToString()))
            {
                await conn.OpenAsync();
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        """
                        PRAGMA foreign_keys = ON;
                        CREATE TABLE schema_migrations (
                            version     TEXT NOT NULL PRIMARY KEY,
                            name        TEXT NOT NULL,
                            applied_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                        );
                        CREATE TABLE episodic_memories (
                            id              INTEGER PRIMARY KEY AUTOINCREMENT,
                            content         TEXT NOT NULL,
                            occurred_at     TEXT NOT NULL,
                            created_at      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                            updated_at      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                            source          TEXT NOT NULL
                                            CHECK (source IN (
                                                'self', 'chat', 'imported', 'observation', 'correction', 'system'
                                            )),
                            source_ref      TEXT NULL,
                            labels_json     TEXT NOT NULL DEFAULT '[]',
                            importance      REAL NOT NULL DEFAULT 0.5,
                            is_quarantined  INTEGER NOT NULL DEFAULT 0,
                            embedding_id    INTEGER NULL,
                            CONSTRAINT episodic_content_nonempty CHECK (length(trim(content)) > 0)
                        );
                        CREATE TABLE episodic_embedding_vectors (
                            episodic_id     INTEGER PRIMARY KEY
                                            REFERENCES episodic_memories (id) ON DELETE CASCADE,
                            model           TEXT NOT NULL DEFAULT 'nomic-embed-text',
                            dims            INTEGER NOT NULL,
                            vector          BLOB NOT NULL,
                            created_at      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                        );
                        INSERT INTO schema_migrations (version, name) VALUES ('001', 'initial');
                        INSERT INTO schema_migrations (version, name) VALUES ('002', 'embedding_vectors');
                        INSERT INTO episodic_memories (content, occurred_at, source)
                        VALUES ('pre-upgrade chat row', '2026-07-01T00:00:00.000Z', 'chat');
                        INSERT INTO episodic_embedding_vectors (episodic_id, model, dims, vector)
                        VALUES (1, 'test', 1, X'0000803F');
                        """;
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            long modelId;
            await using (var store = new SqliteMemoryStore(path))
            {
                modelId = await store.WriteEpisodicAsync("post-upgrade model row", "model");
                Assert.True(modelId > 1, "AUTOINCREMENT must continue past preserved id=1");
            }

            await using var verify = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path
            }.ToString());
            await verify.OpenAsync();

            await using (var cmd = verify.CreateCommand())
            {
                cmd.CommandText = "SELECT content, source FROM episodic_memories ORDER BY id;";
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("pre-upgrade chat row", reader.GetString(0));
                Assert.Equal("chat", reader.GetString(1));
                Assert.True(await reader.ReadAsync());
                Assert.Equal("post-upgrade model row", reader.GetString(0));
                Assert.Equal("model", reader.GetString(1));
                Assert.False(await reader.ReadAsync());
            }

            await using (var emb = verify.CreateCommand())
            {
                emb.CommandText = "SELECT COUNT(*) FROM episodic_embedding_vectors WHERE episodic_id = 1;";
                var count = Convert.ToInt64(await emb.ExecuteScalarAsync());
                Assert.Equal(1, count);
            }

            await using (var ledger = verify.CreateCommand())
            {
                ledger.CommandText =
                    "SELECT COUNT(*) FROM schema_migrations WHERE version = '005';";
                var count = Convert.ToInt64(await ledger.ExecuteScalarAsync());
                Assert.Equal(1, count);
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task WriteEpisodic_UnknownSource_StillCoercesToSystem()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-src-unk-{Guid.NewGuid():N}.db");
        try
        {
            long id;
            await using (var store = new SqliteMemoryStore(path))
            {
                id = await store.WriteEpisodicAsync("unknown label", "not-a-real-source");
            }

            await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path
            }.ToString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT source FROM episodic_memories WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            Assert.Equal("system", (string?)await cmd.ExecuteScalarAsync());
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }
}
