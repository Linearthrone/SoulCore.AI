using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SoulCore.Memory.Repositories;

/// <summary>PROP-22.1: <c>operator_work_items</c> CRUD (list / create / get / delete).</summary>
public sealed class SqliteOperatorWorkItemRepository : IOperatorWorkItemStore
{
    private const string SelectColumns =
        "id, kind, title, description, details, deadline_utc, priority, no_checkin, " +
        "phase_checkpoints_json, deliverables_json, needs_extra_permissions, outside_collaboration, " +
        "status, created_at, updated_at";

    private readonly SqliteMemorySession _session;

    public SqliteOperatorWorkItemRepository(SqliteMemorySession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    /// <inheritdoc />
    public async Task<IReadOnlyList<OperatorWorkItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                $"""
                SELECT {SelectColumns}
                FROM operator_work_items
                ORDER BY updated_at DESC, id DESC;
                """;

            var list = new List<OperatorWorkItem>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                list.Add(Read(reader));
            return (IReadOnlyList<OperatorWorkItem>)list;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(OperatorWorkItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var kind = (item.Kind ?? "").Trim().ToLowerInvariant();
        if (!OperatorWorkItemRules.AllowedKinds.Contains(kind))
            throw new ArgumentException("Invalid kind. Allowed: task, project.", nameof(item));
        if (string.IsNullOrWhiteSpace(item.Title))
            throw new ArgumentException("Title must be non-empty.", nameof(item));
        if (item.Priority is < 1 or > 5)
            throw new ArgumentException("Priority must be 1-5.", nameof(item));

        var status = string.IsNullOrWhiteSpace(item.Status) ? "open" : item.Status.Trim().ToLowerInvariant();
        if (!OperatorWorkItemRules.AllowedStatuses.Contains(status))
            throw new ArgumentException("Invalid status. Allowed: open, done, cancelled.", nameof(item));

        var now = UtcNow();
        // phase checkpoints / deliverables are project-only (contract §3.1).
        var isProject = kind == "project";

        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO operator_work_items
                    (kind, title, description, details, deadline_utc, priority, no_checkin,
                     phase_checkpoints_json, deliverables_json, needs_extra_permissions,
                     outside_collaboration, status, created_at, updated_at)
                VALUES
                    ($kind, $title, $description, $details, $deadline_utc, $priority, $no_checkin,
                     $phase_checkpoints_json, $deliverables_json, $needs_extra_permissions,
                     $outside_collaboration, $status, $created_at, $updated_at);
                """;
            cmd.Parameters.AddWithValue("$kind", kind);
            cmd.Parameters.AddWithValue("$title", item.Title.Trim());
            cmd.Parameters.AddWithValue("$description", (object?)item.Description?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$details", (object?)item.Details?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$deadline_utc", (object?)NullIfBlank(item.DeadlineUtc) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$priority", item.Priority);
            cmd.Parameters.AddWithValue("$no_checkin", item.NoCheckin ? 1 : 0);
            cmd.Parameters.AddWithValue(
                "$phase_checkpoints_json",
                isProject ? (object?)NullIfBlank(item.PhaseCheckpointsJson) ?? DBNull.Value : DBNull.Value);
            cmd.Parameters.AddWithValue(
                "$deliverables_json",
                isProject ? (object?)NullIfBlank(item.DeliverablesJson) ?? DBNull.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("$needs_extra_permissions", item.NeedsExtraPermissions ? 1 : 0);
            cmd.Parameters.AddWithValue("$outside_collaboration", item.OutsideCollaboration ? 1 : 0);
            cmd.Parameters.AddWithValue("$status", status);
            cmd.Parameters.AddWithValue("$created_at", now);
            cmd.Parameters.AddWithValue("$updated_at", now);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            await using var idCmd = _session.Connection.CreateCommand();
            idCmd.CommandText = "SELECT last_insert_rowid();";
            var result = await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (result is null || result is DBNull)
                throw new InvalidOperationException("Failed to obtain operator_work_items row id after insert.");
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperatorWorkItem?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                $"""
                SELECT {SelectColumns}
                FROM operator_work_items
                WHERE id = $id
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
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
            cmd.CommandText = "DELETE FROM operator_work_items WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string UtcNow() =>
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static string? OptString(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    private static OperatorWorkItem Read(SqliteDataReader r) => new(
        Id: r.GetInt64(0),
        Kind: r.GetString(1),
        Title: r.GetString(2),
        Description: OptString(r, 3),
        Details: OptString(r, 4),
        DeadlineUtc: OptString(r, 5),
        Priority: r.GetInt32(6),
        NoCheckin: r.GetInt32(7) != 0,
        PhaseCheckpointsJson: OptString(r, 8),
        DeliverablesJson: OptString(r, 9),
        NeedsExtraPermissions: r.GetInt32(10) != 0,
        OutsideCollaboration: r.GetInt32(11) != 0,
        Status: r.GetString(12),
        CreatedAt: r.GetString(13),
        UpdatedAt: r.GetString(14));
}
