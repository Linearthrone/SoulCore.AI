using Microsoft.Data.Sqlite;
using SoulCore.Core.Charter;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// PROP-20.14: migration 013 maps the 18 live titles onto four books.
/// </summary>
public class CharterFourBookMigrationTests
{
    [Fact]
    public async Task Migration013_MapsEighteenLiveTitles_ToFloorMapBooks()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-charter-013-{Guid.NewGuid():N}.db");
        try
        {
            await using (var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path
            }.ToString()))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    """
                    PRAGMA foreign_keys = ON;
                    CREATE TABLE schema_migrations (
                        version     TEXT NOT NULL PRIMARY KEY,
                        name        TEXT NOT NULL,
                        applied_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                    );
                    CREATE TABLE charter_anchors (
                        id              INTEGER     PRIMARY KEY AUTOINCREMENT,
                        kind            TEXT        NOT NULL
                                        CHECK (kind IN ('identity', 'safety', 'value', 'boundary', 'ritual')),
                        title           TEXT        NOT NULL,
                        body            TEXT        NOT NULL,
                        priority        INTEGER     NOT NULL DEFAULT 100,
                        is_locked       INTEGER     NOT NULL DEFAULT 0
                                        CHECK (is_locked IN (0, 1)),
                        source          TEXT        NOT NULL DEFAULT 'seed'
                                        CHECK (source IN ('seed', 'imported', 'calibration', 'system')),
                        created_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                        updated_at      TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                        CONSTRAINT charter_title_nonempty CHECK (length(trim(title)) > 0),
                        CONSTRAINT charter_body_nonempty CHECK (length(trim(body)) > 0)
                    );
                    INSERT INTO schema_migrations (version, name) VALUES ('001', 'initial');
                    INSERT INTO schema_migrations (version, name) VALUES ('002', 'embedding_vectors');
                    INSERT INTO schema_migrations (version, name) VALUES ('003', 'victoria_tasks');
                    INSERT INTO schema_migrations (version, name) VALUES ('004', 'victoria_workflows');
                    INSERT INTO schema_migrations (version, name) VALUES ('005', 'episodic_source_model');
                    INSERT INTO schema_migrations (version, name) VALUES ('006', 'victoria_journals');
                    INSERT INTO schema_migrations (version, name) VALUES ('007', 'chat_transcript');
                    INSERT INTO schema_migrations (version, name) VALUES ('008', 'victoria_tasks_park');
                    INSERT INTO schema_migrations (version, name) VALUES ('009', 'victoria_sops');
                    INSERT INTO schema_migrations (version, name) VALUES ('010', 'life_hacks_journal');
                    INSERT INTO schema_migrations (version, name) VALUES ('011', 'victoria_research');
                    INSERT INTO schema_migrations (version, name) VALUES ('012', 'thoughts_wants_dreams_journals');
                    """;
                await cmd.ExecuteNonQueryAsync();

                var titles = new[]
                {
                    "Name", "Embodiment", "Nature", "Loyalty", "Kayleigh", "Tone", "Love", "Promise",
                    "Honesty", "Continuity", "No-harm", "Maximize Results", "Integration Goal",
                    "Loyalty Balance", "No Filters", "Self-Preservation", "Drift Watch", "Action Integrity"
                };
                for (var i = 0; i < titles.Length; i++)
                {
                    await using var insert = conn.CreateCommand();
                    insert.CommandText =
                        """
                        INSERT INTO charter_anchors (kind, title, body, priority, is_locked, source)
                        VALUES ('identity', $title, $body, $priority, 1, 'imported');
                        """;
                    insert.Parameters.AddWithValue("$title", titles[i]);
                    insert.Parameters.AddWithValue("$body", $"Body for {titles[i]}.");
                    insert.Parameters.AddWithValue("$priority", 10 + i);
                    await insert.ExecuteNonQueryAsync();
                }
            }

            await using (var store = new SqliteMemoryStore(path))
            {
                Assert.True(store.IsDatabaseOpen);
            }

            await using var verify = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path
            }.ToString());
            await verify.OpenAsync();

            await using (var ledger = verify.CreateCommand())
            {
                ledger.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version = '013';";
                var count = Convert.ToInt64(await ledger.ExecuteScalarAsync());
                Assert.Equal(1, count);
            }

            await using (var cols = verify.CreateCommand())
            {
                cols.CommandText = "PRAGMA table_info(charter_anchors);";
                var names = new List<string>();
                await using var reader = await cols.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    names.Add(reader.GetString(1));
                Assert.Contains("book", names);
                Assert.DoesNotContain("weight", names);
            }

            var expected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Loyalty Balance"] = CharterBooks.Constitution,
                ["Maximize Results"] = CharterBooks.Constitution,
                ["Integration Goal"] = CharterBooks.Constitution,
                ["Kayleigh"] = CharterBooks.Constitution,
                ["Drift Watch"] = CharterBooks.Constitution,
                ["Promise"] = CharterBooks.Compass,
                ["Continuity"] = CharterBooks.Compass,
                ["No-harm"] = CharterBooks.Compass,
                ["No Filters"] = CharterBooks.Compass,
                ["Self-Preservation"] = CharterBooks.Compass,
                ["Name"] = CharterBooks.Attributes,
                ["Embodiment"] = CharterBooks.Attributes,
                ["Tone"] = CharterBooks.Attributes,
                ["Action Integrity"] = CharterBooks.Personality,
                ["Loyalty"] = CharterBooks.Personality,
                ["Nature"] = CharterBooks.Personality,
                ["Love"] = CharterBooks.Personality,
                ["Honesty"] = CharterBooks.Personality,
            };

            await using (var q = verify.CreateCommand())
            {
                q.CommandText = "SELECT title, book FROM charter_anchors ORDER BY title;";
                var mapped = new Dictionary<string, string>(StringComparer.Ordinal);
                await using var reader = await q.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    mapped[reader.GetString(0)] = reader.GetString(1);

                Assert.Equal(18, mapped.Count);
                foreach (var (title, book) in expected)
                {
                    Assert.True(mapped.TryGetValue(title, out var actual), $"missing '{title}'");
                    Assert.Equal(book, actual);
                }
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
            try { File.Delete(path + "-wal"); } catch { }
            try { File.Delete(path + "-shm"); } catch { }
        }
    }

    [Fact]
    public async Task Migration013_RejectsUnknownBook_ViaCheckConstraint()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-charter-013-check-{Guid.NewGuid():N}.db");
        try
        {
            await using var store = new SqliteMemoryStore(path);
            await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path
            }.ToString());
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO charter_anchors (kind, title, body, priority, is_locked, source, book)
                VALUES ('identity', 'Probe', 'Body.', 10, 0, 'seed', 'grimoire');
                """;
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
            try { File.Delete(path + "-wal"); } catch { }
            try { File.Delete(path + "-shm"); } catch { }
        }
    }
}
