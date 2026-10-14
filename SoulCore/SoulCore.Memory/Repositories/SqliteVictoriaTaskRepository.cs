using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SoulCore.Memory.Repositories;

public sealed class SqliteVictoriaTaskRepository : IVictoriaTaskStore
{
    public static readonly HashSet<string> AllowedTaskStatuses = VictoriaTaskRules.AllowedStatuses;
    public static readonly HashSet<string> AllowedTaskSurfaces = VictoriaTaskRules.AllowedSurfaces;
    public const string DefaultTaskPriority = "medium";
    /// <summary>PROP-17.1: operator add defaults to suspended unless marked ready.</summary>
    public const string DefaultTaskStatus = "suspended";

    private readonly SqliteMemorySession _session;

    public SqliteVictoriaTaskRepository(SqliteMemorySession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    /// <inheritdoc />
    public Task<long> CreateAsync(
        string title,
        string? description,
        string? priority,
        CancellationToken cancellationToken = default)
        => CreateAsync(title, description, priority, surface: null, ready: false, status: null, notes: null, cancellationToken);

    /// <inheritdoc />
    public async Task<long> CreateAsync(
        string title,
        string? description,
        string? priority,
        string? surface,
        bool ready,
        string? status,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task title must be non-empty.", nameof(title));

        var now = UtcNow();
        var resolvedPriority = string.IsNullOrWhiteSpace(priority)
            ? DefaultTaskPriority
            : priority.Trim().ToLowerInvariant();
        var resolvedDescription = description?.Trim() ?? string.Empty;
        var resolvedNotes = notes?.Trim() ?? string.Empty;
        var resolvedSurface = NormalizeSurface(surface);
        var resolvedStatus = ResolveCreateStatus(status, ready);

        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO victoria_tasks
                    (title, description, status, priority, created_at, updated_at,
                     surface, park_reason, park_until, identical_failures, last_failure_key, notes)
                VALUES
                    ($title, $description, $status, $priority, $created_at, $updated_at,
                     $surface, '', NULL, 0, '', $notes);
                """;
            cmd.Parameters.AddWithValue("$title", title.Trim());
            cmd.Parameters.AddWithValue("$description", resolvedDescription);
            cmd.Parameters.AddWithValue("$status", resolvedStatus);
            cmd.Parameters.AddWithValue("$priority", resolvedPriority);
            cmd.Parameters.AddWithValue("$created_at", now);
            cmd.Parameters.AddWithValue("$updated_at", now);
            cmd.Parameters.AddWithValue("$surface", resolvedSurface);
            cmd.Parameters.AddWithValue("$notes", resolvedNotes);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            await using var idCmd = _session.Connection.CreateCommand();
            idCmd.CommandText = "SELECT last_insert_rowid();";
            var result = await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (result is null || result is DBNull)
                throw new InvalidOperationException("Failed to obtain victoria_tasks row id after insert.");
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<VictoriaTask?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, title, description, status, priority, created_at, updated_at,
                       surface, park_reason, park_until, identical_failures, last_failure_key, notes
                FROM victoria_tasks
                WHERE id = $id
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$id", id);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                return null;

            return ReadVictoriaTask(reader);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateStatusAsync(long id, string status, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Task status must be non-empty.", nameof(status));

        var normalized = status.Trim().ToLowerInvariant();
        if (!AllowedTaskStatuses.Contains(normalized))
        {
            throw new ArgumentException(
                $"Invalid task status '{status}'. Allowed: todo, in_progress, done, blocked, parked, suspended.",
                nameof(status));
        }

        var now = UtcNow();
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE victoria_tasks
                SET status = $status, updated_at = $updated_at
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$status", normalized);
            cmd.Parameters.AddWithValue("$updated_at", now);
            cmd.Parameters.AddWithValue("$id", id);
            var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return rows > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ParkAsync(
        long id,
        string reason,
        DateTimeOffset parkUntil,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;
        var now = UtcNow();
        var why = (reason ?? "").Trim();
        if (why.Length > 240)
            why = why[..240];
        var until = parkUntil.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE victoria_tasks
                SET status = 'parked',
                    park_reason = $reason,
                    park_until = $until,
                    updated_at = $updated_at
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$reason", why);
            cmd.Parameters.AddWithValue("$until", until);
            cmd.Parameters.AddWithValue("$updated_at", now);
            cmd.Parameters.AddWithValue("$id", id);
            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ReleaseAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;
        var now = UtcNow();
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE victoria_tasks
                SET status = 'todo',
                    park_reason = '',
                    park_until = NULL,
                    identical_failures = 0,
                    last_failure_key = '',
                    updated_at = $updated_at
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$updated_at", now);
            cmd.Parameters.AddWithValue("$id", id);
            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> SuspendAsync(long id, string? notes = null, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;
        var now = UtcNow();
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            if (notes is null)
            {
                cmd.CommandText =
                    """
                    UPDATE victoria_tasks
                    SET status = 'suspended', updated_at = $updated_at
                    WHERE id = $id;
                    """;
            }
            else
            {
                cmd.CommandText =
                    """
                    UPDATE victoria_tasks
                    SET status = 'suspended', notes = $notes, updated_at = $updated_at
                    WHERE id = $id;
                    """;
                cmd.Parameters.AddWithValue("$notes", notes.Trim());
            }

            cmd.Parameters.AddWithValue("$updated_at", now);
            cmd.Parameters.AddWithValue("$id", id);
            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> RecordIdenticalFailureAsync(
        long id,
        string failureKey,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return 0;
        var key = (failureKey ?? "").Trim();
        if (key.Length > 400)
            key = key[..400];
        var now = UtcNow();

        return await _session.RunDbAsync(async ct =>
        {
            await using var read = _session.Connection.CreateCommand();
            read.CommandText =
                """
                SELECT identical_failures, last_failure_key
                FROM victoria_tasks WHERE id = $id LIMIT 1;
                """;
            read.Parameters.AddWithValue("$id", id);
            await using var reader = await read.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                return 0;
            var current = reader.GetInt32(0);
            var last = reader.IsDBNull(1) ? "" : reader.GetString(1);
            await reader.DisposeAsync().ConfigureAwait(false);

            var next = string.Equals(last, key, StringComparison.Ordinal) ? current + 1 : 1;

            await using var upd = _session.Connection.CreateCommand();
            upd.CommandText =
                """
                UPDATE victoria_tasks
                SET identical_failures = $n, last_failure_key = $key, updated_at = $updated_at
                WHERE id = $id;
                """;
            upd.Parameters.AddWithValue("$n", next);
            upd.Parameters.AddWithValue("$key", key);
            upd.Parameters.AddWithValue("$updated_at", now);
            upd.Parameters.AddWithValue("$id", id);
            await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return next;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearFailureCounterAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return;
        var now = UtcNow();
        await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE victoria_tasks
                SET identical_failures = 0, last_failure_key = '', updated_at = $updated_at
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$updated_at", now);
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateDetailsAsync(
        long id,
        string? surface = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;
        if (surface is null && notes is null)
            return await GetAsync(id, cancellationToken).ConfigureAwait(false) is not null;

        var now = UtcNow();
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            if (surface is not null && notes is not null)
            {
                cmd.CommandText =
                    """
                    UPDATE victoria_tasks
                    SET surface = $surface, notes = $notes, updated_at = $updated_at
                    WHERE id = $id;
                    """;
                cmd.Parameters.AddWithValue("$surface", NormalizeSurface(surface));
                cmd.Parameters.AddWithValue("$notes", notes.Trim());
            }
            else if (surface is not null)
            {
                cmd.CommandText =
                    """
                    UPDATE victoria_tasks
                    SET surface = $surface, updated_at = $updated_at
                    WHERE id = $id;
                    """;
                cmd.Parameters.AddWithValue("$surface", NormalizeSurface(surface));
            }
            else
            {
                cmd.CommandText =
                    """
                    UPDATE victoria_tasks
                    SET notes = $notes, updated_at = $updated_at
                    WHERE id = $id;
                    """;
                cmd.Parameters.AddWithValue("$notes", (notes ?? "").Trim());
            }

            cmd.Parameters.AddWithValue("$updated_at", now);
            cmd.Parameters.AddWithValue("$id", id);
            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VictoriaTask>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            if (string.IsNullOrWhiteSpace(status))
            {
                cmd.CommandText =
                    """
                    SELECT id, title, description, status, priority, created_at, updated_at,
                           surface, park_reason, park_until, identical_failures, last_failure_key, notes
                    FROM victoria_tasks
                    ORDER BY updated_at DESC, id DESC;
                    """;
            }
            else
            {
                var normalized = status.Trim().ToLowerInvariant();
                cmd.CommandText =
                    """
                    SELECT id, title, description, status, priority, created_at, updated_at,
                           surface, park_reason, park_until, identical_failures, last_failure_key, notes
                    FROM victoria_tasks
                    WHERE status = $status
                    ORDER BY updated_at DESC, id DESC;
                    """;
                cmd.Parameters.AddWithValue("$status", normalized);
            }

            var list = new List<VictoriaTask>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                list.Add(ReadVictoriaTask(reader));
            return list;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;

        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM victoria_tasks WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveCreateStatus(string? status, bool ready)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            if (!AllowedTaskStatuses.Contains(normalized))
            {
                throw new ArgumentException(
                    $"Invalid task status '{status}'. Allowed: todo, in_progress, done, blocked, parked, suspended.",
                    nameof(status));
            }

            return normalized;
        }

        return ready ? "todo" : DefaultTaskStatus;
    }

    private static string NormalizeSurface(string? surface)
    {
        if (string.IsNullOrWhiteSpace(surface))
            return "local";
        var normalized = surface.Trim().ToLowerInvariant();
        if (!AllowedTaskSurfaces.Contains(normalized))
        {
            throw new ArgumentException(
                $"Invalid task surface '{surface}'. Allowed: maestro, browser, vm, local.",
                nameof(surface));
        }

        return normalized;
    }

    private static string UtcNow() =>
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static VictoriaTask ReadVictoriaTask(SqliteDataReader reader)
    {
        // Columns 0-6 always present (003). 7-12 added by 008 — tolerate older readers via FieldCount.
        var surface = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetString(7) : "local";
        var parkReason = reader.FieldCount > 8 && !reader.IsDBNull(8) ? reader.GetString(8) : "";
        string? parkUntil = reader.FieldCount > 9 && !reader.IsDBNull(9) ? reader.GetString(9) : null;
        var failures = reader.FieldCount > 10 && !reader.IsDBNull(10) ? reader.GetInt32(10) : 0;
        var lastKey = reader.FieldCount > 11 && !reader.IsDBNull(11) ? reader.GetString(11) : "";
        var notes = reader.FieldCount > 12 && !reader.IsDBNull(12) ? reader.GetString(12) : "";

        return new VictoriaTask(
            Id: reader.GetInt64(0),
            Title: reader.GetString(1),
            Description: reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Status: reader.GetString(3),
            Priority: reader.GetString(4),
            CreatedAt: reader.GetString(5),
            UpdatedAt: reader.GetString(6),
            Surface: surface,
            ParkReason: parkReason,
            ParkUntil: parkUntil,
            IdenticalFailures: failures,
            LastFailureKey: lastKey,
            Notes: notes);
    }
}
