namespace SoulCore.Memory;

/// <summary>
/// Victoria's personal journals — three intentional notebooks distinct from
/// general <c>episodic_memories</c>: feeling (moment mood), animation/expression
/// wants, and environment wants (Home / buildings / vehicles / modules).
/// </summary>
public interface IVictoriaJournalStore
{
    /// <summary>The three journal books (<c>feeling</c>, <c>animation</c>, <c>environment</c>).</summary>
    Task<IReadOnlyList<VictoriaJournalBook>> ListBooksAsync(CancellationToken cancellationToken = default);

    /// <summary>Load one book by id, or <c>null</c> when missing.</summary>
    Task<VictoriaJournalBook?> GetBookAsync(string bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Append an entry to a journal. Returns the new entry id.
    /// Throws <see cref="ArgumentException"/> for an unknown <paramref name="bookId"/>.
    /// </summary>
    Task<long> WriteEntryAsync(
        string bookId,
        string body,
        string? moodJson = null,
        string? tagsJson = null,
        string? source = null,
        string? occurredAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recent entries for a book (newest first). When <paramref name="bookId"/> is
    /// null/empty, returns across all books.
    /// </summary>
    Task<IReadOnlyList<VictoriaJournalEntry>> ListEntriesAsync(
        string? bookId = null,
        int limit = 20,
        CancellationToken cancellationToken = default);
}

/// <summary>One of Victoria's three journal notebooks.</summary>
public sealed record VictoriaJournalBook(
    string Id,
    string Title,
    string Purpose,
    string CreatedAt);

/// <summary>One dated entry in a journal book.</summary>
public sealed record VictoriaJournalEntry(
    long Id,
    string BookId,
    string Body,
    string? MoodJson,
    string TagsJson,
    string OccurredAt,
    string CreatedAt,
    string Source);
