using System.Globalization;
using Microsoft.Data.Sqlite;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Sqlite;

namespace SoulCore.Core.Charter;

/// <summary>
/// SQLite-backed <see cref="ICharter"/> implementation. Reads and seeds the
/// <c>charter_anchors</c> table in the same LocalAppData database file as
/// <c>SqliteMemoryStore</c>. Host wires this as a singleton on
/// <c>memoryOptions.ResolveDbPath()</c>; DDL is owned by Memory migrations —
/// <see cref="EnsureSchema"/> is an idempotent fallback for test-only DBs.
/// All command paths serialize through the path-keyed <see cref="SqlitePathGate"/>
/// shared with the memory store (no ungated dual-writer races).
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
    private readonly SemaphoreSlim _dbGate;
    private bool _disposed;

    /// <summary>
    /// Opens a read/write connection at <paramref name="dbPath"/> (same file as memory).
    /// Host registers one singleton per resolved memory path; tests may pass a temp path.
    /// </summary>
    public CharterService(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("dbPath must be non-empty.", nameof(dbPath));

        DatabasePath = SqlitePathGate.NormalizePath(dbPath);
        _dbGate = SqlitePathGate.ForPath(DatabasePath);

        var dir = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = SqlitePathGate.DefaultBusyTimeoutMs / 1000
        }.ToString());

        _dbGate.Wait();
        try
        {
            _connection.Open();
            ApplyBusyTimeout();
            EnsureSchema();
        }
        finally
        {
            _dbGate.Release();
        }
    }

    /// <summary>Normalized absolute path to the shared SQLite file.</summary>
    public string DatabasePath { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAnchorsAsync(CancellationToken cancellationToken = default)
    {
        return await RunDbAsync(async ct =>
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT body FROM charter_anchors
                ORDER BY priority ASC, id ASC;
                """;

            var list = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                list.Add(reader.GetString(0));

            return (IReadOnlyList<string>)list;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns total anchor count and how many are locked (<c>is_locked=1</c>).
    /// Used by Host <c>/health</c> for Presence charter status.
    /// </summary>
    public async Task<(int Total, int Locked)> GetLockCountsAsync(CancellationToken cancellationToken = default)
    {
        return await RunDbAsync(async ct =>
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT COUNT(*), COALESCE(SUM(CASE WHEN is_locked = 1 THEN 1 ELSE 0 END), 0)
                FROM charter_anchors;
                """;
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                return (0, 0);
            return (reader.GetInt32(0), reader.GetInt32(1));
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns structured anchor rows (id/kind/title/body/…) ordered by priority.
    /// Optional <paramref name="kind"/> filter (identity / safety / value / boundary / ritual).
    /// Used by Host <c>GET /settings/identity</c> for the FED Identity tab.
    /// </summary>
    public async Task<IReadOnlyList<CharterAnchorInfo>> ListAnchorDetailsAsync(
        string? kind = null,
        CancellationToken cancellationToken = default)
    {
        return await RunDbAsync(async ct =>
        {
            await using var cmd = _connection.CreateCommand();
            if (string.IsNullOrWhiteSpace(kind))
            {
                cmd.CommandText =
                    """
                    SELECT id, kind, title, body, priority, is_locked, source
                    FROM charter_anchors
                    ORDER BY priority ASC, id ASC;
                    """;
            }
            else
            {
                ValidateKind(kind);
                cmd.CommandText =
                    """
                    SELECT id, kind, title, body, priority, is_locked, source
                    FROM charter_anchors
                    WHERE kind = $kind
                    ORDER BY priority ASC, id ASC;
                    """;
                cmd.Parameters.AddWithValue("$kind", NormalizeKind(kind));
            }

            var list = new List<CharterAnchorInfo>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new CharterAnchorInfo(
                    Id: reader.GetInt64(0),
                    Kind: reader.GetString(1),
                    Title: reader.GetString(2),
                    Body: reader.GetString(3),
                    Priority: reader.GetInt32(4),
                    IsLocked: reader.GetInt32(5) == 1,
                    Source: reader.GetString(6)));
            }

            return (IReadOnlyList<CharterAnchorInfo>)list;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAnchorsByKindAsync(
        string kind,
        bool? lockedOnly = null,
        CancellationToken cancellationToken = default)
    {
        ValidateKind(kind);

        return await RunDbAsync(async ct =>
        {
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
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                list.Add(reader.GetString(0));

            return (IReadOnlyList<string>)list;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SeedAsync(
        IReadOnlyList<CharterAnchorSeed> seeds,
        CancellationToken cancellationToken = default)
    {
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

        return await RunDbAsync(async ct =>
        {
            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var inserted = 0;

            await using var tx = await _connection.BeginTransactionAsync(ct).ConfigureAwait(false);
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

                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    inserted++;
                }

                await tx.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }

            return inserted;
        }, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _dbGate.Wait();
        try
        {
            if (_disposed) return;
            _connection.Dispose();
            _disposed = true;
        }
        finally
        {
            _dbGate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void ApplyBusyTimeout()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"PRAGMA busy_timeout = {SqlitePathGate.DefaultBusyTimeoutMs};";
        cmd.ExecuteNonQuery();
    }

    private async Task RunDbAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _dbGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await work(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _dbGate.Release();
        }
    }

    private async Task<T> RunDbAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _dbGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return await work(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _dbGate.Release();
        }
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
