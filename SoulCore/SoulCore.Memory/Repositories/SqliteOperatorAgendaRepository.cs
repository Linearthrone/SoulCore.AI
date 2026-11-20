using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace SoulCore.Memory.Repositories;

/// <summary>PROP-22.1: <c>operator_agenda_items</c> CRUD (list / create / get / delete).</summary>
public sealed partial class SqliteOperatorAgendaRepository : IOperatorAgendaStore
{
    private readonly SqliteMemorySession _session;

    public SqliteOperatorAgendaRepository(SqliteMemorySession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    [GeneratedRegex(@"^([01]\d|2[0-3]):[0-5]\d$")]
    private static partial Regex TimeLocalPattern();

    /// <summary>True when <paramref name="value"/> is <c>HH:mm</c> (24h).</summary>
    public static bool IsValidTimeLocal(string? value) =>
        !string.IsNullOrEmpty(value) && TimeLocalPattern().IsMatch(value);

    /// <inheritdoc />
    public async Task<IReadOnlyList<OperatorAgendaItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, title, weekday, time_local, notes, created_at
                FROM operator_agenda_items
                ORDER BY weekday ASC, time_local ASC, id ASC;
                """;

            var list = new List<OperatorAgendaItem>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                list.Add(Read(reader));
            return (IReadOnlyList<OperatorAgendaItem>)list;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(OperatorAgendaItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(item.Title))
            throw new ArgumentException("Title must be non-empty.", nameof(item));
        if (item.Weekday is < 0 or > 6)
            throw new ArgumentException("Weekday must be 0 (Mon) through 6 (Sun).", nameof(item));
        var time = string.IsNullOrWhiteSpace(item.TimeLocal) ? "09:00" : item.TimeLocal.Trim();
        if (!IsValidTimeLocal(time))
            throw new ArgumentException("timeLocal must be HH:mm.", nameof(item));

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO operator_agenda_items (title, weekday, time_local, notes, created_at)
                VALUES ($title, $weekday, $time_local, $notes, $created_at);
                """;
            cmd.Parameters.AddWithValue("$title", item.Title.Trim());
            cmd.Parameters.AddWithValue("$weekday", item.Weekday);
            cmd.Parameters.AddWithValue("$time_local", time);
            cmd.Parameters.AddWithValue(
                "$notes",
                string.IsNullOrWhiteSpace(item.Notes) ? DBNull.Value : item.Notes.Trim());
            cmd.Parameters.AddWithValue("$created_at", now);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            await using var idCmd = _session.Connection.CreateCommand();
            idCmd.CommandText = "SELECT last_insert_rowid();";
            var result = await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (result is null || result is DBNull)
                throw new InvalidOperationException("Failed to obtain operator_agenda_items row id after insert.");
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperatorAgendaItem?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, title, weekday, time_local, notes, created_at
                FROM operator_agenda_items
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
            cmd.CommandText = "DELETE FROM operator_agenda_items WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static OperatorAgendaItem Read(SqliteDataReader r) => new(
        Id: r.GetInt64(0),
        Title: r.GetString(1),
        Weekday: r.GetInt32(2),
        TimeLocal: r.GetString(3),
        Notes: r.IsDBNull(4) ? null : r.GetString(4),
        CreatedAt: r.GetString(5));
}
