using System.Globalization;
using Microsoft.Data.Sqlite;
using SoulCore.Core.Abstractions;

namespace SoulCore.Core.Charter;

/// <summary>
/// SQLite-backed <see cref="ICharter"/> implementation. Reads and seeds the
/// <c>charter_anchors</c> table. Opens an independent read/write connection — does
/// NOT touch the live Host DB or <c>SqliteMemoryStore</c>. Intended for
/// test/staging use; no DI wiring in Program.cs.
/// </summary>
public sealed class CharterService : ICharter, IAsyncDisposable, IDisposable
{
    private static readonly HashSet<string> ValidKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "identity", "safety", "value", "boundary", "ritual"
    };

    private static readonly HashSet<string> ValidSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "seed", "imported", "calibration", "system"
    };

    private readonly SqliteConnection _connection;
    private readonly object _gate = new();
    private bool _disposed;

    /// <summary>
    /// Opens (or creates) a SQLite DB at <paramref name="dbPath"/> and ensures the
    /// <c>charter_anchors</c> table exists. The caller owns the file lifecycle —
    /// pass a temp path for tests.
    /// </summary>
    public CharterService(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("dbPath must be non-empty.", nameof(dbPath));

        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString());

        _connection.Open();
        EnsureSchema();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAnchorsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT body FROM charter_anchors
            ORDER BY priority ASC, id ASC;
            """;

        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(reader.GetString(0));

        return list;
    }

    /// <summary>
    /// Returns total anchor count and how many are locked (<c>is_locked=1</c>).
    /// Used by Host <c>/health</c> for Presence charter status.
    /// </summary>
    public async Task<(int Total, int Locked)> GetLockCountsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT COUNT(*), COALESCE(SUM(CASE WHEN is_locked = 1 THEN 1 ELSE 0 END), 0)
            FROM charter_anchors;
            """;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return (0, 0);
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAnchorsByKindAsync(
        string kind,
        bool? lockedOnly = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateKind(kind);

        await using var cmd = _connection.CreateCommand();
        if (lockedOnly is null)
        {
            cmd.CommandText =
                """
                SELECT body FROM charter_anchors
                WHERE kind = $kind
                ORDER BY priority ASC, id ASC;
                """;
        }
        else
        {
            cmd.CommandText =
                """
                SELECT body FROM charter_anchors
                WHERE kind = $kind AND is_locked = $locked
                ORDER BY priority ASC, id ASC;
                """;
            cmd.Parameters.AddWithValue("$locked", lockedOnly.Value ? 1 : 0);
        }
        cmd.Parameters.AddWithValue("$kind", NormalizeKind(kind));

        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(reader.GetString(0));

        return list;
    }

    /// <inheritdoc />
    public async Task<int> SeedAsync(
        IReadOnlyList<CharterAnchorSeed> seeds,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(seeds);

        if (seeds.Count == 0)
            return 0;

        foreach (var seed in seeds)
        {
            ValidateKind(seed.Kind);
            ValidateSource(seed.Source);
            if (string.IsNullOrWhiteSpace(seed.Title))
                throw new ArgumentException($"Seed title must be non-empty (kind={seed.Kind}).", nameof(seeds));
            if (string.IsNullOrWhiteSpace(seed.Body))
                throw new ArgumentException($"Seed body must be non-empty (kind={seed.Kind}).", nameof(seeds));
        }

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var inserted = 0;

        await using var tx = await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var seed in seeds)
            {
                await using var cmd = _connection.CreateCommand();
                cmd.Transaction = (SqliteTransaction)tx;
                cmd.CommandText =
                    """
                    INSERT INTO charter_anchors (kind, title, body, priority, is_locked, source, created_at, updated_at)
                    VALUES ($kind, $title, $body, $priority, $locked, $source, $created_at, $updated_at);
                    """;
                cmd.Parameters.AddWithValue("$kind", NormalizeKind(seed.Kind));
                cmd.Parameters.AddWithValue("$title", seed.Title.Trim());
                cmd.Parameters.AddWithValue("$body", seed.Body.Trim());
                cmd.Parameters.AddWithValue("$priority", seed.Priority);
                cmd.Parameters.AddWithValue("$locked", seed.IsLocked ? 1 : 0);
                cmd.Parameters.AddWithValue("$source", NormalizeSource(seed.Source));
                cmd.Parameters.AddWithValue("$created_at", now);
                cmd.Parameters.AddWithValue("$updated_at", now);

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                inserted++;
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return inserted;
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_gate)
        {
            if (_disposed) return;
            _connection.Dispose();
            _disposed = true;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void EnsureSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS charter_anchors (
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

            CREATE INDEX IF NOT EXISTS idx_charter_kind_priority
                ON charter_anchors (kind, priority ASC);

            CREATE INDEX IF NOT EXISTS idx_charter_locked
                ON charter_anchors (is_locked)
                WHERE is_locked = 1;
            """;
        cmd.ExecuteNonQuery();
    }

    private static void ValidateKind(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind) || !ValidKinds.Contains(kind))
            throw new ArgumentException(
                $"kind must be one of: {string.Join(", ", ValidKinds)}. Got: '{kind}'.",
                nameof(kind));
    }

    private static void ValidateSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || !ValidSources.Contains(source))
            throw new ArgumentException(
                $"source must be one of: {string.Join(", ", ValidSources)}. Got: '{source}'.",
                nameof(source));
    }

    private static string NormalizeKind(string kind)
        => kind.Trim().ToLowerInvariant();

    private static string NormalizeSource(string source)
        => source.Trim().ToLowerInvariant();
}
