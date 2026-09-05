using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SoulCore.Memory.Repositories;

public sealed class SqliteVictoriaJournalRepository : IVictoriaJournalStore
{
    public static readonly HashSet<string> AllowedJournalBookIds = new(StringComparer.OrdinalIgnoreCase){"feeling","animation","environment"};
    private readonly SqliteMemorySession _session;
    public SqliteVictoriaJournalRepository(SqliteMemorySession session) => _session = session ?? throw new ArgumentNullException(nameof(session));


/// <inheritdoc />
public async Task<IReadOnlyList<VictoriaJournalBook>> ListBooksAsync(
    CancellationToken cancellationToken = default)
{

    return await _session.RunDbAsync(async ct =>
    {
    await using var cmd = _session.Connection.CreateCommand();
    cmd.CommandText =
        """
        SELECT id, title, purpose, created_at
        FROM victoria_journal_books
        ORDER BY CASE id
            WHEN 'feeling' THEN 1
            WHEN 'animation' THEN 2
            WHEN 'environment' THEN 3
            ELSE 9
        END;
        """;

    var list = new List<VictoriaJournalBook>();
    await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
    while (await reader.ReadAsync(ct).ConfigureAwait(false))
    {
        list.Add(new VictoriaJournalBook(
            Id: reader.GetString(0),
            Title: reader.GetString(1),
            Purpose: reader.GetString(2),
            CreatedAt: reader.GetString(3)));
    }

    return list;
    }, cancellationToken).ConfigureAwait(false);
}

/// <inheritdoc />
public async Task<VictoriaJournalBook?> GetBookAsync(
    string bookId,
    CancellationToken cancellationToken = default)
{

    if (string.IsNullOrWhiteSpace(bookId))
        return null;
    return await _session.RunDbAsync(async ct =>
    {
    await using var cmd = _session.Connection.CreateCommand();
    cmd.CommandText =
        """
        SELECT id, title, purpose, created_at
        FROM victoria_journal_books
        WHERE id = $id
        LIMIT 1;
        """;
    cmd.Parameters.AddWithValue("$id", bookId.Trim().ToLowerInvariant());

    await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
    if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        return null;

    return new VictoriaJournalBook(
        Id: reader.GetString(0),
        Title: reader.GetString(1),
        Purpose: reader.GetString(2),
        CreatedAt: reader.GetString(3));
    }, cancellationToken).ConfigureAwait(false);
}

/// <inheritdoc />
public async Task<long> WriteEntryAsync(
    string bookId,
    string body,
    string? moodJson = null,
    string? tagsJson = null,
    string? source = null,
    string? occurredAt = null,
    CancellationToken cancellationToken = default)
{

    if (string.IsNullOrWhiteSpace(bookId))
        throw new ArgumentException("Journal book id must be non-empty.", nameof(bookId));
    if (string.IsNullOrWhiteSpace(body))
        throw new ArgumentException("Journal entry body must be non-empty.", nameof(body));

    var normalizedBook = bookId.Trim().ToLowerInvariant();
    if (!AllowedJournalBookIds.Contains(normalizedBook))
    {
        throw new ArgumentException(
            $"Unknown journal book '{bookId}'. Allowed: feeling, animation, environment.",
            nameof(bookId));
    }

    var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    var when = string.IsNullOrWhiteSpace(occurredAt) ? now : occurredAt.Trim();
    var resolvedSource = MemorySourceNormalizer.Normalize(source ?? "self");
    var tags = string.IsNullOrWhiteSpace(tagsJson) ? "[]" : tagsJson.Trim();
    return await _session.RunDbAsync(async ct =>
    {
    await using var cmd = _session.Connection.CreateCommand();
    cmd.CommandText =
        """
        INSERT INTO victoria_journal_entries
            (book_id, body, mood_json, tags_json, occurred_at, created_at, source)
        VALUES
            ($book_id, $body, $mood_json, $tags_json, $occurred_at, $created_at, $source);
        """;
    cmd.Parameters.AddWithValue("$book_id", normalizedBook);
    cmd.Parameters.AddWithValue("$body", body.Trim());
    cmd.Parameters.AddWithValue("$mood_json", (object?)moodJson ?? DBNull.Value);
    cmd.Parameters.AddWithValue("$tags_json", tags);
    cmd.Parameters.AddWithValue("$occurred_at", when);
    cmd.Parameters.AddWithValue("$created_at", now);
    cmd.Parameters.AddWithValue("$source", resolvedSource);
    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

    await using var idCmd = _session.Connection.CreateCommand();
    idCmd.CommandText = "SELECT last_insert_rowid();";
    var result = await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
    if (result is null || result is DBNull)
        throw new InvalidOperationException("Failed to obtain victoria_journal_entries row id after insert.");
    return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }, cancellationToken).ConfigureAwait(false);
}

/// <inheritdoc />
public async Task<IReadOnlyList<VictoriaJournalEntry>> ListEntriesAsync(
    string? bookId = null,
    int limit = 20,
    CancellationToken cancellationToken = default)
{

    var take = Math.Clamp(limit, 1, 200);
    return await _session.RunDbAsync(async ct =>
    {
    await using var cmd = _session.Connection.CreateCommand();
    if (string.IsNullOrWhiteSpace(bookId))
    {
        cmd.CommandText =
            """
            SELECT id, book_id, body, mood_json, tags_json, occurred_at, created_at, source
            FROM victoria_journal_entries
            ORDER BY occurred_at DESC, id DESC
            LIMIT $limit;
            """;
    }
    else
    {
        cmd.CommandText =
            """
            SELECT id, book_id, body, mood_json, tags_json, occurred_at, created_at, source
            FROM victoria_journal_entries
            WHERE book_id = $book_id
            ORDER BY occurred_at DESC, id DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$book_id", bookId.Trim().ToLowerInvariant());
    }

    cmd.Parameters.AddWithValue("$limit", take);

    var list = new List<VictoriaJournalEntry>();
    await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
    while (await reader.ReadAsync(ct).ConfigureAwait(false))
    {
        list.Add(new VictoriaJournalEntry(
            Id: reader.GetInt64(0),
            BookId: reader.GetString(1),
            Body: reader.GetString(2),
            MoodJson: reader.IsDBNull(3) ? null : reader.GetString(3),
            TagsJson: reader.IsDBNull(4) ? "[]" : reader.GetString(4),
            OccurredAt: reader.GetString(5),
            CreatedAt: reader.GetString(6),
            Source: reader.GetString(7)));
    }

    return list;
    }, cancellationToken).ConfigureAwait(false);
}


}
