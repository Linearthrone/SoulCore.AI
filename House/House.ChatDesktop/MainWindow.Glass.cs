using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using House.ChatDesktop.Drawers;
using House.ChatDesktop.Services;

namespace House.ChatDesktop;

public partial class MainWindow
{
    private PresenceHotkeyHost? _hotkeys;
    private Window? _settingsWindow;
    private Window? _houseWindow;
    private ContactsDrawerWindow? _contactsWindow;
    private RadialEdgeWindow? _radialWindow;
    private ViewportDrawer? _viewport;
    private readonly ViewportInputPolicy _viewportPolicy = new();
    private Task _cuaChain = Task.CompletedTask;
    private bool _cuaHeldForDialog;
    private bool _cuaWasOn;
    private bool _settingsDetached;
    private bool _houseDetached;
    private bool _glassChromeApplying;
    private TrayIcon? _trayIcon;

    private void InitGlassShell()
    {
        try
        {
            ApplyChromeFromSettings(persist: false);
        }
        catch (Exception ex)
        {
            PresenceStartupLog.WriteException("glass theme", ex);
        }

        try
        {
            ArmHotkeysAndTaskbar();
        }
        catch (Exception ex)
        {
            PresenceStartupLog.WriteException("glass hotkeys", ex);
        }

        try
        {
            BuildDrawers();
        }
        catch (Exception ex)
        {
            PresenceStartupLog.WriteException("glass drawers", ex);
        }

        try
        {
            BuildTray();
        }
        catch (Exception ex)
        {
            PresenceStartupLog.WriteException("glass tray", ex);
        }

        if (ChromeOpacitySlider is not null)
            ChromeOpacitySlider.PropertyChanged += ChromeOpacitySlider_PropertyChanged;

        Closing += Glass_Closing;
        Opened += Glass_Opened;
    }

    private void ArmHotkeysAndTaskbar()
    {
        _hotkeys = new PresenceHotkeyHost();
        var armed = _hotkeys.TryRegister(out var detail);
        PresenceStartupLog.Write("glass hotkeys: " + detail);
        GlassShellState.HotkeysArmed = armed && PresenceHotkeyPlan.HideFromTaskbar(armed);
        if (!GlassShellState.HotkeysArmed)
        {
            _hotkeys.Dispose();
            _hotkeys = null;
            KeyDown += Glass_KeyDown;
            return;
        }

        ShowInTaskbar = false;
        _hotkeys.Pressed += OnHotkey;
    }

    private void OnHotkey(PresenceHotkeyAction action)
    {
        if (action == PresenceHotkeyAction.ToggleChatDrawer)
            ToggleChatDrawer();
        else if (action == PresenceHotkeyAction.Exit)
            ExitGlass();
    }

    private void Glass_KeyDown(object? sender, KeyEventArgs e)
    {
        if (GlassShellState.HotkeysArmed)
            return;
        if (e.KeyModifiers != (KeyModifiers.Control | KeyModifiers.Shift))
            return;
        if (e.Key == Key.H)
        {
            e.Handled = true;
            ToggleChatDrawer();
        }
        else if (e.Key == Key.Q)
        {
            e.Handled = true;
            ExitGlass();
        }
    }

    private void BuildDrawers()
    {
        if (SettingsView is not null && GlassDrawerChrome.TryDetach(SettingsView))
        {
            SettingsView.IsVisible = true;
            _settingsWindow = GlassDrawerChrome.Host("Settings", SettingsView, 960, 720);
            _settingsDetached = true;
        }

        if (HouseDrawerHost is not null && GlassDrawerChrome.TryDetach(HouseDrawerHost))
        {
            if (ServicesPanel is not null)
                ServicesPanel.IsVisible = true;
            if (HouseDrawerTab is not null)
                ToolTip.SetTip(HouseDrawerTab, "Hide HOUSE drawer");
            _houseWindow = GlassDrawerChrome.Host("HOUSE", HouseDrawerHost, 920, 280);
            _houseDetached = true;
        }

        _contactsWindow = new ContactsDrawerWindow();
        _contactsWindow.OpenChatRequested += (_, persona) => _ = OpenPersonaFromContactsAsync(persona, edit: false);
        _contactsWindow.EditRequested += (_, persona) => _ = OpenPersonaFromContactsAsync(persona, edit: true);
        _contactsWindow.PinRequested += (_, persona) => PinFavorite(persona, null);
        _contactsWindow.PhotoRequested += (_, persona) => _ = SetFavoritePhotoAsync(persona);

        _radialWindow = new RadialEdgeWindow();
        _radialWindow.CreateAgentRequested += (_, _) => _ = OpenPersonaWizardAsync(null, fromRadial: true);
        _radialWindow.ContactsRequested += (_, _) => ShowContactsDrawer();
        _radialWindow.FavoriteRequested += (_, slot) => _ = OpenFavoriteAsync(slot);
        _radialWindow.ModeChosen += (_, mode) => _ = ChoosePersonaModeAsync(mode);
        _radialWindow.SetFavorites(_uiSettings.Favorites);
        if (SystemPersonaMode is not null)
            SystemPersonaMode.ModeChosen += (_, mode) => _ = ChoosePersonaModeAsync(mode);
        _modeBoard.Changed += OnPersonaModeChanged;
        OnPersonaModeChanged();

        _viewport = new ViewportDrawer(_viewportPolicy, _uiSettings);
        ApplyForegroundChrome(_foregroundNames.House);
        _viewportPolicy.Changed += OnViewportPolicyChanged;
    }

    private void BuildTray()
    {
        var menu = new NativeMenu();
        var openChat = new NativeMenuItem(PresenceTrayMenu.OpenChat);
        openChat.Click += (_, _) => ShowChatDrawer();
        var openSettings = new NativeMenuItem(PresenceTrayMenu.OpenSettings);
        openSettings.Click += (_, _) => ShowSettingsView();
        var exit = new NativeMenuItem(PresenceTrayMenu.Exit);
        exit.Click += (_, _) => ExitGlass();
        menu.Items.Add(openChat);
        menu.Items.Add(openSettings);
        menu.Items.Add(exit);

        WindowIcon? icon = null;
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://House.ChatDesktop/Assets/house-victoria.ico"));
            icon = new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            PresenceStartupLog.WriteException("tray icon", ex);
        }

        _trayIcon = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "SoulCore Presence",
            Menu = menu,
            IsVisible = icon is not null
        };
        if (Application.Current is { } app)
            TrayIcon.SetIcons(app, new TrayIcons { _trayIcon });
    }

    private void Glass_Opened(object? sender, EventArgs e)
    {
        _radialWindow?.Show();
        _radialWindow?.PinToLeftEdge();
        _viewport?.Show();
        if (GlassShellState.HotkeysArmed)
            Hide();
    }

    private void ShowViewport_Click(object? sender, RoutedEventArgs e)
    {
        var live = ViewportStill.LiveView();
        if (!live.FocusStillInChatDrawer || live.ParentHwnd)
            return;
        ShowPresenceView();
        ShowChatDrawer();
        ScreenPanel?.BringIntoView();
        ScreenPanel?.Focus();
    }

    private void OnViewportPolicyChanged()
    {
        try
        {
            _viewport?.ApplyPolicy();
        }
        catch (Exception ex)
        {
            PresenceStartupLog.WriteException("viewport policy", ex);
        }

        _cuaChain = RunCuaPauseAfter(_cuaChain, _viewportPolicy.CuaPaused);
    }

    private Task RunCuaPauseAfter(Task previous, bool pause)
    {
        return Await();

        async Task Await()
        {
            try
            {
                await previous.ConfigureAwait(true);
            }
            catch
            {
                // The next pause still runs.
            }

            if (pause == _cuaHeldForDialog)
                return;
            if (pause)
            {
                _cuaHeldForDialog = true;
                _cuaWasOn = _lastAllowComputerControl == true;
                if (!_cuaWasOn)
                    return;
                var snap = await _toolsSettings.PatchAsync(allowComputerControl: false).ConfigureAwait(true);
                if (snap.Reachable)
                    ApplyToolsAccess(snap, saved: true);
                return;
            }

            var restore = _cuaWasOn;
            _cuaHeldForDialog = false;
            _cuaWasOn = false;
            if (!restore)
                return;
            var back = await _toolsSettings.PatchAsync(allowComputerControl: true).ConfigureAwait(true);
            if (back.Reachable)
                ApplyToolsAccess(back, saved: true);
        }
    }

    private void Glass_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (GlassShellState.IsExiting)
            return;
        e.Cancel = true;
        Hide();
    }

    private void ShowChatDrawer()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    private void ToggleChatDrawer()
    {
        if (!IsVisible || WindowState == WindowState.Minimized)
            ShowChatDrawer();
        else
            Hide();
    }

    private void NavHouse_Click(object? sender, RoutedEventArgs e) => ShowHouseDrawer();

    private void ShowHouseDrawer()
    {
        if (_houseWindow is null)
        {
            if (ServicesPanel is not null)
                ServicesPanel.IsVisible = true;
            return;
        }

        if (ServicesPanel is not null)
            ServicesPanel.IsVisible = true;
        _houseWindow.Show();
        _houseWindow.Activate();
    }

    private void ShowContactsDrawer()
    {
        if (_contactsWindow is null)
            return;
        _contactsWindow.Bind(_knownPersonas, _uiSettings.Favorites);
        _contactsWindow.Show();
        _contactsWindow.Activate();
    }

    private void RefreshGlassLists()
    {
        _contactsWindow?.Bind(_knownPersonas, _uiSettings.Favorites);
        _radialWindow?.SetFavorites(_uiSettings.Favorites);
        RefreshThreadList();
    }

    private void ExitGlass()
    {
        if (GlassShellState.IsExiting)
            return;
        GlassShellState.IsExiting = true;
        if (_trayIcon is not null)
            _trayIcon.IsVisible = false;
        CuaLampClear.TryClearNow();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        else
            Close();
    }

    private void ThemeDark_Click(object? sender, RoutedEventArgs e)
    {
        _uiSettings.Theme = GlassChrome.DarkTheme;
        ApplyChromeFromSettings(persist: true);
    }

    private void ThemeLight_Click(object? sender, RoutedEventArgs e)
    {
        _uiSettings.Theme = GlassChrome.LightTheme;
        ApplyChromeFromSettings(persist: true);
    }

    private void ChromeOpacitySlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_glassChromeApplying || e.Property != RangeBase.ValueProperty || ChromeOpacitySlider is null)
            return;
        _uiSettings.ChromeOpacity = ChromeOpacitySlider.Value;
        ApplyChromeFromSettings(persist: true);
    }

    private void ApplyChromeFromSettings(bool persist)
    {
        if (Application.Current is not { } app)
            return;

        _glassChromeApplying = true;
        try
        {
            var light = GlassChrome.IsLightTheme(_uiSettings.Theme);
            var opacity = GlassChrome.ClampOpacity(_uiSettings.ChromeOpacity);
            _uiSettings.Theme = light ? GlassChrome.LightTheme : GlassChrome.DarkTheme;
            _uiSettings.ChromeOpacity = opacity;
            GlassChrome.Apply(app, light, opacity);
            if (ChromeOpacitySlider is not null && Math.Abs(ChromeOpacitySlider.Value - opacity) > 0.001)
                ChromeOpacitySlider.Value = opacity;
            MarkThemeButtons(light);
            if (persist)
                _uiSettings.Save();
        }
        finally
        {
            _glassChromeApplying = false;
        }
    }

    private void MarkThemeButtons(bool light)
    {
        void Mark(Button? button, bool active)
        {
            if (button is null)
                return;
            if (active)
            {
                if (!button.Classes.Contains("active"))
                    button.Classes.Add("active");
            }
            else
            {
                button.Classes.Remove("active");
            }
        }

        Mark(ThemeDarkButton, !light);
        Mark(ThemeLightButton, light);
    }

    private async Task OpenPersonaFromContactsAsync(PersonaPackInfo persona, bool edit)
    {
        if (edit)
        {
            var get = await _personas.GetAsync(persona.PersonaId).ConfigureAwait(true);
            if (!get.Ok || get.Persona is null)
            {
                _contactsWindow?.SetStatus(get.Detail ?? "Could not load persona.");
                return;
            }

            await OpenPersonaWizardAsync(get.Persona).ConfigureAwait(true);
            return;
        }

        var focus = ThreadFocus.Resolve(_modeBoard.Displayed, _activePersonaId, persona.PersonaId);
        if (!focus.Activate)
        {
            OpenThreadWithoutActivating(persona.PersonaId, persona.DisplayName, showChat: true);
            return;
        }

        await SwitchToPersonaAsync(persona).ConfigureAwait(true);
    }

    private async Task SetFavoritePhotoAsync(PersonaPackInfo persona)
    {
        if (_contactsWindow is null)
            return;

        var files = await _contactsWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Favorite photo",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif", "*.bmp"]
                }
            ]
        }).ConfigureAwait(true);

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        var existing = _uiSettings.Favorites.FirstOrDefault(slot =>
            string.Equals(slot.PersonaId, persona.PersonaId, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            PinFavorite(persona, path);
            return;
        }

        existing.PhotoPath = path;
        _uiSettings.Save();
        RefreshGlassLists();
        _contactsWindow.SetStatus($"Photo saved for {persona.DisplayName}.");
    }

    private void PinFavorite(PersonaPackInfo persona, string? photoPath)
    {
        if (!FavoriteRoster.TryAdd(
                _uiSettings.Favorites,
                persona.PersonaId,
                persona.DisplayName,
                photoPath,
                out var error))
        {
            _contactsWindow?.SetStatus(error);
            return;
        }

        _uiSettings.Save();
        RefreshGlassLists();
        _contactsWindow?.SetStatus($"Pinned {persona.DisplayName}.");
    }
}
