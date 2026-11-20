using Avalonia.Controls;
using Avalonia.Interactivity;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Views;

/// <summary>Her private journal books, rendered inside the glass shell (PROP-22.7).</summary>
public partial class JournalView : UserControl, IDisposable
{
    private readonly SoulCoreJournalClient _client = new();
    private bool _busy;
    private long? _pendingEntryId;

    public JournalView()
    {
        InitializeComponent();
    }

    public async Task ReloadAsync()
    {
        if (_busy)
            return;
        _busy = true;
        try
        {
            SetStatus("Loading books…");
            var fetch = await _client.ListBooksAsync().ConfigureAwait(true);
            BookList.ItemsSource = fetch.Books;
            EntryList.ItemsSource = null;
            EntryBody.Text = "";
            if (!string.IsNullOrWhiteSpace(fetch.Problem))
            {
                SetStatus(fetch.Problem);
                return;
            }

            var hasThoughts = fetch.Books.Any(b =>
                string.Equals(b.Id, "thoughts", StringComparison.OrdinalIgnoreCase));
            SetStatus(fetch.Books.Count == 0
                ? "No journal books yet."
                : hasThoughts
                    ? $"{fetch.Books.Count} book(s), including thoughts."
                    : $"{fetch.Books.Count} book(s) — thoughts missing (Host needs migration 012).");
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Open a specific entry (used by the Victoria drawer's journal link).</summary>
    public async Task OpenEntryAsync(string? bookId, long? entryId)
    {
        await ReloadAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(bookId))
            return;

        var book = (BookList.ItemsSource as IEnumerable<JournalBookRow>)?
            .FirstOrDefault(b => string.Equals(b.Id, bookId, StringComparison.OrdinalIgnoreCase));
        if (book is null)
        {
            SetStatus($"Journal book “{bookId}” was not found.");
            return;
        }

        _pendingEntryId = entryId;
        BookList.SelectedItem = book;
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e) =>
        await ReloadAsync().ConfigureAwait(true);

    private async void BookList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (BookList.SelectedItem is not JournalBookRow book)
            return;
        await LoadEntriesAsync(book).ConfigureAwait(true);
    }

    private async Task LoadEntriesAsync(JournalBookRow book)
    {
        if (_busy)
            return;
        _busy = true;
        try
        {
            SetStatus($"Loading entries for {book.Title}…");
            var fetch = await _client.ListEntriesAsync(book.Id).ConfigureAwait(true);
            EntriesHeader.Text = string.IsNullOrWhiteSpace(fetch.BookTitle)
                ? "Entries"
                : $"Entries — {fetch.BookTitle}";
            EntryList.ItemsSource = fetch.Entries;
            EntryBody.Text = "";
            if (!string.IsNullOrWhiteSpace(fetch.Problem))
            {
                SetStatus(fetch.Problem);
                return;
            }

            SetStatus(fetch.Entries.Count == 0
                ? $"No entries in “{book.Title}” yet."
                : $"{fetch.Entries.Count} entr{(fetch.Entries.Count == 1 ? "y" : "ies")} in “{book.Title}”.");

            if (_pendingEntryId is long wanted)
            {
                _pendingEntryId = null;
                var match = fetch.Entries.FirstOrDefault(r => r.Id == wanted);
                if (match is not null)
                    EntryList.SelectedItem = match;
            }
        }
        finally
        {
            _busy = false;
        }
    }

    private void EntryList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (EntryList.SelectedItem is not JournalEntryRow row)
            return;

        // Topic appends carry subject tags — show them so residue is inspectable.
        var tags = string.IsNullOrWhiteSpace(row.TagsJson) || row.TagsJson == "[]"
            ? "(none)"
            : row.TagsJson.Trim();
        EntryBody.Text =
            $"#{row.Id} · {row.BookId} · {row.Source}\n" +
            $"Occurred {row.OccurredAt}\n" +
            $"Tags {tags}\n\n" +
            $"{row.Body}";
        SetStatus($"Opened entry #{row.Id}.");
    }

    private void SetStatus(string text) => StatusText.Text = text;

    public void Dispose() => _client.Dispose();
}
