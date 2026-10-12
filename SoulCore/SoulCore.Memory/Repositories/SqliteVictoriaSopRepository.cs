using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SoulCore.Memory.Repositories;

/// <summary>
/// PROP-17.2 SOP CRUD. SOPs are recall only — they never execute host gates.
/// </summary>
public sealed class SqliteVictoriaSopRepository : IVictoriaSopStore
{
    private readonly SqliteMemorySession _session;

    public SqliteVictoriaSopRepository(SqliteMemorySession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    public async Task<long> CreateAsync(
        string title,
        string whenText,
        string steps,
        string donts,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("SOP title must be non-empty.", nameof(title));

        var now = UtcNow();
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO victoria_sops (title, when_text, steps, donts, created_at, updated_at)
                VALUES ($title, $when, $steps, $donts, $created_at, $updated_at);
                """;
            cmd.Parameters.AddWithValue("$title", title.Trim());
            cmd.Parameters.AddWithValue("$when", (whenText ?? "").Trim());
            cmd.Parameters.AddWithValue("$steps", (steps ?? "").Trim());
            cmd.Parameters.AddWithValue("$donts", (donts ?? "").Trim());
            cmd.Parameters.AddWithValue("$created_at", now);
            cmd.Parameters.AddWithValue("$updated_at", now);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            await using var idCmd = _session.Connection.CreateCommand();
            idCmd.CommandText = "SELECT last_insert_rowid();";
            var result = await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (result is null || result is DBNull)
                throw new InvalidOperationException("Failed to obtain victoria_sops row id after insert.");
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<VictoriaSop?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, title, when_text, steps, donts, created_at, updated_at
                FROM victoria_sops WHERE id = $id LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                return null;
            return Read(reader);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> UpdateAsync(
        long id,
        string? title = null,
        string? whenText = null,
        string? steps = null,
        string? donts = null,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;
        var existing = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return false;

        var now = UtcNow();
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE victoria_sops
                SET title = $title, when_text = $when, steps = $steps, donts = $donts, updated_at = $updated_at
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$title", (title ?? existing.Title).Trim());
            cmd.Parameters.AddWithValue("$when", (whenText ?? existing.WhenText).Trim());
            cmd.Parameters.AddWithValue("$steps", (steps ?? existing.Steps).Trim());
            cmd.Parameters.AddWithValue("$donts", (donts ?? existing.Donts).Trim());
            cmd.Parameters.AddWithValue("$updated_at", now);
            cmd.Parameters.AddWithValue("$id", id);
            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VictoriaSop>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, title, when_text, steps, donts, created_at, updated_at
                FROM victoria_sops
                ORDER BY id ASC;
                """;
            var list = new List<VictoriaSop>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                list.Add(Read(reader));
            return list;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string UtcNow() =>
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static VictoriaSop Read(SqliteDataReader reader) =>
        new(
            Id: reader.GetInt64(0),
            Title: reader.GetString(1),
            WhenText: reader.IsDBNull(2) ? "" : reader.GetString(2),
            Steps: reader.IsDBNull(3) ? "" : reader.GetString(3),
            Donts: reader.IsDBNull(4) ? "" : reader.GetString(4),
            CreatedAt: reader.GetString(5),
            UpdatedAt: reader.GetString(6));
}
