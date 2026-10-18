using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using House.ChatDesktop.Services;

namespace House.ChatDesktop;

/// <summary>Table of research/hobby subject journals and APA papers.</summary>
public partial class ResearchJournalsWindow : Window
{
    private readonly SoulCoreResearchClient _client;
    private bool _busy;

    public ResearchJournalsWindow() : this(new SoulCoreResearchClient())
    {
    }

    public ResearchJournalsWindow(SoulCoreResearchClient client)
    {
        _client = client;
        InitializeComponent();
        Opened += async (_, _) => await ReloadSubjectsAsync().ConfigureAwait(true);
        Closed += (_, _) => _client.Dispose();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e) =>
        await ReloadSubjectsAsync().ConfigureAwait(true);

    private async Task ReloadSubjectsAsync()
    {
        if (_busy)
            return;
        _busy = true;
        try
        {
            SetStatus("Loading subjects…");
            var fetch = await _client.ListSubjectsAsync().ConfigureAwait(true);
            if (SubjectList is not null)
                SubjectList.ItemsSource = fetch.Subjects;
            if (PaperList is not null)
                PaperList.ItemsSource = null;
            if (PaperBody is not null)
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

    private async void SubjectList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SubjectList?.SelectedItem is not ResearchSubjectRow subject)
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
            if (PapersHeader is not null)
                PapersHeader.Text = string.IsNullOrWhiteSpace(fetch.SubjectTitle)
                    ? "Papers"
                    : $"Papers — {fetch.SubjectTitle}";
            if (PaperList is not null)
                PaperList.ItemsSource = fetch.Papers;
            if (PaperBody is not null)
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
        if (PaperList?.SelectedItem is not ResearchPaperRow row)
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
            if (PaperBody is not null)
            {
                PaperBody.Text =
                    $"{paper.Title}\n({paper.Kind} · {paper.Status})\n\n" +
                    $"Abstract\n{paper.Abstract}\n\n" +
                    $"Body\n{paper.BodyApa}\n\n" +
                    $"References\n{paper.BibliographyApa}";
            }

            SetStatus($"Opened paper #{paper.Id}.");
        }
        finally
        {
            _busy = false;
        }
    }

    private void SetStatus(string text)
    {
        if (StatusText is not null)
            StatusText.Text = text;
    }
}
