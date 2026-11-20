using Avalonia.Controls;
using Avalonia.Interactivity;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Views;

/// <summary>Research / hobby subject journals and APA papers, in-glass (PROP-22.7).</summary>
public partial class ResearchView : UserControl, IDisposable
{
    private readonly SoulCoreResearchClient _client = new();
    private bool _busy;

    public ResearchView()
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
            SetStatus("Loading subjects…");
            var fetch = await _client.ListSubjectsAsync().ConfigureAwait(true);
            SubjectList.ItemsSource = fetch.Subjects;
            PaperList.ItemsSource = null;
            PaperBody.Text = "";
            if (!string.IsNullOrWhiteSpace(fetch.Problem))
            {
                SetStatus(fetch.Problem);
                return;
            }

            SetStatus(fetch.Subjects.Count == 0
                ? "No research subjects yet — she opens them during R&R."
                : $"{fetch.Subjects.Count} subject(s).");
        }
        finally
        {
            _busy = false;
        }
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e) =>
        await ReloadAsync().ConfigureAwait(true);

    private async void SubjectList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SubjectList.SelectedItem is not ResearchSubjectRow subject)
            return;
        await LoadPapersAsync(subject).ConfigureAwait(true);
    }

    private async Task LoadPapersAsync(ResearchSubjectRow subject)
    {
        if (_busy)
            return;
        _busy = true;
        try
        {
            SetStatus($"Loading papers for {subject.Title}…");
            var fetch = await _client.ListPapersAsync(subject.Id).ConfigureAwait(true);
            PapersHeader.Text = string.IsNullOrWhiteSpace(fetch.SubjectTitle)
                ? "Papers"
                : $"Papers — {fetch.SubjectTitle}";
            PaperList.ItemsSource = fetch.Papers;
            PaperBody.Text = "";
            if (!string.IsNullOrWhiteSpace(fetch.Problem))
            {
                SetStatus(fetch.Problem);
                return;
            }

            SetStatus(fetch.Papers.Count == 0
                ? $"No papers in “{subject.Title}” yet."
                : $"{fetch.Papers.Count} paper(s) in “{subject.Title}”.");
        }
        finally
        {
            _busy = false;
        }
    }

    private async void PaperList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PaperList.SelectedItem is not ResearchPaperRow row)
            return;
        if (_busy)
            return;
        _busy = true;
        try
        {
            var fetch = await _client.GetPaperAsync(row.Id).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(fetch.Problem))
            {
                SetStatus(fetch.Problem);
                return;
            }

            var paper = fetch.Paper ?? row;
            PaperBody.Text =
                $"{paper.Title}\n({paper.Kind} · {paper.Status})\n\n" +
                $"Abstract\n{paper.Abstract}\n\n" +
                $"Body\n{paper.BodyApa}\n\n" +
                $"References\n{paper.BibliographyApa}";
            SetStatus($"Opened paper #{paper.Id}.");
        }
        finally
        {
            _busy = false;
        }
    }

    private void SetStatus(string text) => StatusText.Text = text;

    public void Dispose() => _client.Dispose();
}
