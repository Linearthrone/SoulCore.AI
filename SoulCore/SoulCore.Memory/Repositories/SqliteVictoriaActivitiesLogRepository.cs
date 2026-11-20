using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SoulCore.Memory.Repositories;

public sealed class SqliteVictoriaActivitiesLogRepository : IVictoriaActivitiesLogStore
{
    private readonly SqliteMemorySession _session;

    public SqliteVictoriaActivitiesLogRepository(SqliteMemorySession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    /// <inheritdoc />
    public Task<long> AppendAsync(
        string activity,
        string subject,
        string sessionDesc,
        string? occurredAt = null,
        string? journalBookId = null,
        long? journalEntryId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(activity))
            throw new ArgumentException("Activity must be non-empty.", nameof(activity));
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject must be non-empty.", nameof(subject));
        if (string.IsNullOrWhiteSpace(sessionDesc))
            throw new ArgumentException("Session desc must be non-empty.", nameof(sessionDesc));

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var when = string.IsNullOrWhiteSpace(occurredAt) ? now : occurredAt.Trim();
        var book = string.IsNullOrWhiteSpace(journalBookId)
            ? null
            : journalBookId.Trim().ToLowerInvariant();

        return _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO victoria_activities_log
                    (occurred_at, activity, subject, session_desc,
                     journal_book_id, journal_entry_id, created_at)
                VALUES
                    ($occurred_at, $activity, $subject, $session_desc,
                     $journal_book_id, $journal_entry_id, $created_at);
                """;
            cmd.Parameters.AddWithValue("$occurred_at", when);
            cmd.Parameters.AddWithValue("$activity", activity.Trim());
            cmd.Parameters.AddWithValue("$subject", subject.Trim());
            cmd.Parameters.AddWithValue("$session_desc", sessionDesc.Trim());
            cmd.Parameters.AddWithValue("$journal_book_id", (object?)book ?? DBNull.Value);
            cmd.Parameters.AddWithValue(
                "$journal_entry_id",
                journalEntryId.HasValue ? journalEntryId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("$created_at", now);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            await using var idCmd = _session.Connection.CreateCommand();
            idCmd.CommandText = "SELECT last_insert_rowid();";
            var result = await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (result is null || result is DBNull)
                throw new InvalidOperationException(
                    "Failed to obtain victoria_activities_log entry_no after insert.");
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<VictoriaActivityLogEntry>> ListRecentAsync(
        string? subject = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 500);
        return _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            if (string.IsNullOrWhiteSpace(subject))
            {
                cmd.CommandText =
                    """
                    SELECT entry_no, occurred_at, activity, subject, session_desc,
                           journal_book_id, journal_entry_id, created_at
                    FROM victoria_activities_log
                    ORDER BY occurred_at DESC, entry_no DESC
                    LIMIT $limit;
                    """;
            }
            else
            {
                cmd.CommandText =
                    """
                    SELECT entry_no, occurred_at, activity, subject, session_desc,
                           journal_book_id, journal_entry_id, created_at
                    FROM victoria_activities_log
                    WHERE subject = $subject
                    ORDER BY occurred_at DESC, entry_no DESC
                    LIMIT $limit;
                    """;
                cmd.Parameters.AddWithValue("$subject", subject.Trim());
            }

            cmd.Parameters.AddWithValue("$limit", take);

            var list = new List<VictoriaActivityLogEntry>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                list.Add(ReadEntry(reader));
            return (IReadOnlyList<VictoriaActivityLogEntry>)list;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VictoriaActivityLogEntry?> GetAsync(
        long entryNo,
        CancellationToken cancellationToken = default) =>
        _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT entry_no, occurred_at, activity, subject, session_desc,
                       journal_book_id, journal_entry_id, created_at
                FROM victoria_activities_log
                WHERE entry_no = $entry_no
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$entry_no", entryNo);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                return null;
            return ReadEntry(reader);
        }, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<VictoriaActivityLogEntry>> ListSinceAsync(
        string sinceOccurredAt,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sinceOccurredAt))
            throw new ArgumentException("sinceOccurredAt must be non-empty.", nameof(sinceOccurredAt));

        var take = Math.Clamp(limit, 1, 500);
        var since = sinceOccurredAt.Trim();
        return _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT entry_no, occurred_at, activity, subject, session_desc,
                       journal_book_id, journal_entry_id, created_at
                FROM victoria_activities_log
                WHERE occurred_at >= $since
                ORDER BY occurred_at DESC, entry_no DESC
                LIMIT $limit;
                """;
            cmd.Parameters.AddWithValue("$since", since);
            cmd.Parameters.AddWithValue("$limit", take);

            var list = new List<VictoriaActivityLogEntry>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                list.Add(ReadEntry(reader));
            return (IReadOnlyList<VictoriaActivityLogEntry>)list;
        }, cancellationToken);
    }

    private static VictoriaActivityLogEntry ReadEntry(SqliteDataReader reader) =>
        new(
            EntryNo: reader.GetInt64(0),
            OccurredAt: reader.GetString(1),
            Activity: reader.GetString(2),
            Subject: reader.GetString(3),
            SessionDesc: reader.GetString(4),
            JournalBookId: reader.IsDBNull(5) ? null : reader.GetString(5),
            JournalEntryId: reader.IsDBNull(6) ? null : reader.GetInt64(6),
            CreatedAt: reader.GetString(7));
}
