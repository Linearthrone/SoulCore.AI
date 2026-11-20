using Microsoft.Data.Sqlite;
using SoulCore.Memory;
using SoulCore.Memory.Repositories;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// PROP-18.6: migration 015 activities log schema + indexes + rollback notes.
/// </summary>
public class Prop186ActivitiesLogMigrationTests
{
    [Fact]
    public async Task Migration015_CreatesTable_Indexes_AndLedgerRow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-actlog-015-{Guid.NewGuid():N}.db");
        try
        {
            await using (var store = new SqliteMemoryStore(path))
            {
                Assert.True(store.IsDatabaseOpen);
            }

            await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path
            }.ToString());
            await conn.OpenAsync();

            await using (var ledger = conn.CreateCommand())
            {
                ledger.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version = '015';";
                var count = Convert.ToInt64(await ledger.ExecuteScalarAsync());
                Assert.Equal(1, count);
            }

            await using (var cols = conn.CreateCommand())
            {
                cols.CommandText = "PRAGMA table_info(victoria_activities_log);";
                var names = new List<string>();
                await using var reader = await cols.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    names.Add(reader.GetString(1));

                Assert.Contains("entry_no", names);
                Assert.Contains("occurred_at", names);
                Assert.Contains("activity", names);
                Assert.Contains("subject", names);
                Assert.Contains("session_desc", names);
                Assert.Contains("journal_book_id", names);
                Assert.Contains("journal_entry_id", names);
                Assert.Contains("created_at", names);
            }

            await using (var idx = conn.CreateCommand())
            {
                idx.CommandText =
                    """
                    SELECT name FROM sqlite_master
                    WHERE type = 'index' AND tbl_name = 'victoria_activities_log'
                    ORDER BY name;
                    """;
                var names = new List<string>();
                await using var reader = await idx.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    names.Add(reader.GetString(0));

                Assert.Contains("idx_activities_occurred", names);
                Assert.Contains("idx_activities_subject_occurred", names);
            }

            // Research papers remain a separate table (boundary check).
            await using (var research = conn.CreateCommand())
            {
                research.CommandText =
                    """
                    SELECT COUNT(*) FROM sqlite_master
                    WHERE type = 'table' AND name = 'victoria_research_papers';
                    """;
                var count = Convert.ToInt64(await research.ExecuteScalarAsync());
                Assert.Equal(1, count);
            }
        }
        finally
        {
            TryDeleteDb(path);
        }
    }

    [Fact]
    public async Task Migration015_Store_AppendAndList_RecentFirst_AndSubjectFilter()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-actlog-store-{Guid.NewGuid():N}.db");
        try
        {
            await using var session = new SqliteMemorySession(path);
            IVictoriaActivitiesLogStore log = new SqliteVictoriaActivitiesLogRepository(session);

            var older = await log.AppendAsync(
                activity: "research",
                subject: "optics",
                sessionDesc: "Skimmed a 2024 review on coherent backscatter; noted one open question on phase noise.",
                occurredAt: "2026-11-18T10:00:00.000Z");
            var newer = await log.AppendAsync(
                activity: "leisure",
                subject: "optics",
                sessionDesc: "Re-read the phase-noise paragraph and wrote two sentences into the thoughts journal.",
                occurredAt: "2026-11-19T09:00:00.000Z",
                journalBookId: "thoughts");
            var other = await log.AppendAsync(
                activity: "parked-work",
                subject: "desk-layout",
                sessionDesc: "Moved the Presence strip mock one column left; left a park note for FED.",
                occurredAt: "2026-11-19T08:00:00.000Z");

            Assert.True(older > 0);
            Assert.True(newer > older);
            Assert.True(other > newer);

            var all = await log.ListRecentAsync(limit: 10);
            Assert.Equal(3, all.Count);
            Assert.Equal(newer, all[0].EntryNo);
            Assert.Equal(other, all[1].EntryNo);
            Assert.Equal(older, all[2].EntryNo);

            var optics = await log.ListRecentAsync(subject: "optics", limit: 10);
            Assert.Equal(2, optics.Count);
            Assert.Equal(newer, optics[0].EntryNo);
            Assert.Equal(older, optics[1].EntryNo);
            Assert.Equal("thoughts", optics[0].JournalBookId);

            var got = await log.GetAsync(newer);
            Assert.NotNull(got);
            Assert.Equal("leisure", got!.Activity);
            Assert.Contains("phase-noise", got.SessionDesc, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDb(path);
        }
    }

    [Fact]
    public async Task Migration015_RejectsEmptyDesc_ViaCheckConstraint()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-actlog-check-{Guid.NewGuid():N}.db");
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
                INSERT INTO victoria_activities_log
                    (occurred_at, activity, subject, session_desc)
                VALUES
                    ('2026-11-19T00:00:00.000Z', 'research', 'optics', '   ');
                """;
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDb(path);
        }
    }

    [Fact]
    public async Task Migration015_RollbackScript_DropsTableAndLedger_PreservesResearch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-actlog-rb-{Guid.NewGuid():N}.db");
        try
        {
            await using (var store = new SqliteMemoryStore(path))
            {
                Assert.True(store.IsDatabaseOpen);
            }

            // bin/Debug/net8.0 → SoulCore/ → SoulCore.Memory/Migrations/
            var rollbackPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "SoulCore.Memory",
                "Migrations",
                "015_victoria_activities_log_rollback.sql"));
            Assert.True(File.Exists(rollbackPath), $"missing rollback script at {rollbackPath}");
            var rollbackSql = await File.ReadAllTextAsync(rollbackPath);

            await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path
            }.ToString());
            await conn.OpenAsync();

            await using (var seed = conn.CreateCommand())
            {
                seed.CommandText =
                    """
                    INSERT INTO victoria_activities_log
                        (occurred_at, activity, subject, session_desc)
                    VALUES
                        ('2026-11-19T00:00:00.000Z', 'research', 'optics',
                         'Probe row before rollback.');
                    INSERT OR IGNORE INTO victoria_research_subjects (slug, title)
                    VALUES ('optics', 'Optics');
                    """;
                await seed.ExecuteNonQueryAsync();
            }

            await using (var rb = conn.CreateCommand())
            {
                rb.CommandText = rollbackSql;
                await rb.ExecuteNonQueryAsync();
            }

            await using (var gone = conn.CreateCommand())
            {
                gone.CommandText =
                    """
                    SELECT COUNT(*) FROM sqlite_master
                    WHERE type = 'table' AND name = 'victoria_activities_log';
                    """;
                Assert.Equal(0L, Convert.ToInt64(await gone.ExecuteScalarAsync()));
            }

            await using (var ledger = conn.CreateCommand())
            {
                ledger.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version = '015';";
                Assert.Equal(0L, Convert.ToInt64(await ledger.ExecuteScalarAsync()));
            }

            await using (var research = conn.CreateCommand())
            {
                research.CommandText = "SELECT COUNT(*) FROM victoria_research_subjects WHERE slug = 'optics';";
                Assert.Equal(1L, Convert.ToInt64(await research.ExecuteScalarAsync()));
            }

            await using (var journals = conn.CreateCommand())
            {
                journals.CommandText = "SELECT COUNT(*) FROM victoria_journal_books;";
                Assert.True(Convert.ToInt64(await journals.ExecuteScalarAsync()) >= 1);
            }
        }
        finally
        {
            TryDeleteDb(path);
        }
    }

    private static void TryDeleteDb(string path)
    {
        try { File.Delete(path); } catch { /* best-effort */ }
        try { File.Delete(path + "-wal"); } catch { }
        try { File.Delete(path + "-shm"); } catch { }
    }
}
