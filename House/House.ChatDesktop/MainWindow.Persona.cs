using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using House.ChatDesktop.Models;
using House.ChatDesktop.Services;

namespace House.ChatDesktop;

public partial class MainWindow
{
    private readonly SoulCorePersonaClient _personas = new();
    private readonly List<Bitmap> _threadBitmaps = new();
    private bool _personaSwitcherHydrating;
    private bool _threadListHydrating;
    private bool _threadArchiveWired;
    private bool _archiveInFlight;
    private PresenceThreadRow? _archivePressRow;
    private string? _activePersonaId;
    private string _activePersonaDisplayName = "Companion";
    private string? _openThreadPersonaId;
    private readonly PersonaModeBoard _modeBoard = new();
    private readonly ForegroundChromeNames _foregroundNames = new("Companion");
    private bool _modePosting;
    private bool? _personaApiOk;
    private List<PersonaPackInfo> _knownPersonas = new();

    private async Task RefreshPersonasAsync()
    {
        var snap = await _personas.ListAsync().ConfigureAwait(true);
        if (!snap.Reachable)
        {
            ClearPersonaSwitcherItems();
            _personaApiOk = null;
            SetPersonaStatus(snap.Detail ?? "Host unreachable", warn: true);
            RefreshBuildVersionChrome(_lastHostVersion);
            return;
        }

        if (snap.PersonaApiMissing)
        {
            ClearPersonaSwitcherItems();
            _personaApiOk = false;
            SetPersonaStatus(
                snap.Detail ?? BuildCurrencyStatus.HostNoPersonaApiMessage,
                warn: true);
            RefreshBuildVersionChrome(_lastHostVersion);
            return;
        }

        _personaApiOk = true;
        ApplyPersonaList(snap);
        RefreshBuildVersionChrome(_lastHostVersion);
    }

    private void ClearPersonaSwitcherItems()
    {
        if (PersonaSwitcher is null)
            return;

        _personaSwitcherHydrating = true;
        try
        {
            PersonaSwitcher.ItemsSource = null;
            PersonaSwitcher.SelectedItem = null;
        }
        finally
        {
            _personaSwitcherHydrating = false;
        }
    }

    private void SetPersonaStatus(string? text, bool warn)
    {
        var brush = warn
            ? (_badBrush ?? _warnBrush ?? _mutedBrush)
            : _mutedBrush;
        foreach (var tb in new[] { PersonaChromeStatusText, PersonaSwitcherStatusText })
        {
            if (tb is null)
                continue;
            tb.Text = text ?? "";
            tb.IsVisible = !string.IsNullOrWhiteSpace(text);
            if (brush is not null)
                tb.Foreground = brush;
        }
    }

    private void ApplyPersonaList(PersonaListSnapshot snap)
    {
        _knownPersonas = snap.Personas.ToList();
        RefreshGlassLists();
        if (PersonaSwitcher is null)
            return;

        _personaSwitcherHydrating = true;
        try
        {
            var items = snap.Personas.ToList();
            PersonaSwitcher.ItemsSource = items;

            var activeId = snap.ActivePersonaId;
            PersonaPackInfo? active = null;
            if (!string.IsNullOrWhiteSpace(activeId))
            {
                active = items.FirstOrDefault(p =>
                    string.Equals(p.PersonaId, activeId, StringComparison.OrdinalIgnoreCase));
            }

            active ??= items.FirstOrDefault(p => p.IsActive) ?? items.FirstOrDefault();
            PersonaSwitcher.SelectedItem = active;

            if (active is not null)
                ApplyActivePersonaToShell(active.PersonaId, active.DisplayName, syncLocalName: true);
            else
                ApplyActivePersonaToShell(activeId, "Companion", syncLocalName: false);

            if (items.Count == 0)
            {
                SetPersonaStatus("No persona packs on Host", warn: false);
            }
            else
            {
                var name = active?.DisplayName ?? "none";
                SetPersonaStatus($"{items.Count} pack(s) · active: {name}", warn: false);
            }
        }
        finally
        {
            _personaSwitcherHydrating = false;
        }
    }

    private async void PersonaSwitcher_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_personaSwitcherHydrating || PersonaSwitcher is null)
            return;
        if (PersonaSwitcher.SelectedItem is not PersonaPackInfo selected)
            return;
        await SwitchToPersonaAsync(selected).ConfigureAwait(true);
    }

    private async void ThreadList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_archivePressRow is not null)
        {
            _threadListHydrating = true;
            try
            {
                RestoreActiveThreadSelection();
            }
            finally
            {
                _threadListHydrating = false;
            }

            return;
        }

        if (_threadListHydrating || ThreadList?.SelectedItem is not PresenceThreadRow row)
            return;

        var focus = ThreadFocus.Resolve(_modeBoard.Displayed, _activePersonaId, row.PersonaId);
        if (!focus.Activate)
        {
            OpenThreadWithoutActivating(row.PersonaId, row.DisplayName, showChat: false);
            return;
        }

        var pack = _knownPersonas.FirstOrDefault(persona =>
            string.Equals(persona.PersonaId, row.PersonaId, StringComparison.OrdinalIgnoreCase));
        if (pack is null)
            return;
        await SwitchToPersonaAsync(pack).ConfigureAwait(true);
    }

    private async Task SwitchToPersonaAsync(PersonaPackInfo selected)
    {
        if (string.Equals(selected.PersonaId, _activePersonaId, StringComparison.OrdinalIgnoreCase))
            return;

        var previousId = _activePersonaId;
        var previousItem = FindPersonaItem(previousId);

        var confirmed = await ConfirmPersonaQuarantineAsync(selected).ConfigureAwait(true);
        var plan = ContactsOpenChat.Resolve(alreadyForeground: false, quarantineConfirmed: confirmed);
        if (!plan.Activate)
        {
            _personaSwitcherHydrating = true;
            try
            {
                if (PersonaSwitcher is not null)
                    PersonaSwitcher.SelectedItem = previousItem;
            }
            finally
            {
                _personaSwitcherHydrating = false;
            }

            RefreshThreadList();
            return;
        }

        var result = await _personas.ActivateAsync(selected.PersonaId).ConfigureAwait(true);
        if (!result.Ok || result.Persona is null)
        {
            var detail = result.Detail ?? "Host rejected activate";
            if (detail.Contains("404", StringComparison.Ordinal)
                || detail.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
            {
                detail = BuildCurrencyStatus.HostNoPersonaApiMessage;
                _personaApiOk = false;
                RefreshBuildVersionChrome(_lastHostVersion);
            }

            AppendSystemNotice($"Persona switch failed: {detail}");
            SetPersonaStatus(detail, warn: true);
            _personaSwitcherHydrating = true;
            try
            {
                if (PersonaSwitcher is not null)
                    PersonaSwitcher.SelectedItem = previousItem;
            }
            finally
            {
                _personaSwitcherHydrating = false;
            }

            RefreshThreadList();
            return;
        }

        ApplyActivePersonaToShell(
            result.Persona.PersonaId,
            result.Persona.DisplayName,
            syncLocalName: true);

        AppendSystemNotice(
            $"Active persona → {result.Persona.DisplayName} ({result.Persona.PersonaId}). " +
            "Memories stay quarantined; identity applies on the next chat turn.");

        SetPersonaStatus(
            result.Note ?? $"Active: {result.Persona.DisplayName}",
            warn: false);
        if (plan.ShowDrawer)
            ShowChatDrawer();
    }

    private async Task OpenFavoriteAsync(FavoriteSlot slot)
    {
        var focus = ThreadFocus.Resolve(_modeBoard.Displayed, _activePersonaId, slot.PersonaId);
        if (!focus.Activate)
        {
            OpenThreadWithoutActivating(slot.PersonaId, slot.DisplayName, showChat: true);
            return;
        }

        var pack = _knownPersonas.FirstOrDefault(persona =>
            string.Equals(persona.PersonaId, slot.PersonaId, StringComparison.OrdinalIgnoreCase));
        if (pack is null)
        {
            ApplyActivePersonaToShell(slot.PersonaId, slot.DisplayName, syncLocalName: false);
            ShowChatDrawer();
            AppendSystemNotice(
                $"Opened the local transcript for {slot.DisplayName}. Host did not list this persona.");
            return;
        }

        await SwitchToPersonaAsync(pack).ConfigureAwait(true);
        if (string.Equals(slot.PersonaId, _activePersonaId, StringComparison.OrdinalIgnoreCase))
            ShowChatDrawer();
    }

    private void RefreshThreadList()
    {
        if (ThreadList is null)
            return;

        EnsureThreadArchiveWired();

        foreach (var bitmap in _threadBitmaps)
            bitmap.Dispose();
        _threadBitmaps.Clear();

        var rows = new List<PresenceThreadRow>();
        foreach (var persona in _knownPersonas)
        {
            if (!_chatHistory.IsThreadListed(ChatTranscriptKey.ForPersona(persona.PersonaId)))
                continue;

            Bitmap? photo = null;
            var path = _uiSettings.Favorites.FirstOrDefault(slot =>
                string.Equals(slot.PersonaId, persona.PersonaId, StringComparison.OrdinalIgnoreCase))?.PhotoPath;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    photo = new Bitmap(path);
                    _threadBitmaps.Add(photo);
                }
                catch
                {
                    photo = null;
                }
            }

            rows.Add(new PresenceThreadRow
            {
                PersonaId = persona.PersonaId,
                DisplayName = string.IsNullOrWhiteSpace(persona.DisplayName) ? persona.PersonaId : persona.DisplayName,
                Initials = FavoriteRoster.InitialsFor(persona.DisplayName),
                Photo = photo
            });
        }

        _threadListHydrating = true;
        try
        {
            ThreadList.ItemsSource = rows;
            ThreadList.SelectedItem = rows.FirstOrDefault(row =>
                string.Equals(row.PersonaId, TranscriptPersonaId(), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _threadListHydrating = false;
        }
    }

    private void EnsureThreadArchiveWired()
    {
        if (_threadArchiveWired || ThreadList is null)
            return;

        _threadArchiveWired = true;
        ThreadList.AddHandler(PointerPressedEvent, ThreadList_ArchivePressed, RoutingStrategies.Tunnel);
        ThreadList.AddHandler(PointerReleasedEvent, ThreadList_ArchiveReleased, RoutingStrategies.Tunnel);
        ThreadList.AddHandler(PointerCaptureLostEvent, ThreadList_ArchiveCaptureLost, RoutingStrategies.Tunnel);
    }

    private void ThreadList_ArchivePressed(object? sender, PointerPressedEventArgs e)
    {
        _archivePressRow = ArchiveRowFromSource(e.Source);
    }

    private void ThreadList_ArchiveReleased(object? sender, PointerReleasedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => _archivePressRow = null, DispatcherPriority.Background);
    }

    private void ThreadList_ArchiveCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        Dispatcher.UIThread.Post(() => _archivePressRow = null, DispatcherPriority.Background);
    }

    private static PresenceThreadRow? ArchiveRowFromSource(object? source)
    {
        var visual = source as Visual;
        while (visual is not null)
        {
            if (visual is Button { Tag: string tag, DataContext: PresenceThreadRow row }
                && string.Equals(tag, "archive-thread", StringComparison.Ordinal))
            {
                return row;
            }

            visual = visual.GetVisualParent();
        }

        return null;
    }

    private void RestoreActiveThreadSelection()
    {
        if (ThreadList?.ItemsSource is not IEnumerable<PresenceThreadRow> rows)
            return;

        ThreadList.SelectedItem = rows.FirstOrDefault(item =>
            string.Equals(item.PersonaId, TranscriptPersonaId(), StringComparison.OrdinalIgnoreCase));
    }

    private async void ArchiveThread_Click(object? sender, RoutedEventArgs e)
    {
        if (_archiveInFlight)
            return;
        if (sender is not Button { DataContext: PresenceThreadRow row })
            return;
        if (string.IsNullOrWhiteSpace(row.PersonaId))
            return;

        _archiveInFlight = true;
        try
        {
            string? failure = null;
            var outcome = await ThreadArchive.ExecuteAsync(
                _chatHistory,
                row.PersonaId,
                async (summary, ct) =>
                {
                    var posted = await _personas.ArchiveAsync(row.PersonaId, summary, ct).ConfigureAwait(true);
                    if (!posted.Ok)
                    {
                        failure = string.IsNullOrWhiteSpace(posted.Detail)
                            ? "Host rejected the archive."
                            : posted.Detail;
                    }

                    return posted.Ok;
                }).ConfigureAwait(true);

            if (!outcome.Hidden)
            {
                SetPersonaStatus(
                    $"Archive failed for {row.DisplayName}. {failure ?? "Thread left visible."}",
                    warn: true);
                return;
            }

            SetPersonaStatus($"Archived {row.DisplayName}.", warn: false);
            var key = ChatTranscriptKey.ForPersona(row.PersonaId);
            if (string.Equals(key, _loadedTranscriptKey, StringComparison.Ordinal))
                LoadChatHistory();
            RefreshThreadList();
        }
        finally
        {
            _archiveInFlight = false;
        }
    }

    private PersonaPackInfo? FindPersonaItem(string? personaId)
    {
        if (PersonaSwitcher?.ItemsSource is not IEnumerable<PersonaPackInfo> items
            || string.IsNullOrWhiteSpace(personaId))
        {
            return null;
        }

        return items.FirstOrDefault(p =>
            string.Equals(p.PersonaId, personaId, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyActivePersonaToShell(string? personaId, string? displayName, bool syncLocalName)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? "Companion" : displayName.Trim();
        _activePersonaId = string.IsNullOrWhiteSpace(personaId) ? null : personaId.Trim();
        _activePersonaDisplayName = name;
        _openThreadPersonaId = _activePersonaId;
        ChatMessage.AssistantDisplayName = name;
        ApplyForegroundChrome(name);

        if (IdentityNameText is not null)
            IdentityNameText.Text = name;

        if (ChatInput is not null)
            ChatInput.Watermark = $"Message {name}…";

        if (syncLocalName
            && !string.Equals(_uiSettings.DisplayName, name, StringComparison.Ordinal))
        {
            _uiSettings.DisplayName = name;
            _uiSettings.Save();
            if (DisplayNameBox is not null)
                DisplayNameBox.Text = name;
        }

        UpdateIdentityDetail();
        var transcriptKey = ChatTranscriptKey.ForPersona(TranscriptPersonaId());
        if (!string.Equals(transcriptKey, _loadedTranscriptKey, StringComparison.Ordinal))
            LoadChatHistory();
        RefreshThreadList();

        // Refresh assistant role labels on existing bubbles.
        foreach (var msg in _messages)
        {
            if (msg.IsAssistant)
                msg.NotifyDisplayRoleChanged();
        }
    }

    private string? TranscriptPersonaId() =>
        string.IsNullOrWhiteSpace(_openThreadPersonaId) ? _activePersonaId : _openThreadPersonaId;

    private void OpenThreadWithoutActivating(string? personaId, string? displayName, bool showChat)
    {
        var id = string.IsNullOrWhiteSpace(personaId) ? null : personaId.Trim();
        var name = string.IsNullOrWhiteSpace(displayName) ? (id ?? "Companion") : displayName.Trim();
        var names = _foregroundNames.NamesAfterFocus(_modeBoard.Displayed, id, _activePersonaId, name);
        PaintForeground(names.House, names.Viewport);
        _openThreadPersonaId = id;
        if (ChatInput is not null)
            ChatInput.Watermark = $"Message {name}…";

        var key = ChatTranscriptKey.ForPersona(id);
        if (!string.Equals(key, _loadedTranscriptKey, StringComparison.Ordinal))
            LoadChatHistory();
        RefreshThreadList();
        if (showChat)
            ShowChatDrawer();
    }

    private void ApplyForegroundChrome(string? name)
    {
        _foregroundNames.ApplyForeground(name);
        PaintForeground(_foregroundNames.House, _foregroundNames.Viewport);
    }

    private void PaintForeground(string house, string viewport)
    {
        if (HouseForegroundName is not null)
            HouseForegroundName.Text = house;
        if (DesktopViewPersonaName is not null)
            DesktopViewPersonaName.Text = viewport;
        _viewport?.SetForegroundPersona(viewport);
        if (_houseWindow is not null)
            _houseWindow.Title = ForegroundChromeNames.HouseTitle(house);
        if (_desktopPopOut is not null)
            _desktopPopOut.Title = ForegroundChromeNames.ViewportTitle(viewport);
    }

    private void OnPersonaModeChanged()
    {
        void Apply()
        {
            var mode = _modeBoard.Displayed;
            SystemPersonaMode?.Apply(mode);
            _radialWindow?.ApplyMode(mode);
            if (ThreadModeMark is not null)
            {
                ThreadModeMark.Text = PersonaChatMode.Label(mode).ToUpperInvariant();
                ToolTip.SetTip(
                    ThreadModeMark,
                    PersonaChatMode.IsMany(mode)
                        ? "Many sends the open thread. HOUSE and the viewport stay on the foreground."
                        : "One sends only the foreground persona.");
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    private async Task ChoosePersonaModeAsync(string? mode)
    {
        if (_modePosting)
            return;
        if (!_modeBoard.TryBeginChoice(mode, out var previous))
            return;

        _modePosting = true;
        try
        {
            var result = await _personas.SetModeAsync(_modeBoard.Displayed).ConfigureAwait(true);
            if (!result.Ok)
            {
                _modeBoard.Restore(previous);
                AppendSystemNotice(
                    "Chat mode was not changed. Both controls stay on " + PersonaChatMode.Label(previous) + ".");
                return;
            }

            _modeBoard.Confirm(result.Mode);
        }
        finally
        {
            _modePosting = false;
        }
    }

    private async Task RefreshPersonaModeAsync()
    {
        var snap = await _personas.GetModeAsync().ConfigureAwait(true);
        _modeBoard.ApplyGet(snap.Ok, snap.Mode);
    }

    private async Task<bool> ConfirmPersonaQuarantineAsync(PersonaPackInfo selected)
    {
        var light = GlassChrome.IsLightTheme(_uiSettings.Theme);
        var dialog = new Window
        {
            Title = "Confirm persona switch",
            Width = 440,
            Height = 260,
            MinWidth = 360,
            MinHeight = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Topmost = true,
            ShowInTaskbar = !GlassShellState.HotkeysArmed,
            Background = new SolidColorBrush(GlassChrome.OpaqueBackground(light)),
            Foreground = Res("TextBrush"),
            TransparencyLevelHint = [WindowTransparencyLevel.None],
            Opacity = 1,
            SystemDecorations = SystemDecorations.BorderOnly
        };

        var message = new TextBlock
        {
            Text = PersonaQuarantineConfirm.BuildMessage(selected.DisplayName, selected.PersonaId),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var cancel = new Button
        {
            Content = "Cancel",
            MinWidth = 88,
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancel.Classes.Add("ghost");
        var confirm = new Button
        {
            Content = "Switch",
            MinWidth = 88
        };
        confirm.Classes.Add("primary");

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, confirm }
        };

        dialog.Content = new Border
        {
            Padding = new Thickness(20),
            Child = new DockPanel
            {
                Children =
                {
                    buttons,
                    message
                }
            }
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        var tcs = new TaskCompletionSource<bool>();
        cancel.Click += (_, _) =>
        {
            tcs.TrySetResult(false);
            dialog.Close();
        };
        confirm.Click += (_, _) =>
        {
            tcs.TrySetResult(true);
            dialog.Close();
        };
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        Window owner = this;
        if (!IsVisible && _radialWindow is { IsVisible: true })
            owner = _radialWindow;
        if (!owner.IsVisible)
            owner.Show();

        using (_viewportPolicy.Open(ViewportDialogKind.QuarantineConfirm))
        {
            await dialog.ShowDialog(owner).ConfigureAwait(true);
        }

        return await tcs.Task.ConfigureAwait(true);
    }

    private async void PersonaRefresh_Click(object? sender, RoutedEventArgs e) =>
        await RefreshPersonasAsync().ConfigureAwait(true);

    private async void PersonaCreate_Click(object? sender, RoutedEventArgs e) =>
        await OpenPersonaWizardAsync(editTarget: null).ConfigureAwait(true);

    private async void PersonaEdit_Click(object? sender, RoutedEventArgs e)
    {
        var selected = PersonaSwitcher?.SelectedItem as PersonaPackInfo
            ?? FindPersonaItem(_activePersonaId);
        if (selected is null)
        {
            AppendSystemNotice("Select a persona in the title-bar switcher before editing.");
            return;
        }

        // Reload full pack (traits/charter) before edit.
        var get = await _personas.GetAsync(selected.PersonaId).ConfigureAwait(true);
        if (!get.Ok || get.Persona is null)
        {
            AppendSystemNotice($"Could not load persona for edit: {get.Detail ?? "Host error"}");
            return;
        }

        await OpenPersonaWizardAsync(get.Persona).ConfigureAwait(true);
    }

    private async Task OpenPersonaWizardAsync(PersonaPackInfo? editTarget, bool fromRadial = false)
    {
        var list = await _personas.ListAsync().ConfigureAwait(true);
        if (!list.Reachable || list.PersonaApiMissing)
        {
            var notice = !list.Reachable
                ? $"Persona Host unreachable: {list.Detail ?? "no detail"}"
                : (list.Detail ?? BuildCurrencyStatus.HostNoPersonaApiMessage);
            AppendSystemNotice(notice);
            if (list.PersonaApiMissing)
            {
                _personaApiOk = false;
                SetPersonaStatus(notice, warn: true);
                RefreshBuildVersionChrome(_lastHostVersion);
            }

            if (fromRadial)
                _radialWindow?.ShowNotice(notice);
            return;
        }

        if (fromRadial)
            _radialWindow?.ClearNotice();

        var wizard = new PersonaWizardWindow(_personas, list.Personas, editTarget);
        bool saved;
        if (fromRadial && _radialWindow is { } radial)
        {
            using (radial.PresentAbove(wizard))
                saved = await wizard.ShowDialog<bool>(radial).ConfigureAwait(true);
        }
        else
        {
            if (!IsVisible)
                ShowChatDrawer();
            saved = await wizard.ShowDialog<bool>(this).ConfigureAwait(true);
        }
        if (!saved || wizard.SavedPersona is null)
            return;

        AppendSystemNotice(wizard.ResultNotice
            ?? PersonaWizardLogic.BuildSaveNextTurnNotice(wizard.SavedPersona.DisplayName, editTarget is null));

        await RefreshPersonasAsync().ConfigureAwait(true);

        // Keep switcher selection on the saved pack without forcing activate.
        if (PersonaSwitcher is not null)
        {
            _personaSwitcherHydrating = true;
            try
            {
                var item = FindPersonaItem(wizard.SavedPersona.PersonaId);
                if (item is not null)
                    PersonaSwitcher.SelectedItem = item;
            }
            finally
            {
                _personaSwitcherHydrating = false;
            }
        }

        if (editTarget is not null
            && string.Equals(wizard.SavedPersona.PersonaId, _activePersonaId, StringComparison.OrdinalIgnoreCase))
        {
            ApplyActivePersonaToShell(
                wizard.SavedPersona.PersonaId,
                wizard.SavedPersona.DisplayName,
                syncLocalName: true);
        }
    }
}
