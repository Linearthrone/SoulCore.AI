using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace House.ChatDesktop.Services;

public sealed class JournalBookRow
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Purpose { get; init; } = "";
    public string CreatedAt { get; init; } = "";

    public string Line =>
        string.IsNullOrWhiteSpace(Title)
            ? Id
            : $"{Title} ({Id})";
}

public sealed class JournalEntryRow
{
    public long Id { get; init; }
    public string BookId { get; init; } = "";
    public string Body { get; init; } = "";
    public string? MoodJson { get; init; }
    public string TagsJson { get; init; } = "[]";
    public string OccurredAt { get; init; } = "";
    public string CreatedAt { get; init; } = "";
    public string Source { get; init; } = "";

    public string Line
    {
        get
        {
            var when = string.IsNullOrWhiteSpace(OccurredAt) ? CreatedAt : OccurredAt;
            var preview = Body.Length <= 96 ? Body : Body[..96] + "…";
            var tagHint = TopicTagHint(TagsJson);
            var head = string.IsNullOrWhiteSpace(when) ? preview : $"{when} — {preview}";
            return string.IsNullOrWhiteSpace(tagHint) ? head : $"{head} · {tagHint}";
        }
    }

    /// <summary>PROP-18.3: surface topic tags in the list so subject appends are visible.</summary>
    internal static string? TopicTagHint(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson) || tagsJson.Trim() is "[]" or "null")
            return null;
        var t = tagsJson.Trim();
        return t.Length <= 48 ? t : t[..47] + "…";
    }
}

public sealed record JournalBooksFetch(IReadOnlyList<JournalBookRow> Books, string? Problem);

public sealed record JournalEntriesFetch(
    IReadOnlyList<JournalEntryRow> Entries,
    string? Problem,
    string? BookTitle,
    string? BookId);

/// <summary>Presence client for Host private Victoria journals (not research papers).</summary>
public sealed class SoulCoreJournalClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };

    public async Task<JournalBooksFetch> ListBooksAsync(CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return new JournalBooksFetch(Array.Empty<JournalBookRow>(), "Her journals stay on this machine.");

        try
        {
            using var response = await _http.GetAsync(Uri("/journals/books"), cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new JournalBooksFetch(
                    Array.Empty<JournalBookRow>(),
                    response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "Her journals need a Host restart."
                        : $"Journals HTTP {(int)response.StatusCode}");
            }

            var dto = await response.Content.ReadFromJsonAsync<BooksDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var rows = (dto?.Books ?? new List<BookDto>())
                .Select(b => new JournalBookRow
                {
                    Id = b.Id ?? "",
                    Title = b.Title ?? "",
                    Purpose = b.Purpose ?? "",
                    CreatedAt = b.CreatedAt ?? ""
                })
                .ToList();
            return new JournalBooksFetch(rows, null);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new JournalBooksFetch(Array.Empty<JournalBookRow>(), "Her journals unavailable.");
        }
    }

    public async Task<JournalEntriesFetch> ListEntriesAsync(
        string bookId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bookId) || !ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return new JournalEntriesFetch(Array.Empty<JournalEntryRow>(), "Invalid journal book.", null, null);

        try
        {
            var path =
                $"/journals/books/{System.Uri.EscapeDataString(bookId.Trim())}/entries?limit={Math.Clamp(limit, 1, 200)}";
            using var response = await _http.GetAsync(Uri(path), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new JournalEntriesFetch(
                    Array.Empty<JournalEntryRow>(),
                    response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "Journal book not found (or Host needs restart)."
                        : $"Journals HTTP {(int)response.StatusCode}",
                    null,
                    bookId);
            }

            var dto = await response.Content.ReadFromJsonAsync<BookEntriesDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var rows = (dto?.Entries ?? new List<EntryDto>()).Select(ToRow).ToList();
            return new JournalEntriesFetch(rows, null, dto?.Book?.Title, dto?.Book?.Id ?? bookId);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new JournalEntriesFetch(Array.Empty<JournalEntryRow>(), "Journal entries unavailable.", null, bookId);
        }
    }

    public void Dispose() => _http.Dispose();

    private static Uri Uri(string path) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}{path}");

    private static JournalEntryRow ToRow(EntryDto e) => new()
    {
        Id = e.Id,
        BookId = e.BookId ?? "",
        Body = e.Body ?? "",
        MoodJson = e.MoodJson,
        TagsJson = e.TagsJson ?? "[]",
        OccurredAt = e.OccurredAt ?? "",
        CreatedAt = e.CreatedAt ?? "",
        Source = e.Source ?? ""
    };

    private sealed class BooksDto
    {
        [JsonPropertyName("books")]
        public List<BookDto>? Books { get; set; }
    }

    private sealed class BookDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("purpose")]
        public string? Purpose { get; set; }

        [JsonPropertyName("createdAt")]
        public string? CreatedAt { get; set; }
    }

    private sealed class BookEntriesDto
    {
        [JsonPropertyName("book")]
        public BookDto? Book { get; set; }

        [JsonPropertyName("entries")]
        public List<EntryDto>? Entries { get; set; }
    }

    private sealed class EntryDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("bookId")]
        public string? BookId { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("moodJson")]
        public string? MoodJson { get; set; }

        [JsonPropertyName("tagsJson")]
        public string? TagsJson { get; set; }

        [JsonPropertyName("occurredAt")]
        public string? OccurredAt { get; set; }

        [JsonPropertyName("createdAt")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }
}
