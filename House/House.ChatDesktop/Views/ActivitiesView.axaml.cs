using Avalonia.Controls;
using Avalonia.Interactivity;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Views;

/// <summary>Host activities log (Entry / DateTime / Activity / Subject / Desc), in-glass (PROP-22.7).</summary>
public partial class ActivitiesView : UserControl, IDisposable
{
    private readonly SoulCoreActivitiesClient _client = new();
    private bool _busy;

    public ActivitiesView()
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
            SetStatus("Loading activities…");
            var subject = SubjectFilterBox.Text?.Trim();
            var fetch = await _client.ListAsync(subject, limit: 80).ConfigureAwait(true);
            ActivityList.ItemsSource = fetch.Activities;
            DetailBox.Text = "";

            if (!string.IsNullOrWhiteSpace(fetch.Problem))
            {
                SetStatus(fetch.Problem);
                return;
            }

            SetStatus(fetch.Activities.Count == 0
                ? "No activities yet — empty is honest."
                : $"{fetch.Activities.Count} entr{(fetch.Activities.Count == 1 ? "y" : "ies")} from Host.");
        }
        finally
        {
            _busy = false;
        }
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e) =>
        await ReloadAsync().ConfigureAwait(true);

    private async void Filter_Click(object? sender, RoutedEventArgs e) =>
        await ReloadAsync().ConfigureAwait(true);

    private void ActivityList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ActivityList.SelectedItem is not ActivityLogRow row)
            return;

        var journal = row.JournalEntryId is > 0
            ? $"\nJournal soft-link: {row.JournalBookId ?? "?"} #{row.JournalEntryId}"
            : "";
        DetailBox.Text =
            $"Entry #{row.EntryNo}\n" +
            $"Date/Time: {row.DateTimeText}\n" +
            $"Activity: {row.ActivityText}\n" +
            $"Subject/Focus: {row.SubjectText}\n\n" +
            $"{row.DescText}{journal}";
        SetStatus($"Opened entry #{row.EntryNo}.");
    }

    private void SetStatus(string text) => StatusText.Text = text;

    public void Dispose() => _client.Dispose();
}
