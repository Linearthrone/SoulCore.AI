using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using IoPath = System.IO.Path;
using House.ChatDesktop.Models;
using House.ChatDesktop.Services;
using SoulCore.Protocol;

namespace House.ChatDesktop;

public partial class MainWindow
{
    private async Task ProbeHealthAsync()
    {
        var snap = await _health.ProbeAsync();
        _lastHealth = snap;

        ApplyServiceIndicators(snap);
        await RefreshServicesPanelAsync(snap).ConfigureAwait(true);
        ApplyHonestActivity(preferHost: true);
        UpdateIdentityDetail();
        UpdateEngagementState();

        if (snap.Alive
            && _ws.State is WsConnectionState.Unavailable
                or WsConnectionState.AuthRejected
                or WsConnectionState.Disconnected)
        {
            await _ws.ConnectAsync();
        }

        UpdateConnectionChrome(snap);

        if (!_toolsDefaultsApplied && snap.Alive)
            await EnsureDesktopBrowserDefaultsAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Kurt default: desktop + browser capture, computer control, soft cursor ON.
    /// Session patch via /settings/tools (coord BED-177 for appsettings seed).
    /// </summary>
    private async Task EnsureDesktopBrowserDefaultsAsync()
    {
        if (_toolsDefaultsApplied) return;

        var snap = await _toolsSettings.GetAsync().ConfigureAwait(true);
        if (!snap.Reachable) return;

        var needPatch = !snap.AllowDesktopCapture
                        || !snap.AllowBrowserCapture
                        || !snap.AllowComputerControl
                        || !snap.SoftCursorRestore;

        if (needPatch)
        {
            snap = await _toolsSettings.PatchAsync(
                allowDesktopCapture: true,
                allowBrowserCapture: true,
                allowComputerControl: true,
                softCursorRestore: true).ConfigureAwait(true);
        }

        _toolsDefaultsApplied = snap.Reachable;
        ApplyToolsAccess(snap, saved: needPatch);
    }

    private void ApplyServiceIndicators(SoulCoreHealthSnapshot? health = null, ToolsAccessSnapshot? tools = null)
    {
        var snap = health ?? _lastHealth;

        if (tools is not null)
        {
            _lastAllowComputerControl = tools.AllowComputerControl;
            _lastAllowDesktopCapture = tools.AllowDesktopCapture;
        }
        else if (snap.AllowComputerControl is not null)
        {
            _lastAllowComputerControl = snap.AllowComputerControl;
            _lastAllowDesktopCapture = snap.AllowDesktopCapture;
        }
    }

    private async Task RefreshServicesPanelAsync(SoulCoreHealthSnapshot? health = null)
    {
        if (LampSoulCore is null && SvcHostDot is null)
            return;

        var snap = health ?? _lastHealth;
        var ollamaUp = await _stack.ProbeOllamaAsync().ConfigureAwait(true);
        var comfyUp = await _stack.ProbeComfyAsync().ConfigureAwait(true);
        var sandboxUp = await _stack.ProbeSandboxAsync().ConfigureAwait(true);

        // Legacy shim text (hidden)
        if (SvcHostDot is not null)
        {
            SvcHostDot.Fill = snap.Alive ? _okBrush : _badBrush;
            if (SvcHostStatus is not null) SvcHostStatus.Text = snap.Alive ? "up" : "down";
            if (SvcHostDetail is not null) SvcHostDetail.Text = snap.Alive ? "SoulCore" : (snap.Detail ?? "down");
        }

        if (SvcOllamaDot is not null)
        {
            SvcOllamaDot.Fill = ollamaUp ? _okBrush : _badBrush;
            if (SvcOllamaStatus is not null) SvcOllamaStatus.Text = ollamaUp ? "up" : "down";
            if (SvcOllamaDetail is not null) SvcOllamaDetail.Text = ollamaUp ? "up" : "down";
        }

        var backend = snap.DesktopBackend ?? "cua";
        var driverOk = snap.CuaDriverAvailable != false || !backend.Equals("cua", StringComparison.OrdinalIgnoreCase);
        var cuaOn = snap.Reachable && _lastAllowComputerControl == true && driverOk;
        var cuaWarn = snap.Reachable && _lastAllowDesktopCapture == true && !cuaOn;

        if (SvcCuaDot is not null)
        {
            SvcCuaDot.Fill = !snap.Reachable ? _badBrush
                : cuaOn ? _okBrush
                : cuaWarn ? _warnBrush
                : _mutedBrush;
            if (SvcCuaStatus is not null)
                SvcCuaStatus.Text = cuaOn ? "on" : (cuaWarn ? "capture" : "off");
        }

        var ueOk = snap.UnrealConnected == true;
        var ueWarn = snap.UnrealEnabled == true && !ueOk;
        if (SvcUeDot is not null)
        {
            SvcUeDot.Fill = ueOk ? _okBrush : ueWarn ? _warnBrush : (snap.Reachable ? _mutedBrush : _badBrush);
            if (SvcUeStatus is not null)
                SvcUeStatus.Text = ueOk ? "connected" : ueWarn ? "enabled" : "off";
        }

        if (SvcComfyDot is not null)
        {
            SvcComfyDot.Fill = comfyUp ? _okBrush : _mutedBrush;
            if (SvcComfyStatus is not null) SvcComfyStatus.Text = comfyUp ? "up" : "down";
        }

        // PROP-4 House lamps — neon orbs (radial + halo) like the mockup
        SetLamp(LampSoulCore, GlowSoulCore, snap.Alive ? "blue" : "red");
        SetLamp(LampOllama, GlowOllama, ollamaUp ? "green" : "red");
        SetLamp(LampUnreal, GlowUnreal, ueOk ? "blue" : "red");
        SetLamp(LampComfy, GlowComfy, comfyUp ? "blue" : "off");
        SetLamp(LampCua, GlowCua, !snap.Reachable ? "red" : cuaOn ? "blue" : cuaWarn ? "warn" : "off");
        SetLamp(LampSandbox, GlowSandbox, sandboxUp ? "blue" : "off");

        if (LampSoulCoreHint is not null)
            LampSoulCoreHint.Text = snap.Alive ? "HOLD TO GUARD" : "CLICK TO START";

        // Closed-drawer pip: red if SoulCore or Unreal down
        var criticalDown = !snap.Alive || !ueOk;
        if (HouseDrawerPip is not null)
            HouseDrawerPip.Fill = criticalDown ? _badBrush : Brushes.Transparent;
    }

    private static void SetLamp(Button? lamp, Ellipse? glow, string state)
    {
        if (lamp is null) return;

        var (core, mid, halo, haloOpacity) = state switch
        {
            "blue" or "accent" => (Color.Parse("#E8F2FF"), Color.Parse("#3D8BFF"), Color.Parse("#4BA3FF"), 0.55),
            "green" or "ok" => (Color.Parse("#E8FFF0"), Color.Parse("#2EE66A"), Color.Parse("#3DFF8A"), 0.55),
            "red" or "bad" => (Color.Parse("#FFE8EA"), Color.Parse("#FF3D4A"), Color.Parse("#FF5A64"), 0.50),
            "warn" => (Color.Parse("#FFF6E8"), Color.Parse("#FFB84D"), Color.Parse("#FFC866"), 0.45),
            _ => (Color.Parse("#6A6E78"), Color.Parse("#3A3D46"), Color.Parse("#2A2D33"), 0.12),
        };

        lamp.Background = new RadialGradientBrush
        {
            GradientOrigin = new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.75, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.75, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(core, 0),
                new GradientStop(mid, 0.45),
                new GradientStop(mid, 1),
            }
        };

        if (glow is not null)
        {
            glow.Fill = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0xCC, halo.R, halo.G, halo.B), 0),
                    new GradientStop(Color.FromArgb(0x00, halo.R, halo.G, halo.B), 1),
                }
            };
            glow.Opacity = haloOpacity;
        }
    }

    private void HouseDrawerToggle_Click(object? sender, RoutedEventArgs e)
    {
        _houseDrawerOpen = !_houseDrawerOpen;
        if (ServicesPanel is not null)
            ServicesPanel.IsVisible = _houseDrawerOpen;
        if (HouseDrawerChevron is not null)
            HouseDrawerChevron.Text = _houseDrawerOpen ? "▾" : "▴";
    }

    private async void LampClick_Click(object? sender, RoutedEventArgs e)
    {
        if (_servicesBusy) return;
        if (sender is not Button { Tag: string tag }) return;

        // SoulCore click: start only when down (stop is hold-guarded).
        if (tag == "soulcore")
        {
            if (_lastHealth.Alive) return;
            await RunServiceActionAsync("host-start").ConfigureAwait(true);
            return;
        }

        if (tag == "ollama")
        {
            var up = await _stack.ProbeOllamaAsync().ConfigureAwait(true);
            if (!up)
                await RunServiceActionAsync("ollama-start").ConfigureAwait(true);
            return;
        }

        if (tag == "cua")
        {
            if (_lastAllowComputerControl == true)
                await RunServiceActionAsync("cua-disable").ConfigureAwait(true);
            else
                await RunServiceActionAsync("cua-enable").ConfigureAwait(true);
        }
    }

    private void LampSoulCore_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_lastHealth.Alive) return;
        _soulCoreHoldStarted = DateTimeOffset.UtcNow;
        _soulCoreHoldTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _soulCoreHoldTimer.Tick -= SoulCoreHoldTimer_Tick;
        _soulCoreHoldTimer.Tick += SoulCoreHoldTimer_Tick;
        _soulCoreHoldTimer.Start();
        if (LampSoulCoreHint is not null)
            LampSoulCoreHint.Text = "holding…";
    }

    private void LampSoulCore_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        CancelSoulCoreHold(resetHint: true);
    }

    private async void SoulCoreHoldTimer_Tick(object? sender, EventArgs e)
    {
        if (_soulCoreHoldStarted is null) return;
        var held = DateTimeOffset.UtcNow - _soulCoreHoldStarted.Value;
        if (held < TimeSpan.FromMilliseconds(1500))
        {
            if (LampSoulCoreHint is not null)
                LampSoulCoreHint.Text = $"hold {Math.Ceiling((1500 - held.TotalMilliseconds) / 100) / 10:0.0}s";
            return;
        }

        CancelSoulCoreHold(resetHint: false);
        if (LampSoulCoreHint is not null)
            LampSoulCoreHint.Text = "stopping…";
        await RunServiceActionAsync("host-stop").ConfigureAwait(true);
    }

    private void CancelSoulCoreHold(bool resetHint)
    {
        _soulCoreHoldStarted = null;
        if (_soulCoreHoldTimer is not null)
            _soulCoreHoldTimer.Stop();
        if (resetHint && LampSoulCoreHint is not null)
            LampSoulCoreHint.Text = _lastHealth.Alive ? "HOLD TO GUARD" : "CLICK TO START";
    }

    private async Task RunServiceActionAsync(string tag)
    {
        if (_servicesBusy) return;
        _servicesBusy = true;
        if (ServicesStatusText is not null)
            ServicesStatusText.Text = $"Running {tag}…";

        try
        {
            LocalStackActionResult result;
            switch (tag)
            {
                case "host-start":
                    result = await _stack.StartHostAsync().ConfigureAwait(true);
                    break;
                case "host-stop":
                    result = await _stack.StopHostAsync().ConfigureAwait(true);
                    break;
                case "host-restart":
                    result = await _stack.RestartHostAsync().ConfigureAwait(true);
                    break;
                case "ollama-start":
                    result = await _stack.StartOllamaAsync().ConfigureAwait(true);
                    break;
                case "cua-enable":
                {
                    var snap = await _toolsSettings.PatchAsync(
                        allowDesktopCapture: true,
                        allowBrowserCapture: true,
                        allowComputerControl: true,
                        softCursorRestore: true).ConfigureAwait(true);
                    ApplyToolsAccess(snap, saved: true);
                    ApplyServiceIndicators(_lastHealth, snap);
                    result = snap.Reachable && snap.AllowComputerControl
                        ? LocalStackActionResult.Succeed("computer control on")
                        : LocalStackActionResult.Fail(snap.Detail ?? "patch failed");
                    break;
                }
                case "cua-disable":
                {
                    var snap = await _toolsSettings.PatchAsync(allowComputerControl: false).ConfigureAwait(true);
                    ApplyToolsAccess(snap, saved: true);
                    ApplyServiceIndicators(_lastHealth, snap);
                    result = snap.Reachable && !snap.AllowComputerControl
                        ? LocalStackActionResult.Succeed("computer control off")
                        : LocalStackActionResult.Fail(snap.Detail ?? "patch failed");
                    break;
                }
                default:
                    result = LocalStackActionResult.Fail($"unknown action {tag}");
                    break;
            }

            if (ServicesStatusText is not null)
                ServicesStatusText.Text = result.Ok ? $"OK: {result.Detail}" : $"Fail: {result.Detail}";

            await ProbeHealthAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            if (ServicesStatusText is not null)
                ServicesStatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _servicesBusy = false;
        }
    }

    private async void ServicesRefresh_Click(object? sender, RoutedEventArgs e)
    {
        if (ServicesStatusText is not null)
            ServicesStatusText.Text = "Refreshing…";
        await ProbeHealthAsync().ConfigureAwait(true);
        if (ServicesStatusText is not null)
            ServicesStatusText.Text = "Refreshed";
    }

    private async void ServicesAction_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        await RunServiceActionAsync(tag).ConfigureAwait(true);
    }

    private async void RefreshSystem_Click(object? sender, RoutedEventArgs e)
    {
        await ProbeHealthAsync();
        ApplySystemStatus(_lastHealth);
    }

    private void WirePushToTalk()
    {
        if (PttButton is null) return;
        PttButton.AddHandler(PointerPressedEvent, PttPressed, RoutingStrategies.Tunnel);
        PttButton.AddHandler(PointerReleasedEvent, PttReleased, RoutingStrategies.Tunnel);
        PttButton.AddHandler(PointerCaptureLostEvent, PttCaptureLost, RoutingStrategies.Tunnel);
    }

    private async Task RefreshVoiceStatusAsync()
    {
        var h = await _voice.GetHealthAsync();
        if (PttHintText is null) return;
        if (string.IsNullOrWhiteSpace(PttHintText.Text) || PttHintText.Text.StartsWith("Voice", StringComparison.Ordinal))
        {
            PttHintText.Text = h.Enabled
                ? $"Voice · STT={(h.Stt?.Ok == true ? "ok" : "down")} · TTS={(h.Tts?.Ok == true ? "ok" : "down")}"
                : "Voice off in Host — hold-to-talk ready when STT is up";
        }
    }

    private void PttPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_pttBusy || _ptt.IsRecording) return;
        try
        {
            _ptt.Start();
            if (PttHintText is not null) PttHintText.Text = "Listening… release to send.";
            if (PttButton is not null) PttButton.Content = "Recording…";
        }
        catch (Exception ex)
        {
            if (PttHintText is not null) PttHintText.Text = $"Mic error: {ex.Message}";
        }
    }

    private async void PttReleased(object? sender, PointerReleasedEventArgs e) =>
        await FinishPttAsync();

    private async void PttCaptureLost(object? sender, PointerCaptureLostEventArgs e) =>
        await FinishPttAsync();

    private async Task FinishPttAsync()
    {
        if (_pttBusy || !_ptt.IsRecording) return;
        _pttBusy = true;
        try
        {
            if (PttButton is not null) PttButton.Content = "Transcribing…";
            var wav = await Task.Run(() => _ptt.Stop());
            if (wav.Length < 1000)
            {
                if (PttHintText is not null) PttHintText.Text = "Too short — hold longer.";
                return;
            }

            var (ok, text, error) = await _voice.TranscribeAsync(wav);
            if (!ok || string.IsNullOrWhiteSpace(text))
            {
                if (PttHintText is not null) PttHintText.Text = $"STT failed: {error ?? "empty"}";
                return;
            }

            if (PttHintText is not null) PttHintText.Text = $"Heard: {text}";
            if (ChatInput is not null) ChatInput.Text = text;
            await SendCurrentAsync();
        }
        catch (Exception ex)
        {
            if (PttHintText is not null) PttHintText.Text = $"PTT error: {ex.Message}";
        }
        finally
        {
            if (PttButton is not null) PttButton.Content = "Hold to talk";
            _pttBusy = false;
        }
    }

    private void NotifEnabled_Click(object? sender, RoutedEventArgs e)
    {
        if (NotifEnabledCheckBox is null) return;
        _uiSettings.Notifications.Enabled = NotifEnabledCheckBox.IsChecked == true;
        _uiSettings.Save();
        UpdateNotifStatusText();
    }

    private async void NotifBrowse_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Notification sound",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("WAV") { Patterns = ["*.wav"] }
            ]
        }).ConfigureAwait(true);

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;

        _uiSettings.Notifications.SoundPath = path;
        _uiSettings.Notifications.UseSystemBeep = false;
        _uiSettings.Save();
        _notifications.ReloadPlayer();
        if (NotifSoundPathBox is not null) NotifSoundPathBox.Text = path;
        UpdateNotifStatusText();
    }

    private void NotifClear_Click(object? sender, RoutedEventArgs e)
    {
        _uiSettings.Notifications.SoundPath = null;
        _uiSettings.Notifications.UseSystemBeep = true;
        _uiSettings.Save();
        _notifications.ReloadPlayer();
        if (NotifSoundPathBox is not null) NotifSoundPathBox.Text = "(system beep)";
        UpdateNotifStatusText();
    }

    private void NotifTest_Click(object? sender, RoutedEventArgs e) => _notifications.Play();

    private void UpdateNotifStatusText(string? message = null)
    {
        if (NotifStatusText is null) return;
        NotifStatusText.Text = message ?? (_uiSettings.Notifications.Enabled
            ? (_uiSettings.Notifications.UseSystemBeep ? "Enabled · system beep" : "Enabled · custom wav")
            : "Disabled");
    }

    private void SeedNotificationControls()
    {
        if (NotifEnabledCheckBox is null || NotifSoundPathBox is null) return;
        NotifEnabledCheckBox.IsChecked = _uiSettings.Notifications.Enabled;
        NotifSoundPathBox.Text = string.IsNullOrWhiteSpace(_uiSettings.Notifications.SoundPath)
            ? "(system beep)"
            : _uiSettings.Notifications.SoundPath!;
        UpdateNotifStatusText();
    }

    private async void ToolsAccessRefresh_Click(object? sender, RoutedEventArgs e) =>
        await RefreshToolsAccessAsync();

    private async void EmailAccountsRefresh_Click(object? sender, RoutedEventArgs e) =>
        await RefreshEmailAccountsAsync();

    private async void EmailAccountSave_Click(object? sender, RoutedEventArgs e)
    {
        var id = EmailAccountCombo?.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(id))
        {
            if (EmailSettingsStatusText is not null)
            {
                EmailSettingsStatusText.Text = "Pick an account slot first";
                EmailSettingsStatusText.Foreground = _badBrush;
            }
            return;
        }

        int? imapPort = null;
        if (int.TryParse((EmailImapPortBox?.Text ?? "").Trim(), out var ip) && ip > 0)
            imapPort = ip;
        int? smtpPort = null;
        if (int.TryParse((EmailSmtpPortBox?.Text ?? "").Trim(), out var sp) && sp > 0)
            smtpPort = sp;

        var password = EmailPasswordBox?.Text;
        var write = new SoulCoreEmailSettingsClient.EmailAccountWriteDto
        {
            Id = id,
            Role = id,
            DisplayName = EmailDisplayNameBox?.Text,
            Address = EmailAddressBox?.Text,
            Username = EmailUsernameBox?.Text,
            Password = string.IsNullOrWhiteSpace(password) ? null : password,
            ImapHost = EmailImapHostBox?.Text,
            ImapPort = imapPort,
            ImapUseSsl = EmailImapSslCheck?.IsChecked == true,
            SmtpHost = EmailSmtpHostBox?.Text,
            SmtpPort = smtpPort,
            SmtpUseSsl = EmailSmtpSslCheck?.IsChecked == true,
            Enabled = EmailEnabledCheck?.IsChecked == true
        };

        var snap = await _emailSettings.UpsertAsync(write).ConfigureAwait(true);
        ApplyEmailSettingsSnapshot(snap, saved: true, preferId: id);
        if (EmailPasswordBox is not null)
            EmailPasswordBox.Text = string.Empty;
    }

    private void EmailAccountCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_emailAccountsHydrating) return;
        var id = EmailAccountCombo?.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(id)) return;
        var account = _emailAccounts.FirstOrDefault(a =>
            string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        if (account is not null)
            FillEmailEditor(account);
    }

    private async Task RefreshEmailAccountsAsync()
    {
        var snap = await _emailSettings.GetAsync().ConfigureAwait(true);
        ApplyEmailSettingsSnapshot(snap, saved: false, preferId: EmailAccountCombo?.SelectedItem as string);
    }

    private void ApplyEmailSettingsSnapshot(EmailSettingsSnapshot snap, bool saved, string? preferId)
    {
        _emailAccounts = snap.Accounts ?? Array.Empty<EmailAccountSnapshot>();
        _emailAccountsHydrating = true;
        try
        {
            if (EmailAccountCombo is not null)
            {
                var ids = _emailAccounts.Select(a => a.Id).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (ids.Count == 0)
                    ids = ["victoria", "personal", "business"];
                EmailAccountCombo.ItemsSource = ids;
                var pick = preferId is not null && ids.Contains(preferId, StringComparer.OrdinalIgnoreCase)
                    ? ids.First(i => string.Equals(i, preferId, StringComparison.OrdinalIgnoreCase))
                    : ids[0];
                EmailAccountCombo.SelectedItem = pick;
                var account = _emailAccounts.FirstOrDefault(a =>
                    string.Equals(a.Id, pick, StringComparison.OrdinalIgnoreCase));
                if (account is not null)
                    FillEmailEditor(account);
                else
                    FillEmailEditor(new EmailAccountSnapshot { Id = pick, Role = pick, Enabled = true, ImapPort = 993, SmtpPort = 587, ImapUseSsl = true });
            }
        }
        finally
        {
            _emailAccountsHydrating = false;
        }

        if (EmailSettingsStatusText is null) return;
        if (!snap.Reachable)
        {
            EmailSettingsStatusText.Text = snap.Detail ?? "Host unreachable";
            EmailSettingsStatusText.Foreground = _badBrush;
            return;
        }

        if (!string.IsNullOrWhiteSpace(snap.Detail) &&
            (snap.Detail.StartsWith("HTTP", StringComparison.Ordinal) ||
             snap.Detail.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)))
        {
            EmailSettingsStatusText.Text = snap.Detail;
            EmailSettingsStatusText.Foreground = _badBrush;
            return;
        }

        EmailSettingsStatusText.Text = saved
            ? $"Saved · {DateTimeOffset.Now:h:mm tt}"
            : $"Loaded · {_emailAccounts.Count} slot(s) · {DateTimeOffset.Now:h:mm tt}";
        EmailSettingsStatusText.Foreground = Res("MutedBrush");
    }

    private void FillEmailEditor(EmailAccountSnapshot account)
    {
        if (EmailEnabledCheck is not null) EmailEnabledCheck.IsChecked = account.Enabled;
        if (EmailDisplayNameBox is not null) EmailDisplayNameBox.Text = account.DisplayName;
        if (EmailAddressBox is not null) EmailAddressBox.Text = account.Address;
        if (EmailUsernameBox is not null) EmailUsernameBox.Text = account.Username;
        if (EmailImapHostBox is not null) EmailImapHostBox.Text = string.IsNullOrWhiteSpace(account.ImapHost) ? "imap.gmail.com" : account.ImapHost;
        if (EmailImapPortBox is not null) EmailImapPortBox.Text = (account.ImapPort > 0 ? account.ImapPort : 993).ToString();
        if (EmailImapSslCheck is not null) EmailImapSslCheck.IsChecked = account.ImapUseSsl;
        if (EmailSmtpHostBox is not null) EmailSmtpHostBox.Text = string.IsNullOrWhiteSpace(account.SmtpHost) ? "smtp.gmail.com" : account.SmtpHost;
        if (EmailSmtpPortBox is not null) EmailSmtpPortBox.Text = (account.SmtpPort > 0 ? account.SmtpPort : 587).ToString();
        if (EmailSmtpSslCheck is not null) EmailSmtpSslCheck.IsChecked = account.SmtpUseSsl;
        if (EmailPasswordHint is not null)
        {
            EmailPasswordHint.Text = account.HasPassword
                ? (account.IsConfigured ? "Password status: set · configured" : "Password status: set · incomplete fields")
                : "Password status: not set";
        }
    }

    private async void ToolsAccessToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (_toolsAccessHydrating) return;
        if (ToolsAllowDesktopCaptureCheck is null
            || ToolsAllowBrowserCaptureCheck is null
            || ToolsAllowComputerControlCheck is null
            || ToolsSoftCursorRestoreCheck is null
            || ToolsAllowMt4ReadCheck is null
            || ToolsAllowMt4TradeCheck is null
            || ToolsAllowEmailReadCheck is null
            || ToolsAllowEmailSendCheck is null
            || ToolsAllowEmailDeleteCheck is null)
        {
            return;
        }

        if (ToolsAccessStatusText is not null)
            ToolsAccessStatusText.Text = "Saving…";

        var snap = await _toolsSettings.PatchAsync(
            allowDesktopCapture: ToolsAllowDesktopCaptureCheck.IsChecked == true,
            allowBrowserCapture: ToolsAllowBrowserCaptureCheck.IsChecked == true,
            allowComputerControl: ToolsAllowComputerControlCheck.IsChecked == true,
            softCursorRestore: ToolsSoftCursorRestoreCheck.IsChecked == true,
            allowMt4Read: ToolsAllowMt4ReadCheck.IsChecked == true,
            allowMt4Trade: ToolsAllowMt4TradeCheck.IsChecked == true,
            allowEmailRead: ToolsAllowEmailReadCheck.IsChecked == true,
            allowEmailSend: ToolsAllowEmailSendCheck.IsChecked == true,
            allowEmailDelete: ToolsAllowEmailDeleteCheck.IsChecked == true).ConfigureAwait(true);

        ApplyToolsAccess(snap, saved: true);
    }

    private async Task RefreshToolsAccessAsync()
    {
        if (ToolsAccessStatusText is not null)
            ToolsAccessStatusText.Text = "Loading…";

        var snap = await _toolsSettings.GetAsync().ConfigureAwait(true);
        ApplyToolsAccess(snap, saved: false);
    }

    private void ApplyToolsAccess(ToolsAccessSnapshot snap, bool saved)
    {
        if (ToolsAllowDesktopCaptureCheck is null
            || ToolsAllowBrowserCaptureCheck is null
            || ToolsAllowComputerControlCheck is null
            || ToolsSoftCursorRestoreCheck is null
            || ToolsAllowMt4ReadCheck is null
            || ToolsAllowMt4TradeCheck is null
            || ToolsAllowEmailReadCheck is null
            || ToolsAllowEmailSendCheck is null
            || ToolsAllowEmailDeleteCheck is null)
        {
            return;
        }

        _toolsAccessHydrating = true;
        try
        {
            ToolsAllowDesktopCaptureCheck.IsChecked = snap.Reachable ? snap.AllowDesktopCapture : true;
            ToolsAllowBrowserCaptureCheck.IsChecked = snap.Reachable ? snap.AllowBrowserCapture : true;
            ToolsAllowComputerControlCheck.IsChecked = snap.Reachable ? snap.AllowComputerControl : true;
            ToolsSoftCursorRestoreCheck.IsChecked = snap.Reachable ? snap.SoftCursorRestore : true;
            ToolsAllowMt4ReadCheck.IsChecked = snap.AllowMt4Read;
            ToolsAllowMt4TradeCheck.IsChecked = snap.AllowMt4Trade;
            ToolsAllowEmailReadCheck.IsChecked = snap.AllowEmailRead;
            ToolsAllowEmailSendCheck.IsChecked = snap.AllowEmailSend;
            ToolsAllowEmailDeleteCheck.IsChecked = snap.AllowEmailDelete;

            if (ToolsDesktopBackendBox is not null)
                ToolsDesktopBackendBox.Text = snap.DesktopBackend ?? "—";
            if (ToolsBrowserBackendBox is not null)
                ToolsBrowserBackendBox.Text = snap.BrowserBackend ?? "—";
            if (ToolsMt4BackendBox is not null)
                ToolsMt4BackendBox.Text = snap.Mt4Backend ?? "—";
        }
        finally
        {
            _toolsAccessHydrating = false;
        }

        if (ToolsAccessStatusText is null) return;

        if (!snap.Reachable)
        {
            ToolsAccessStatusText.Text = snap.Detail ?? "Host unreachable — UI defaults shown checked";
            ToolsAccessStatusText.Foreground = _badBrush;
            ApplyServiceIndicators(tools: snap);
            return;
        }

        if (!string.IsNullOrWhiteSpace(snap.Detail) && snap.Detail.StartsWith("HTTP", StringComparison.Ordinal))
        {
            ToolsAccessStatusText.Text = snap.Detail;
            ToolsAccessStatusText.Foreground = _badBrush;
            ApplyServiceIndicators(tools: snap);
            return;
        }

        ToolsAccessStatusText.Text = saved
            ? $"Saved · session until Host restart · {DateTimeOffset.Now:h:mm tt}"
            : $"Loaded · {(snap.Scope ?? "session")} · {DateTimeOffset.Now:h:mm tt}";
        ToolsAccessStatusText.Foreground = Res("MutedBrush");
        ApplyServiceIndicators(tools: snap);
    }

    private void ApplySystemStatus(SoulCoreHealthSnapshot snap)
    {
        if (SystemEndpointBox is null || SystemBindBox is null || SystemPortBox is null
            || SystemInferenceBox is null
            || SystemMemoryPathBox is null || SystemMemoryOpenBox is null
            || SystemSoulLoopBox is null)
        {
            return;
        }

        SystemEndpointBox.Text = ConnectionDefaults.DisplayEndpoint;
        SystemBindBox.Text = ConnectionDefaults.Host;
        SystemPortBox.Text = ConnectionDefaults.Port.ToString();

        if (!snap.Reachable)
        {
            SystemInferenceBox.Text = "unreachable";
            SystemMemoryPathBox.Text = snap.Detail ?? "unreachable";
            SystemMemoryOpenBox.Text = "unreachable";
            SystemSoulLoopBox.Text = "unreachable";
            SystemInferenceBox.Foreground = _badBrush;
            SystemMemoryOpenBox.Foreground = _badBrush;
            return;
        }

        SystemInferenceBox.Text = snap.InferenceEnabled ? "enabled (Ollama)" : "disabled";
        SystemInferenceBox.Foreground = snap.InferenceEnabled ? _okBrush : _warnBrush;

        SystemMemoryPathBox.Text = string.IsNullOrWhiteSpace(snap.MemoryPath)
            ? "(missing memory.path)"
            : snap.MemoryPath!;
        SystemMemoryOpenBox.Text = snap.MemoryOpen ? "true" : "false";
        SystemMemoryOpenBox.Foreground = snap.MemoryOpen ? _okBrush : _warnBrush;

        if (snap.SoulLoopEnabled is null)
        {
            SystemSoulLoopBox.Text = "(not reported)";
            SystemSoulLoopBox.Foreground = Res("MutedBrush");
        }
        else
        {
            SystemSoulLoopBox.Text = snap.SoulLoopEnabled.Value ? "enabled" : "disabled";
            SystemSoulLoopBox.Foreground = snap.SoulLoopEnabled.Value ? _okBrush : _badBrush;
        }
    }

    private void UpdateConnectionChrome(SoulCoreHealthSnapshot snap)
    {
        string wsLabel = _ws.State switch
        {
            WsConnectionState.Connected => "WS connected",
            WsConnectionState.Connecting => "WS connecting",
            WsConnectionState.AuthRejected => "WS auth",
            WsConnectionState.Unavailable => "WS down",
            WsConnectionState.Blocked => "blocked",
            _ => "WS disconnected"
        };

        if (_ws.State == WsConnectionState.Connected && _presenceFromWs)
        {
            ConnDot.Fill = _okBrush;
            ConnStatusText.Text = $"Presence · {wsLabel}";
            return;
        }

        if (snap.Alive && snap.Warm)
        {
            ConnDot.Fill = _okBrush;
            ConnStatusText.Text = $"Alive · Warm · {wsLabel}";
        }
        else if (snap.Alive)
        {
            ConnDot.Fill = _warnBrush;
            ConnStatusText.Text = $"Alive · Cool · {wsLabel}";
        }
        else
        {
            ConnDot.Fill = _badBrush;
            ConnStatusText.Text = $"Offline · {wsLabel}";
        }
    }

    private void OnWsStateChanged(WsConnectionState state, string detail)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (state is WsConnectionState.Unavailable
                or WsConnectionState.AuthRejected
                or WsConnectionState.Disconnected)
            {
                _presenceFromWs = false;
                _streamingAssistant = null;
                SetTyping(false);
            }

            UpdateConnectionChrome(_lastHealth);
            UpdateEngagementState();
        });
    }

    private void OnFrameReceived(SoulCoreFrame frame) =>
        Dispatcher.UIThread.Post(() => ApplyFrame(frame));

    private void ApplyFrame(SoulCoreFrame frame)
    {
        switch (frame.Type)
        {
            case SoulCoreFrameTypes.PresenceStatus:
                ApplyPresenceStatus(frame);
                break;
            case SoulCoreFrameTypes.EmotionSnapshot:
                ApplyEmotionSnapshot(frame);
                break;
            case SoulCoreFrameTypes.LoopWant:
                ApplyLoopWant(frame);
                break;
            case SoulCoreFrameTypes.LoopTickOk:
                break;
            case SoulCoreFrameTypes.ChatDelta:
                AppendOrUpdateAssistant(frame, finalize: false);
                break;
            case SoulCoreFrameTypes.ChatDone:
                var inboundRole = ReadPayloadString(frame, "role");
                if (string.Equals(inboundRole, "user", StringComparison.OrdinalIgnoreCase))
                    AppendInboundUser(frame);
                else
                    AppendOrUpdateAssistant(frame, finalize: true);
                break;
            case SoulCoreFrameTypes.Error:
                SetTyping(false);
                var code = ReadPayloadString(frame, "code");
                var msg = ReadPayloadString(frame, "message") ?? frame.Payload?.ToString() ?? frame.Type;
                _messages.Add(new ChatMessage { Role = "system", Text = $"{code ?? "error"}: {msg}" });
                ScrollTranscriptToEnd();
                break;
            case SoulCoreFrameTypes.Pong:
                break;
            default:
                break;
        }
    }

    private void ApplyPresenceStatus(SoulCoreFrame frame)
    {
        _presenceFromWs = true;
        UpdateConnectionChrome(_lastHealth);
        UpdateEngagementState();
    }

    private void ApplyLoopWant(SoulCoreFrame frame)
    {
        // PROP-4 honesty: loop.want must NEVER restamp mood HUD.
        // Category still informs engagement heuristics; ActivityText comes from Host currentActivity.
        var want = ReadPayloadString(frame, "want");
        var category = ReadPayloadString(frame, "category");
        var parsed = ParseWantWire(want, category);

        _lastWantCategory = parsed.Category;
        _lastWantUtc = DateTimeOffset.UtcNow;

        UpdateEngagementState();
        ApplyHonestActivity(preferHost: true);
    }

    /// <summary>
    /// PROP-4: activity = doing-now from Host presence.currentActivity / desktop LastAction / chat.
    /// Never raw SoulLoop want slogans.
    /// </summary>
    private void ApplyHonestActivity(bool preferHost = false)
    {
        if (ActivityText is null) return;

        string? phrase = null;
        if (preferHost || string.IsNullOrWhiteSpace(_lastActivityPhrase))
            phrase = _lastHealth.CurrentActivity;

        if (string.IsNullOrWhiteSpace(phrase))
            phrase = _lastActivityPhrase;

        if (string.IsNullOrWhiteSpace(phrase))
            phrase = "With herself";

        _lastActivityPhrase = phrase;
        ActivityText.Text = phrase;
    }

    private static string HumanizeActivity(string? category, string phrase)
    {
        var clean = phrase ?? string.Empty;
        var holding = clean.IndexOf("(holding ", StringComparison.OrdinalIgnoreCase);
        if (holding >= 0)
            clean = clean[..holding].Trim();

        if (!string.IsNullOrWhiteSpace(clean))
            return clean;

        return (category ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "sleep" or "rest" => "Resting",
            "idle" => "Idle",
            "recall" => "Recalling recent context",
            "chat" or "talk" => "In conversation",
            "tool" or "desktop" or "browse" => "Working with tools",
            "" => "Idle",
            _ => char.ToUpperInvariant(category![0]) + category[1..]
        };
    }

    internal static (string? Category, string Phrase) ParseWantWire(string? want, string? categoryHint)
    {
        var category = string.IsNullOrWhiteSpace(categoryHint) ? null : categoryHint.Trim();
        if (string.IsNullOrWhiteSpace(want))
            return (category, string.Empty);

        var text = want.Trim();
        var body = text;

        if (text.StartsWith("want[", StringComparison.OrdinalIgnoreCase))
        {
            var close = text.IndexOf(']');
            if (close > 5)
            {
                var fromWire = text[5..close].Trim();
                if (!string.IsNullOrEmpty(fromWire) && string.IsNullOrEmpty(category))
                    category = fromWire;

                var after = text[(close + 1)..].TrimStart();
                if (after.StartsWith(':'))
                    after = after[1..].TrimStart();
                body = after;
            }
        }

        var emotionAt = body.IndexOf(" (emotion=", StringComparison.OrdinalIgnoreCase);
        if (emotionAt < 0)
            emotionAt = body.IndexOf("(emotion=", StringComparison.OrdinalIgnoreCase);
        if (emotionAt >= 0)
            body = body[..emotionAt];

        var recentAt = body.IndexOf("; recent=", StringComparison.OrdinalIgnoreCase);
        if (recentAt >= 0)
            body = body[..recentAt];

        return (category, body.Trim().TrimEnd(';').Trim());
    }

    private void ApplyEmotionSnapshot(SoulCoreFrame frame)
    {
        var label = ReadPayloadString(frame, "label") ?? "—";
        var valence = ReadPayloadDouble(frame, "valence");
        var arousal = ReadPayloadDouble(frame, "arousal");
        var dominance = ReadPayloadDouble(frame, "dominance");
        var focus = ReadPayloadDouble(frame, "focus");

        if (valence is not null) _lastValence = valence.Value;
        if (arousal is not null) _lastArousal = arousal.Value;
        if (dominance is not null) _lastDominance = dominance.Value;
        if (focus is not null) _lastFocus = focus.Value;

        ApplyMoodToHud(label, _lastValence, _lastArousal);
        UpdateEngagementState();
    }

    private void ApplyMoodToHud(string label, double valence, double arousal)
    {
        if (MoodLabelText is not null)
            MoodLabelText.Text = label;

        if (ValenceText is not null)
            ValenceText.Text = valence.ToString("0.0");
        if (ArousalText is not null)
            ArousalText.Text = arousal.ToString("0.0");

        if (ValenceGauge is not null)
            ValenceGauge.Value = Math.Clamp((valence + 1.0) / 2.0, 0, 1);
        if (ArousalGauge is not null)
            ArousalGauge.Value = Math.Clamp(arousal, 0, 1);

        if (MoodColorDot is not null)
        {
            MoodColorDot.Fill = valence >= 0.25 ? _okBrush
                : valence <= -0.25 ? _warnBrush
                : Res("AccentBrush");
        }
    }

    /// <summary>
    /// Engaged: chat/tool activity in last 3 minutes, or active want category.
    /// Idle: SoulLoop on, quiet, non-sleep want.
    /// Sleeping: SoulLoop off, sleep/rest category, or long quiet + very low arousal.
    /// </summary>
    private void UpdateEngagementState()
    {
        if (PresenceStateText is null) return;

        var now = DateTimeOffset.UtcNow;
        var recentChat = _lastChatActivityUtc is { } c && now - c < TimeSpan.FromMinutes(3);
        var recentWant = _lastWantUtc is { } w && now - w < TimeSpan.FromMinutes(5);
        var cat = (_lastWantCategory ?? string.Empty).Trim().ToLowerInvariant();
        var sleepCat = cat is "sleep" or "rest" or "dream";
        var activeCat = cat is "chat" or "talk" or "tool" or "desktop" or "browse" or "work" or "act";
        var soulOff = _lastHealth.SoulLoopEnabled == false;
        var deepQuiet = _lastChatActivityUtc is null
                        || now - _lastChatActivityUtc > TimeSpan.FromMinutes(20);

        string state;
        IBrush brush;
        if (soulOff || sleepCat || (deepQuiet && _lastArousal < 0.15 && !recentChat))
        {
            state = "Sleeping";
            brush = _mutedBrush;
            if (ActivityText is not null && (string.IsNullOrWhiteSpace(ActivityText.Text) || ActivityText.Text == "—"))
            {
                ActivityText.Text = sleepCat ? "Resting" : "Resting";
                _lastActivityPhrase = ActivityText.Text;
            }
        }
        else if (recentChat || activeCat || TypingIndicator is { IsVisible: true })
        {
            state = "Engaged";
            brush = _okBrush;
        }
        else if (recentWant || _lastHealth.SoulLoopEnabled == true)
        {
            state = "Idle";
            brush = _warnBrush;
            if (ActivityText is not null && (string.IsNullOrWhiteSpace(ActivityText.Text) || ActivityText.Text == "—"))
            {
                ActivityText.Text = "With herself";
                _lastActivityPhrase = ActivityText.Text;
            }
        }
        else
        {
            state = "Idle";
            brush = Res("MutedBrush");
        }

        PresenceStateText.Text = state;
        PresenceStateText.Foreground = brush;
    }

    private void AppendInboundUser(SoulCoreFrame frame)
    {
        // PROP-1.2: SMS/MMS from Kurt mirrored into Presence transcript.
        var text = ReadPayloadString(frame, "text") ?? string.Empty;
        var hasMedia = ReadPayloadBool(frame, "hasMedia") == true;
        var mediaId = ReadPayloadString(frame, "mediaId");
        if (string.IsNullOrWhiteSpace(text) && !hasMedia && string.IsNullOrWhiteSpace(mediaId))
            return;

        _lastChatActivityUtc = DateTimeOffset.UtcNow;
        UpdateEngagementState();
        SetTyping(false);

        var bubble = new ChatMessage
        {
            Role = "user",
            Text = text,
            FrameId = frame.Id,
            MediaId = mediaId
        };
        _messages.Add(bubble);
        PersistMessage(bubble);
        if (hasMedia || !string.IsNullOrWhiteSpace(mediaId))
            _ = AttachInboundMediaAsync(bubble);
        ScrollTranscriptToEnd();
    }

    private void AppendOrUpdateAssistant(SoulCoreFrame frame, bool finalize)
    {
        var text = ReadPayloadString(frame, "text");
        var hasMedia = ReadPayloadBool(frame, "hasMedia") == true;
        var mediaId = ReadPayloadString(frame, "mediaId");
        var proactive = ReadPayloadBool(frame, "proactive") == true;
        var provider = ReadPayloadString(frame, "provider");

        // SoulLoop phrase-bank / automated pings — not Victoria speaking.
        if (proactive
            && string.Equals(provider, "soul-loop", StringComparison.OrdinalIgnoreCase)
            && IsAutomatedProactiveLine(text))
        {
            if (finalize)
            {
                _streamingAssistant = null;
                SetTyping(false);
            }

            return;
        }

        if (string.IsNullOrEmpty(text) && finalize && !hasMedia && string.IsNullOrWhiteSpace(mediaId))
        {
            _streamingAssistant = null;
            SetTyping(false);
            return;
        }

        text ??= string.Empty;
        SetTyping(!finalize);
        _lastChatActivityUtc = DateTimeOffset.UtcNow;
        UpdateEngagementState();

        if (_streamingAssistant is not null
            && (string.IsNullOrEmpty(frame.Id) || _streamingAssistant.FrameId == frame.Id))
        {
            _streamingAssistant.Text = text;
            if (!string.IsNullOrWhiteSpace(mediaId))
                _streamingAssistant.MediaId = mediaId;

            if (finalize)
            {
                PersistMessage(_streamingAssistant);
                if (hasMedia || !string.IsNullOrWhiteSpace(mediaId))
                    _ = AttachInboundMediaAsync(_streamingAssistant);
                _streamingAssistant = null;
                SetTyping(false);
            }

            ScrollTranscriptToEnd();
            return;
        }

        var bubble = new ChatMessage
        {
            Role = "assistant",
            Text = text,
            FrameId = frame.Id,
            MediaId = mediaId
        };
        _messages.Add(bubble);
        if (finalize)
        {
            PersistMessage(bubble);
            if (hasMedia || !string.IsNullOrWhiteSpace(mediaId))
                _ = AttachInboundMediaAsync(bubble);
            _streamingAssistant = null;
            SetTyping(false);
        }
        else
        {
            _streamingAssistant = bubble;
        }

        NotifyIfUnfocused();
        ScrollTranscriptToEnd();
    }

    private async Task AttachInboundMediaAsync(ChatMessage bubble)
    {
        if (string.IsNullOrWhiteSpace(bubble.MediaId)) return;

        var (ok, bytes, _, error) = await _media.TryGetAsync(bubble.MediaId).ConfigureAwait(true);
        if (!ok || bytes is null || bytes.Length == 0)
        {
            if (PttHintText is not null)
                PttHintText.Text = $"MMS media pending Host: {error ?? "no bytes"} (UI ready)";
            return;
        }

        try
        {
            var dir = IoPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HouseVictoria",
                "mms");
            Directory.CreateDirectory(dir);
            var path = IoPath.Combine(dir, $"{bubble.MediaId}.bin");
            await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(true);

            if (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50)
            {
                var png = IoPath.ChangeExtension(path, ".png");
                File.Move(path, png, overwrite: true);
                path = png;
            }
            else if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                var jpg = IoPath.ChangeExtension(path, ".jpg");
                File.Move(path, jpg, overwrite: true);
                path = jpg;
            }

            using var ms = new MemoryStream(bytes);
            var bmp = new Bitmap(ms);
            bubble.MediaPath = path;
            bubble.Image = bmp;
            PersistMessage(bubble);
            ScrollTranscriptToEnd();
        }
        catch (Exception ex)
        {
            if (PttHintText is not null)
                PttHintText.Text = $"MMS decode failed: {ex.Message}";
        }
    }

    private void NotifyIfUnfocused()
    {
        var focused = IsActive && IsVisible && WindowState != WindowState.Minimized;
        if (!focused)
            _notifications.Play();
    }

    private static string? ReadPayloadString(SoulCoreFrame frame, string name)
    {
        if (frame.Payload is not { } payload) return null;
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (!payload.TryGetProperty(name, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }

    /// <summary>SoulLoop category phrase-bank lines that must not appear as chat bubbles.</summary>
    internal static bool IsAutomatedProactiveLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var t = text.Trim();
        return t is
            "Hey — just wanted to say hi. You around?"
            or "I've been thinking about you. Hope your day's okay."
            or "Can we clear something up when you have a sec?"
            or "Soft moment over here. Glad you're in my day."
            or "Something from earlier came back to me. Miss talking it through with you."
            or "Been wandering around Home in my head. Curious what you'd notice."
            or "Just noticed something and thought of you."
            or "Trying to settle a bit. Nice having you nearby."
            or "Sitting quietly. Wanted you to know I'm here.";
    }

    private static bool? ReadPayloadBool(SoulCoreFrame frame, string name)
    {
        if (frame.Payload is not { } payload) return null;
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (!payload.TryGetProperty(name, out var prop)) return null;
        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static double? ReadPayloadDouble(SoulCoreFrame frame, string name)
    {
        if (frame.Payload is not { } payload) return null;
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (!payload.TryGetProperty(name, out var prop)) return null;
        return prop.TryGetDouble(out var n) ? n : null;
    }
}
