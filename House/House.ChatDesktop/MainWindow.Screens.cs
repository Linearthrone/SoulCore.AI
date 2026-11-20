using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using House.ChatDesktop.Services;

namespace House.ChatDesktop;

/// <summary>PROP-22.4/22.7 — one in-glass screen at a time, plus the Victoria drawer and portrait card.</summary>
public partial class MainWindow
{
    private enum Screen
    {
        Chat,
        Journal,
        Activities,
        Research,
        Bond,
        Tasks,
        WorkForm,
        Calendar,
        Settings,
        Advanced,
        Log
    }

    /// <summary>Row shown in the portrait card's resting-trait list.</summary>
    internal sealed record PortraitTraitRow(string Name, double Weight, string Percent);

    private readonly SoulCoreInferenceProfilesClient _inferenceProfiles = new();
    private Screen _screen = Screen.Chat;
    private IdentitySettingsSnapshot? _lastIdentity;
    private ActivityLogRow? _lastActivityRow;
    private bool _portraitBusy;

    private Control ScreenRoot(Screen screen) =>
        screen switch
        {
            Screen.Chat => ChatScreen,
            Screen.Journal => JournalScreen,
            Screen.Activities => ActivitiesScreen,
            Screen.Research => ResearchScreen,
            Screen.Bond => BondScreen,
            Screen.Tasks => TasksScreen,
            Screen.WorkForm => WorkItemFormScreen,
            Screen.Calendar => CalendarScreen,
            Screen.Settings => SettingsScreen,
            Screen.Advanced => AdvancedSettingsScreen,
            Screen.Log => LogScreen,
            _ => throw new ArgumentOutOfRangeException(nameof(screen), screen, null)
        };

    private void ShowScreen(Screen screen)
    {
        foreach (var s in Enum.GetValues<Screen>())
            ScreenRoot(s).IsVisible = s == screen;

        _screen = screen;
        CloseVictoriaDrawer();
        ClosePortraitCard();

        switch (screen)
        {
            case Screen.Chat:
                ScrollTranscriptToEnd();
                break;
            case Screen.Journal:
                _ = JournalPane.ReloadAsync();
                break;
            case Screen.Activities:
                _ = ActivitiesPane.ReloadAsync();
                break;
            case Screen.Research:
                _ = ResearchPane.ReloadAsync();
                break;
            case Screen.Bond:
                UpdateIdentityDetail();
                break;
            case Screen.Tasks:
                _ = RefreshOperatorWorkAsync();
                _ = RefreshBandTasksAsync();
                break;
            case Screen.WorkForm:
                break;
            case Screen.Calendar:
                _ = RefreshAgendaAsync();
                break;
            case Screen.Settings:
                RefreshSettingsScreen();
                break;
            case Screen.Advanced:
                RefreshAdvancedScreen();
                break;
            case Screen.Log:
                RefreshLogScreen();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(screen), screen, null);
        }
    }

    private static Screen ParseScreenTag(string? tag) =>
        tag?.Trim().ToLowerInvariant() switch
        {
            "journal" => Screen.Journal,
            "activities" => Screen.Activities,
            "research" => Screen.Research,
            "bond" => Screen.Bond,
            "tasks" => Screen.Tasks,
            "calendar" => Screen.Calendar,
            "settings" => Screen.Settings,
            "advanced" => Screen.Advanced,
            _ => Screen.Chat
        };

    private void Back_Click(object? sender, RoutedEventArgs e) =>
        ShowScreen(ParseScreenTag((sender as Control)?.Tag as string));

    private void Settings_Click(object? sender, RoutedEventArgs e) =>
        ShowScreen(_screen == Screen.Settings ? Screen.Chat : Screen.Settings);

    private void DisposeScreens()
    {
        try { JournalPane.Dispose(); } catch { /* ignore */ }
        try { ActivitiesPane.Dispose(); } catch { /* ignore */ }
        try { ResearchPane.Dispose(); } catch { /* ignore */ }
        _inferenceProfiles.Dispose();
        _systemLog.Dispose();
    }

    // ── Victoria drawer ──────────────────────────────────────────────────────────────────────────

    private void Victoria_Click(object? sender, RoutedEventArgs e)
    {
        if (VictoriaDrawer.IsVisible)
        {
            CloseVictoriaDrawer();
            return;
        }

        ClosePortraitCard();
        VictoriaDrawer.IsVisible = true;
        VictoriaDrawerScrim.IsVisible = true;
        RefreshActivityBand();
        RefreshHonestyBand();
        _ = RefreshLastActivityAsync();
    }

    private void CloseVictoriaDrawer()
    {
        if (VictoriaDrawer is null)
            return;
        VictoriaDrawer.IsVisible = false;
        VictoriaDrawerScrim.IsVisible = false;
        if (EmotionCorrectPanel is not null)
            EmotionCorrectPanel.IsVisible = false;
    }

    private void VictoriaDrawerScrim_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        CloseVictoriaDrawer();
        e.Handled = true;
    }

    private void DrawerNav_Click(object? sender, RoutedEventArgs e) =>
        ShowScreen(ParseScreenTag((sender as Control)?.Tag as string));

    private async Task RefreshLastActivityAsync()
    {
        var fetch = await _activities.ListAsync(limit: 5).ConfigureAwait(true);
        _lastActivityRow = fetch.Activities.OrderByDescending(r => r.EntryNo).FirstOrDefault();

        if (_lastActivityRow is { } row)
        {
            LastActivityText.Text =
                $"{row.DateTimeText} · {row.ActivityText} · {row.SubjectText} — {row.DescText}";
            OpenJournalLink.IsVisible = row.JournalEntryId is not null;
        }
        else
        {
            LastActivityText.Text = string.IsNullOrWhiteSpace(fetch.Problem)
                ? "Nothing recorded yet."
                : fetch.Problem;
            OpenJournalLink.IsVisible = false;
        }
    }

    private async void OpenJournalLink_Click(object? sender, RoutedEventArgs e)
    {
        var row = _lastActivityRow;
        ShowScreen(Screen.Journal);
        if (row?.JournalEntryId is { } entryId)
            await JournalPane.OpenEntryAsync(row.JournalBookId, entryId).ConfigureAwait(true);
    }

    // ── Portrait card ────────────────────────────────────────────────────────────────────────────

    private async void Portrait_Click(object? sender, RoutedEventArgs e)
    {
        CloseVictoriaDrawer();
        PortraitCard.IsVisible = true;
        PortraitScrim.IsVisible = true;
        if (_lastIdentity is { } identity)
            ApplyPortraitIdentity(identity);
        else
            _ = RefreshIdentityDetailAsync();

        await LoadPortraitModelsAsync().ConfigureAwait(true);
    }

    private void ClosePortraitCard()
    {
        if (PortraitCard is null)
            return;
        PortraitCard.IsVisible = false;
        PortraitScrim.IsVisible = false;
    }

    private void PortraitClose_Click(object? sender, RoutedEventArgs e) => ClosePortraitCard();

    private void PortraitScrim_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ClosePortraitCard();
        e.Handled = true;
    }

    private async Task LoadPortraitModelsAsync()
    {
        PortraitModelStatusText.Text = "Loading models…";
        var fetch = await _inferenceProfiles.GetAsync().ConfigureAwait(true);
        PortraitModelCombo.ItemsSource = fetch.Profiles;

        var activeModel = fetch.Active?.Model;
        var match = string.IsNullOrWhiteSpace(activeModel)
            ? null
            : fetch.Profiles.FirstOrDefault(p =>
                string.Equals(p.Model, activeModel, StringComparison.OrdinalIgnoreCase));
        PortraitModelCombo.SelectedItem = match;

        PortraitApplyButton.IsEnabled = fetch.Profiles.Count > 0;
        PortraitModelStatusText.Text = !string.IsNullOrWhiteSpace(fetch.Problem)
            ? fetch.Problem
            : fetch.Active is { } a
                ? $"Active: {(string.IsNullOrWhiteSpace(a.Model) ? "none" : a.Model)}{(a.Cloud ? " · cloud" : "")}"
                : "";
    }

    private async void PortraitApply_Click(object? sender, RoutedEventArgs e)
    {
        if (_portraitBusy || PortraitModelCombo.SelectedItem is not InferenceProfile profile)
        {
            if (PortraitModelCombo.SelectedItem is null)
                PortraitModelStatusText.Text = "Pick a model first.";
            return;
        }

        _portraitBusy = true;
        PortraitApplyButton.IsEnabled = false;
        PortraitModelStatusText.Text = $"Applying {profile.Model}…";
        try
        {
            var result = await _inferenceProfiles.ApplyAsync(profile.Model, profile.Cloud).ConfigureAwait(true);
            if (result.Ok)
            {
                var active = result.Active?.Model ?? profile.Model;
                PortraitModelStatusText.Text = $"Applied · {active}";
                _performanceLog.Add($"Chat model → {active}");
                await ProbeHealthAsync().ConfigureAwait(true);
            }
            else
            {
                PortraitModelStatusText.Text = result.Problem ?? "Apply failed.";
                _performanceLog.Add($"Chat model apply failed: {result.Problem}");
            }
        }
        finally
        {
            _portraitBusy = false;
            PortraitApplyButton.IsEnabled = true;
        }
    }

    /// <summary>Called whenever Identity data refreshes: name, charter synopsis, resting traits.</summary>
    private void ApplyPortraitIdentity(IdentitySettingsSnapshot identity)
    {
        if (PortraitNameText is null)
            return;

        if (!string.IsNullOrWhiteSpace(identity.DisplayName))
            PortraitNameText.Text = identity.DisplayName.Trim();

        if (!identity.Reachable)
        {
            PortraitSynopsisText.Text = identity.Detail ?? "Host unreachable — charter unavailable.";
            PortraitTraitList.ItemsSource = Array.Empty<PortraitTraitRow>();
            PortraitTraitsEmptyText.Text = "";
            PortraitTraitsEmptyText.IsVisible = false;
            return;
        }

        var anchors = identity.IdentityAnchors.Count > 0 ? identity.IdentityAnchors : identity.Anchors;
        var pick = anchors.FirstOrDefault(a =>
                       string.Equals(a.Kind, "identity", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(a.Kind, "self", StringComparison.OrdinalIgnoreCase))
                   ?? anchors.OrderByDescending(a => a.Priority).FirstOrDefault();
        PortraitSynopsisText.Text = pick is null
            ? "No charter anchors on this Host yet."
            : Truncate(string.IsNullOrWhiteSpace(pick.Body) ? pick.Title : pick.Body, 360);

        var rows = identity.RestingTraits
            .Select(t => new PortraitTraitRow(
                string.IsNullOrWhiteSpace(t.Name) ? t.Id : t.Name.Trim(),
                Math.Clamp(t.Weight, 0, 1) * 100.0,
                (Math.Clamp(t.Weight, 0, 1) * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%"))
            .ToList();
        PortraitTraitList.ItemsSource = rows;
        PortraitTraitsEmptyText.IsVisible = rows.Count == 0;
        PortraitTraitsEmptyText.Text = rows.Count == 0 ? "No resting traits reported." : "";
    }

    private static string Truncate(string text, int max)
    {
        var t = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return t.Length <= max ? t : t[..(max - 1)] + "…";
    }
}
