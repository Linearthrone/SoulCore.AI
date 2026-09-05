using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SoulCore.Memory.Repositories;

public sealed class SqliteVictoriaTaskRepository : IVictoriaTaskStore
{
    public static readonly HashSet<string> AllowedTaskStatuses = new(StringComparer.OrdinalIgnoreCase){"todo","in_progress","done","blocked"};
    public const string DefaultTaskPriority = "medium";
    public const string DefaultTaskStatus = "todo";
    private readonly SqliteMemorySession _session;
    public SqliteVictoriaTaskRepository(SqliteMemorySession session) => _session = session ?? throw new ArgumentNullException(nameof(session));


/// <inheritdoc />
public async Task<long> CreateAsync(
    string title,
    string? description,
    string? priority,
    CancellationToken cancellationToken = default)
{

    if (string.IsNullOrWhiteSpace(title))
        throw new ArgumentException("Task title must be non-empty.", nameof(title));

    var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    var resolvedPriority = string.IsNullOrWhiteSpace(priority)
        ? DefaultTaskPriority
        : priority.Trim().ToLowerInvariant();
    var resolvedDescription = description?.Trim() ?? string.Empty;
    return await _session.RunDbAsync(async ct =>
    {
    await using var cmd = _session.Connection.CreateCommand();
    cmd.CommandText =
        """
        INSERT INTO victoria_tasks (title, description, status, priority, created_at, updated_at)
        VALUES ($title, $description, $status, $priority, $created_at, $updated_at);
        """;
    cmd.Parameters.AddWithValue("$title", title.Trim());
    cmd.Parameters.AddWithValue("$description", resolvedDescription);
    cmd.Parameters.AddWithValue("$status", DefaultTaskStatus);
    cmd.Parameters.AddWithValue("$priority", resolvedPriority);
    cmd.Parameters.AddWithValue("$created_at", now);
    cmd.Parameters.AddWithValue("$updated_at", now);
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
        SELECT id, title, description, status, priority, created_at, updated_at
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
            $"Invalid task status '{status}'. Allowed: todo, in_progress, done, blocked.",
            nameof(status));
    }

    var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
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
            SELECT id, title, description, status, priority, created_at, updated_at
            FROM victoria_tasks
            ORDER BY updated_at DESC, id DESC;
            """;
    }
    else
    {
        var normalized = status.Trim().ToLowerInvariant();
        cmd.CommandText =
            """
            SELECT id, title, description, status, priority, created_at, updated_at
            FROM victoria_tasks
            WHERE status = $status
            ORDER BY updated_at DESC, id DESC;
            """;
        cmd.Parameters.AddWithValue("$status", normalized);
    }

    var list = new List<VictoriaTask>();
    await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
    while (await reader.ReadAsync(ct).ConfigureAwait(false))
    {
        list.Add(ReadVictoriaTask(reader));
    }

    return list;
    }, cancellationToken).ConfigureAwait(false);
}

private static VictoriaTask ReadVictoriaTask(SqliteDataReader reader)
{
    return new VictoriaTask(
        Id: reader.GetInt64(0),
        Title: reader.GetString(1),
        Description: reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
        Status: reader.GetString(3),
        Priority: reader.GetString(4),
        CreatedAt: reader.GetString(5),
        UpdatedAt: reader.GetString(6));
}


}
