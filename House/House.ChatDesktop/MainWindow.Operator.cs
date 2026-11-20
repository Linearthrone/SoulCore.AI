using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using House.ChatDesktop.Controls;
using House.ChatDesktop.Services;

namespace House.ChatDesktop;

/// <summary>PROP-22.8 — operator work items (task | project) and the weekly agenda calendar.</summary>
public partial class MainWindow
{
    private readonly SoulCoreOperatorWorkClient _operatorWork = new();
    private readonly SoulCoreAgendaClient _agenda = new();
    private bool _workSaveBusy;
    private bool _agendaBusy;

    private void InitOperatorScreens()
    {
        AgendaWeekdayCombo.ItemsSource = SoulCoreAgendaClient.WeekdayNames;
        AgendaWeekdayCombo.SelectedIndex = SoulCoreAgendaClient.WeekdayIndex(DateTime.Now);
        ApplyWorkFormKind();
    }

    private void DisposeOperatorClients()
    {
        _operatorWork.Dispose();
        _agenda.Dispose();
    }

    // ── Tasks: Yours ─────────────────────────────────────────────────────────────────────────────

    private async Task RefreshOperatorWorkAsync()
    {
        OperatorWorkStatusText.Text = "Loading…";
        var fetch = await _operatorWork.ListAsync().ConfigureAwait(true);
        OperatorWorkList.ItemsSource = fetch.Items;
        OperatorWorkStatusText.Text = !string.IsNullOrWhiteSpace(fetch.Problem)
            ? fetch.Problem
            : fetch.Items.Count == 0
                ? "Nothing here yet. Create New to hand her a task or a project."
                : $"{fetch.Items.Count} open";
    }

    private async void WorkRefresh_Click(object? sender, RoutedEventArgs e)
    {
        await RefreshOperatorWorkAsync().ConfigureAwait(true);
        await RefreshBandTasksAsync().ConfigureAwait(true);
    }

    private async void WorkDelete_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not OperatorWorkItem item)
            return;

        var ok = await ConfirmDialogWindow.ShowAsync(
            this,
            "Remove item",
            $"Remove {item.KindLabel.ToLowerInvariant()} \"{item.Title}\"?",
            confirmText: "Remove").ConfigureAwait(true);
        if (!ok)
            return;

        var result = await _operatorWork.DeleteAsync(item.Id).ConfigureAwait(true);
        if (!result.Ok)
            OperatorWorkStatusText.Text = result.Problem ?? "Could not remove that item.";
        else
            await RefreshOperatorWorkAsync().ConfigureAwait(true);
    }

    // ── Work item form ───────────────────────────────────────────────────────────────────────────

    private void WorkCreateNew_Click(object? sender, RoutedEventArgs e)
    {
        WorkKindProjectToggle.IsChecked = false;
        WorkTitleBox.Text = "";
        WorkDescriptionBox.Text = "";
        WorkDetailsBox.Text = "";
        WorkDeadlineDate.SelectedDate = null;
        WorkDeadlineTime.Text = "17:00";
        WorkPriorityBox.Value = 3;
        WorkNoCheckinCheck.IsChecked = false;
        WorkNeedsPermsCheck.IsChecked = false;
        WorkOutsideCollabCheck.IsChecked = false;
        WorkPhasesBox.Text = "";
        WorkDeliverablesBox.Text = "";
        WorkFormStatusText.Text = "";
        ApplyWorkFormKind();
        ShowScreen(Screen.WorkForm);
        WorkTitleBox.Focus();
    }

    private void WorkKindToggle_Changed(object? sender, RoutedEventArgs e) => ApplyWorkFormKind();

    /// <summary>Phase checkpoints and deliverables are project-only; they are disabled for a task.</summary>
    private void ApplyWorkFormKind()
    {
        if (WorkKindProjectToggle is null)
            return;

        var project = WorkKindProjectToggle.IsChecked == true;
        WorkPhasesBox.IsEnabled = project;
        WorkDeliverablesBox.IsEnabled = project;
        WorkPhasesBox.Opacity = project ? 1.0 : 0.45;
        WorkDeliverablesBox.Opacity = project ? 1.0 : 0.45;
        WorkSaveButton.Content = project ? "Create project" : "Create task";
    }

    private static IReadOnlyList<string> SplitLines(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? Array.Empty<string>()
            : text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private async void WorkSave_Click(object? sender, RoutedEventArgs e)
    {
        if (_workSaveBusy)
            return;

        var title = (WorkTitleBox.Text ?? "").Trim();
        if (title.Length == 0)
        {
            WorkFormStatusText.Text = "A title is required.";
            return;
        }

        DateTimeOffset? deadline = null;
        if (WorkDeadlineDate.SelectedDate is { } date)
        {
            var timeText = (WorkDeadlineTime.Text ?? "").Trim();
            if (!TimeSpan.TryParseExact(timeText, "h\\:mm", CultureInfo.InvariantCulture, out var time)
                && !TimeSpan.TryParseExact(timeText, "hh\\:mm", CultureInfo.InvariantCulture, out time))
            {
                WorkFormStatusText.Text = "Deadline time must be HH:mm.";
                return;
            }

            var local = DateTime.SpecifyKind(date.Date + time, DateTimeKind.Local);
            deadline = new DateTimeOffset(local);
        }

        var project = WorkKindProjectToggle.IsChecked == true;
        var priority = (int)Math.Clamp(WorkPriorityBox.Value ?? 3m, 1m, 5m);

        var draft = new OperatorWorkItemDraft
        {
            IsProject = project,
            Title = title,
            Description = Normalize(WorkDescriptionBox.Text),
            Details = Normalize(WorkDetailsBox.Text),
            Deadline = deadline,
            Priority = priority,
            NoCheckin = WorkNoCheckinCheck.IsChecked == true,
            NeedsExtraPermissions = WorkNeedsPermsCheck.IsChecked == true,
            OutsideCollaboration = WorkOutsideCollabCheck.IsChecked == true,
            PhaseCheckpoints = project ? SplitLines(WorkPhasesBox.Text) : Array.Empty<string>(),
            Deliverables = project ? SplitLines(WorkDeliverablesBox.Text) : Array.Empty<string>()
        };

        _workSaveBusy = true;
        WorkSaveButton.IsEnabled = false;
        WorkFormStatusText.Text = "Saving…";
        try
        {
            var result = await _operatorWork.CreateAsync(draft).ConfigureAwait(true);
            if (!result.Ok)
            {
                WorkFormStatusText.Text = result.Problem ?? "Could not save.";
                return;
            }

            _performanceLog.Add($"Created {(project ? "project" : "task")} \"{title}\" (#{result.Id})");
            ShowScreen(Screen.Tasks);
        }
        finally
        {
            _workSaveBusy = false;
            WorkSaveButton.IsEnabled = true;
        }
    }

    private static string? Normalize(string? text)
    {
        var t = text?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    // ── Calendar (Mon–Sun week) ──────────────────────────────────────────────────────────────────

    private void CalendarRefresh_Click(object? sender, RoutedEventArgs e) => _ = RefreshAgendaAsync();

    private async Task RefreshAgendaAsync()
    {
        var today = DateTime.Now.Date;
        var start = SoulCoreAgendaClient.WeekStart(today);
        CalendarWeekText.Text =
            $"{start.ToString("ddd MMM d", CultureInfo.CurrentCulture)} – " +
            $"{start.AddDays(6).ToString("ddd MMM d", CultureInfo.CurrentCulture)}";

        CalendarStatusText.Text = "Loading…";
        var fetch = await _agenda.ListAsync().ConfigureAwait(true);
        CalendarStatusText.Text = fetch.Problem ?? "";

        CalendarDaysPanel.Children.Clear();
        for (var i = 0; i < 7; i++)
        {
            var day = start.AddDays(i);
            var items = fetch.Items
                .Where(a => a.Weekday == i)
                .OrderBy(a => a.TimeLocal, StringComparer.Ordinal)
                .ToList();
            CalendarDaysPanel.Children.Add(BuildDayCard(day, i, day == today, items));
        }
    }

    private Control BuildDayCard(DateTime day, int weekdayIndex, bool isToday, IReadOnlyList<AgendaItem> items)
    {
        var header = new TextBlock
        {
            Text = $"{SoulCoreAgendaClient.WeekdayNames[weekdayIndex]} {day.Day}",
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            Foreground = isToday ? Res("AccentBrush") : Res("TextBrush")
        };

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(header);

        if (items.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "—",
                FontSize = 11,
                Foreground = Res("MutedBrush")
            });
        }

        foreach (var item in items)
        {
            var remove = new Button
            {
                Content = "Remove",
                Tag = item.Id,
                FontSize = 10,
                Padding = new Thickness(8, 1),
                VerticalAlignment = VerticalAlignment.Top
            };
            remove.Classes.Add("chrome");
            remove.Click += AgendaRemove_Click;

            var text = new StackPanel { Spacing = 1 };
            text.Children.Add(new TextBlock
            {
                Text = $"{item.TimeLocal}  {item.Title}",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
            if (!string.IsNullOrWhiteSpace(item.Notes))
            {
                text.Children.Add(new TextBlock
                {
                    Text = item.Notes,
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Res("MutedBrush")
                });
            }

            var row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            row.Children.Add(text);
            stack.Children.Add(row);
        }

        var card = new Border { Child = stack };
        card.Classes.Add("card");
        return card;
    }

    private async void AgendaRemove_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not long id)
            return;
        var result = await _agenda.DeleteAsync(id).ConfigureAwait(true);
        if (!result.Ok)
            CalendarStatusText.Text = result.Problem ?? "Could not remove that item.";
        await RefreshAgendaAsync().ConfigureAwait(true);
    }

    private async void AgendaAdd_Click(object? sender, RoutedEventArgs e)
    {
        if (_agendaBusy)
            return;

        var title = (AgendaTitleBox.Text ?? "").Trim();
        if (title.Length == 0)
        {
            CalendarStatusText.Text = "A title is required.";
            return;
        }

        var time = (AgendaTimeBox.Text ?? "").Trim();
        if (!SoulCoreAgendaClient.IsValidTimeLocal(time))
        {
            CalendarStatusText.Text = "Time must be HH:mm (24-hour).";
            return;
        }

        var weekday = Math.Max(0, AgendaWeekdayCombo.SelectedIndex);

        _agendaBusy = true;
        try
        {
            var result = await _agenda.CreateAsync(title, weekday, time, Normalize(AgendaNotesBox.Text))
                .ConfigureAwait(true);
            if (!result.Ok)
            {
                CalendarStatusText.Text = result.Problem ?? "Could not add that.";
                return;
            }

            AgendaTitleBox.Text = "";
            AgendaNotesBox.Text = "";
            await RefreshAgendaAsync().ConfigureAwait(true);
        }
        finally
        {
            _agendaBusy = false;
        }
    }
}
