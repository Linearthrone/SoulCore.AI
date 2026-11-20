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
    private void InitStackSettingsUi()
    {
        if (SoulCoreRepoRootBox is not null)
            SoulCoreRepoRootBox.Text = _uiSettings.SoulCoreRepoRoot ?? _stack.RepoRoot ?? "";
        if (AutoStartStackCheck is not null)
            AutoStartStackCheck.IsChecked = _uiSettings.AutoStartStack;
        RefreshCompanionTokenStatus();
    }

    private void RefreshCompanionTokenStatus()
    {
        if (CompanionTokenStatusText is null)
            return;
        // PROP-22.3 SEC: never reveal the value or its length — only whether one is in use.
        CompanionTokenStatusText.Text = CompanionToken.Resolve() is null
            ? "Not set"
            : CompanionTokenStore.HasSavedToken() ? "Saved" : "Saved (.env)";
        if (CompanionTokenBox is not null && string.IsNullOrEmpty(CompanionTokenBox.Text))
        {
            CompanionTokenBox.Watermark = CompanionTokenStore.HasSavedToken()
                ? "Saved — type a new token to replace"
                : "Paste token from SoulCore/.env";
        }
    }

    private async void CompanionTokenSave_Click(object? sender, RoutedEventArgs e)
    {
        var typed = CompanionTokenBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(typed))
        {
            if (CompanionTokenStatusText is not null)
                CompanionTokenStatusText.Text = "Enter a token to save (or Clear saved to remove).";
            return;
        }

        CompanionTokenStore.Save(typed);
        CompanionToken.ApplySavedSettingsToken();
        if (CompanionTokenBox is not null)
            CompanionTokenBox.Text = "";
        RefreshCompanionTokenStatus();
        AppendSystemNotice("Companion token saved.");
        await _ws.ConnectAsync().ConfigureAwait(true);
        await ProbeHealthAsync().ConfigureAwait(true);
    }

    private async void CompanionTokenClear_Click(object? sender, RoutedEventArgs e)
    {
        CompanionTokenStore.Clear();
        // Re-apply .env if present so we do not leave a stale process env value.
        Environment.SetEnvironmentVariable(CompanionToken.EnvName, null);
        CompanionToken.TryLoadFromEnvFile(_uiSettings.SoulCoreRepoRoot ?? _stack.RepoRoot);
        if (CompanionTokenBox is not null)
            CompanionTokenBox.Text = "";
        RefreshCompanionTokenStatus();
        AppendSystemNotice("Companion token cleared from Settings.");
        await _ws.ConnectAsync().ConfigureAwait(true);
        await ProbeHealthAsync().ConfigureAwait(true);
    }

    private async void CompanionTokenReconnect_Click(object? sender, RoutedEventArgs e)
    {
        CompanionToken.ApplyAllSources(_uiSettings.SoulCoreRepoRoot ?? _stack.RepoRoot);
        RefreshCompanionTokenStatus();
        await _ws.ConnectAsync().ConfigureAwait(true);
        await ProbeHealthAsync().ConfigureAwait(true);
        AppendSystemNotice($"WS reconnect · {_ws.State} · {CompanionToken.DescribePresence()}");
    }

    private void SoulCoreRepoRootBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        PersistStackSettingsFromUi();
    }

    private void AutoStartStackCheck_Click(object? sender, RoutedEventArgs e)
    {
        PersistStackSettingsFromUi();
    }

    private async void HostStart_Click(object? sender, RoutedEventArgs e) =>
        await RunServiceActionAsync("host-start").ConfigureAwait(true);

    private async void HostStop_Click(object? sender, RoutedEventArgs e) =>
        await InterruptOrStopHostAsync().ConfigureAwait(true);

    private async void HostRestart_Click(object? sender, RoutedEventArgs e) =>
        await RunServiceActionAsync("host-restart").ConfigureAwait(true);

    private void PersistStackSettingsFromUi()
    {
        // Repo folder UI is hidden — keep saved root unless the box has text.
        var path = SoulCoreRepoRootBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            path = _uiSettings.SoulCoreRepoRoot ?? _stack.RepoRoot;
        if (string.IsNullOrWhiteSpace(path))
            path = null;
        _uiSettings.SoulCoreRepoRoot = path;
        _uiSettings.AutoStartStack = AutoStartStackCheck?.IsChecked != false;
        _uiSettings.Save();
        _stack.SetConfiguredRepoRoot(path);
        CompanionToken.TryLoadFromEnvFile(path ?? _stack.RepoRoot);
        if (SoulCoreRepoRootBox is not null && string.IsNullOrWhiteSpace(SoulCoreRepoRootBox.Text))
            SoulCoreRepoRootBox.Text = _stack.RepoRoot ?? "";
    }

    /// <summary>
    /// On open: bring up Ollama + Host so an installed Presence can chat without a separate ALLSTART.
    /// Always reloads companion token from SoulCore/.env (Velopack installs are outside the repo).
    /// </summary>
    private async Task EnsureLocalStackOnOpenAsync()
    {
        if (_stackBootstrapBusy)
            return;
        _stackBootstrapBusy = true;
        try
        {
            // Persist discovered root so next launch (Velopack) finds it without env.
            if (string.IsNullOrWhiteSpace(_uiSettings.SoulCoreRepoRoot)
                && !string.IsNullOrWhiteSpace(_stack.RepoRoot))
            {
                _uiSettings.SoulCoreRepoRoot = _stack.RepoRoot;
                _uiSettings.Save();
                if (SoulCoreRepoRootBox is not null)
                    SoulCoreRepoRootBox.Text = _stack.RepoRoot;
            }

            // Critical for Setup.exe installs: Program.cs ran too early to find SoulCore/.env.
            // Settings-saved token (if any) overlays .env.
            CompanionToken.ApplyAllSources(_uiSettings.SoulCoreRepoRoot ?? _stack.RepoRoot);
            RefreshCompanionTokenStatus();

            if (!_uiSettings.AutoStartStack)
            {
                AppendSystemNotice(
                    $"Auto-start off. {CompanionToken.DescribePresence()}. " +
                    "Turn on Settings → System → Auto-start, or run ALLSTART.");
                return;
            }

            if (_stack.RepoRoot is null)
            {
                AppendSystemNotice(
                    "SoulCore repo not found — set Settings → System → SoulCore repo folder " +
                    @"(e.g. C:\Users\LinearThrone\Soul_Core) then reopen Presence.");
                return;
            }

            AppendSystemNotice($"Starting local stack from {_stack.RepoRoot}…");
            var progress = new Progress<string>(msg =>
            {
                if (ServicesStatusText is not null)
                    ServicesStatusText.Text = msg;
            });

            var result = await _stack.EnsureStackForChatAsync(progress: progress).ConfigureAwait(true);
            // Host may have loaded .env; Presence must match before WS connect.
            CompanionToken.ApplyAllSources(_uiSettings.SoulCoreRepoRoot ?? _stack.RepoRoot);
            RefreshCompanionTokenStatus();

            if (result.Ok)
            {
                AppendSystemNotice(
                    (result.HostStarted || result.OllamaStarted
                        ? $"Stack ready — {result.Detail}"
                        : "Stack already running.")
                    + $" · {CompanionToken.DescribePresence()}");
            }
            else
            {
                AppendSystemNotice($"Could not auto-start stack: {result.Detail}");
            }
        }
        catch (Exception ex)
        {
            AppendSystemNotice($"Stack auto-start error: {ex.Message}");
        }
        finally
        {
            _stackBootstrapBusy = false;
        }
    }

    private void ReloadCompanionTokenFromRepo()
    {
        CompanionToken.ApplyAllSources(_uiSettings.SoulCoreRepoRoot ?? _stack.RepoRoot);
        RefreshCompanionTokenStatus();
        var envPath = CompanionToken.ResolvedEnvFilePath(_uiSettings.SoulCoreRepoRoot ?? _stack.RepoRoot);
        if (!CompanionTokenStore.HasSavedToken()
            && CompanionToken.Resolve() is null
            && envPath is null)
        {
            AppendSystemNotice(
                "No companion token yet — paste SOULCORE_COMPANION_API_TOKEN in Settings → System, or set SoulCore repo folder so .env can load.");
        }
    }

    private void AppendSystemNotice(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        // PROP-22.4: system notices never enter the transcript — they land in Settings → Performance log.
        _performanceLog.Add(text);
    }

    private async Task ProbeHealthAsync()
    {
        var snap = await _health.ProbeAsync();
        _lastHealth = snap;

        ApplyServiceIndicators(snap);
        await RefreshServicesPanelAsync(snap).ConfigureAwait(true);
        ApplyHonestActivity(preferHost: true);
        await RefreshBandTasksAsync().ConfigureAwait(true);
        RefreshHonestyBand();
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
    /// operator default: desktop + browser capture, computer control, soft cursor ON.
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

    /// <summary>
    /// PROP-22.5: probe the local stack (Ollama / ComfyUI / KsDevice VM) and feed the status orb.
    /// The old lamp tray is gone — this only caches the facts the aggregator needs.
    /// </summary>
    private async Task RefreshServicesPanelAsync(SoulCoreHealthSnapshot? health = null)
    {
        _ = health;
        var ollamaUp = await _stack.ProbeOllamaAsync().ConfigureAwait(true);
        var comfyUp = await _stack.ProbeComfyAsync().ConfigureAwait(true);
        var sandboxUp = await _stack.ProbeSandboxAsync().ConfigureAwait(true);

        _ollamaUp = ollamaUp;
        _comfyUp = comfyUp;
        NoteSandboxRunning(sandboxUp);
        RefreshStatusOrb();
    }

    /// <summary>
    /// Stop stays available during “Starting Host…” and force-kills via LocalStackControl.
    /// </summary>
    private async Task InterruptOrStopHostAsync()
    {
        if (_servicesBusy && _activeServiceTag is "host-start" or "host-restart")
        {
            // Supersede in-flight start/restart so its finally cannot stick “Starting Host…”.
            var stopGen = ++_hostActionGeneration;
            try { _hostActionCts?.Cancel(); }
            catch (ObjectDisposedException) { /* already disposed */ }

            WriteHostActionStatus("Stopping Host…");

            PersistStackSettingsFromUi();
            LocalStackActionResult result;
            try
            {
                result = await _stack.StopHostAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                result = LocalStackActionResult.Fail(ex.Message);
            }

            if (stopGen != _hostActionGeneration)
                return;

            WriteHostActionStatus(result.Ok ? $"OK: {result.Detail}" : $"Fail: {result.Detail}");

            _servicesBusy = false;
            _activeServiceTag = null;
            SetHostStackButtonsBusy(false);
            await ProbeHealthAsync().ConfigureAwait(true);
            return;
        }

        await RunServiceActionAsync("host-stop").ConfigureAwait(true);
    }

    private async Task RunServiceActionAsync(string tag)
    {
        if (_servicesBusy) return;

        var gen = ++_hostActionGeneration;
        _servicesBusy = true;
        _activeServiceTag = tag;
        DisposeHostActionCts();
        var cts = new CancellationTokenSource();
        _hostActionCts = cts;
        SetHostStackButtonsBusy(true, tag);
        var hostStackTag = tag is "host-start" or "host-stop" or "host-restart";
        WriteHostActionStatus(
            tag switch
            {
                "host-start" => "Starting Host…",
                "host-stop" => "Stopping Host…",
                "host-restart" => "Restarting Host…",
                _ => $"Running {tag}…"
            },
            includeHostStack: hostStackTag);

        try
        {
            LocalStackActionResult result;
            switch (tag)
            {
                case "host-start":
                {
                    PersistStackSettingsFromUi();
                    var ensure = await _stack.EnsureStackForChatAsync(cts.Token).ConfigureAwait(true);
                    result = ensure.Ok
                        ? LocalStackActionResult.Succeed(ensure.Detail)
                        : LocalStackActionResult.Fail(ensure.Detail);
                    break;
                }
                case "host-stop":
                    PersistStackSettingsFromUi();
                    result = await _stack.StopHostAsync(cts.Token).ConfigureAwait(true);
                    break;
                case "host-restart":
                    PersistStackSettingsFromUi();
                    result = await _stack.RestartHostAsync(cts.Token).ConfigureAwait(true);
                    break;
                case "ollama-start":
                    result = await _stack.StartOllamaAsync(cts.Token).ConfigureAwait(true);
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

            // Stop interrupt supersedes this generation — do not restore “Starting Host…” / busy.
            if (gen != _hostActionGeneration)
                return;

            var msg = result.Ok ? $"OK: {result.Detail}" : $"Fail: {result.Detail}";
            WriteHostActionStatus(msg, includeHostStack: hostStackTag);

            if (result.Ok && tag is "host-start" or "host-restart")
            {
                CompanionToken.ApplyAllSources(_uiSettings.SoulCoreRepoRoot ?? _stack.RepoRoot);
                await _ws.ConnectAsync().ConfigureAwait(true);
            }

            await ProbeHealthAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            if (gen != _hostActionGeneration)
                return;
            WriteHostActionStatus(
                "Cancelled — Host start stopped.",
                includeHostStack: hostStackTag);
        }
        catch (Exception ex)
        {
            if (gen != _hostActionGeneration)
                return;
            WriteHostActionStatus(
                $"Error: {ex.Message}",
                includeHostStack: hostStackTag);
        }
        finally
        {
            // Dispose only our CTS — Stop interrupt cancels but must not dispose while we still await.
            if (ReferenceEquals(_hostActionCts, cts))
                DisposeHostActionCts();

            if (gen == _hostActionGeneration)
            {
                _servicesBusy = false;
                _activeServiceTag = null;
                SetHostStackButtonsBusy(false);
            }
        }
    }

    private void WriteHostActionStatus(
        string message,
        bool includeServices = true,
        bool includeHostStack = true)
    {
        if (includeServices && ServicesStatusText is not null)
            ServicesStatusText.Text = message;
        if (includeHostStack && HostStackStatusText is not null)
            HostStackStatusText.Text = message;
    }

    /// <summary>
    /// While Starting/Restarting Host, Stop stays enabled for force-kill.
    /// </summary>
    private void SetHostStackButtonsBusy(bool busy, string? activeTag = null)
    {
        if (HostStartButton is not null)
            HostStartButton.IsEnabled = !busy;
        if (HostRestartButton is not null)
            HostRestartButton.IsEnabled = !busy;
        if (HostStopButton is not null)
            HostStopButton.IsEnabled = HostStackBusyPolicy.IsHostStopEnabled(busy, activeTag);
    }

    private void DisposeHostActionCts()
    {
        if (_hostActionCts is null) return;
        try { _hostActionCts.Dispose(); }
        catch (ObjectDisposedException) { /* already disposed */ }
        _hostActionCts = null;
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
        await RefreshEmailAccountsAsync().ConfigureAwait(true);
        await RefreshVoiceStatusAsync().ConfigureAwait(true);
        ApplySystemStatus(_lastHealth);
        RefreshCompanionTokenStatus();
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
        _voiceEnabled = h.Enabled;
        _voiceSttOk = h.Stt?.Ok == true;
        _voiceTtsOk = h.Tts?.Ok == true;
        RefreshStatusOrb();
        var compact = h.Enabled
            ? $"Voice · STT={(h.Stt?.Ok == true ? "ok" : "down")} · TTS={(h.Tts?.Ok == true ? "ok" : "down")}"
            : "Voice off in Host";
        if (VoiceHealthCompactText is not null)
            VoiceHealthCompactText.Text = compact;
        if (SystemVoiceHealthText is not null)
        {
            SystemVoiceHealthText.Text = h.Enabled
                ? $"enabled · STT {(h.Stt?.Ok == true ? "ok" : "down")} · TTS {(h.Tts?.Ok == true ? "ok" : "down")}"
                : "Voice off in Host — Hold to talk waits for STT";
        }
        if (SystemVoiceUrlsText is not null)
        {
            SystemVoiceUrlsText.Text =
                $"STT: {h.Stt?.Url ?? "(not reported)"}\nTTS: {h.Tts?.Url ?? "(not reported)"}";
        }
        if (PttHintText is not null
            && (string.IsNullOrWhiteSpace(PttHintText.Text)
                || PttHintText.Text.StartsWith("Voice", StringComparison.Ordinal)))
        {
            // The compact STT/TTS line lives on the right; the hint area only explains an off Host.
            PttHintText.Text = h.Enabled
                ? string.Empty
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
            SetPttLive(true);
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
            SetPttLive(true);
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
            SetPttLive(false);
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

    private async void EmailSetup_Click(object? sender, RoutedEventArgs e)
    {
        var dlg = new EmailSetupWindow(_emailSettings);
        await dlg.ShowDialog(this).ConfigureAwait(true);
        await RefreshEmailAccountsAsync().ConfigureAwait(true);
    }

    private void WhileYouWereGoneDismiss_Click(object? sender, RoutedEventArgs e)
    {
        if (WhileYouWereGonePanel is not null)
            WhileYouWereGonePanel.IsVisible = false;
    }

    /// <summary>PROP-18.3: chat-open strip from Host store; empty is honest.</summary>
    private async Task LoadWhileYouWereGoneOnOpenAsync()
    {
        if (_whileYouWereGoneLoaded)
            return;
        _whileYouWereGoneLoaded = true;

        var since = _uiSettings.LastPresenceSeenAtUtc;
        var fetch = await _activities.WhileYouWereGoneAsync(since).ConfigureAwait(true);
        var body = PresenceResidueFormatter.FormatWhileYouWereGoneBody(fetch);
        if (WhileYouWereGoneText is not null)
            WhileYouWereGoneText.Text = body;
        if (WhileYouWereGonePanel is not null)
            WhileYouWereGonePanel.IsVisible = true;

        // PROP-18.31 F2: Last journal is journal crumbs only — no activity Desc fallback.
        var journal = PresenceResidueFormatter.NewestJournalPreview(fetch);
        _lastJournalCrumb = string.IsNullOrWhiteSpace(journal) ? null : journal;

        RefreshHonestyBand();
    }

    private void PersistPresenceSeenAt()
    {
        _uiSettings.LastPresenceSeenAtUtc =
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
        _uiSettings.Save();
    }

    private void RefreshHonestyBand()
    {
        if (BandHonestyText is null)
            return;
        BandHonestyText.Text = PresenceResidueFormatter.FormatHonestyLine(
            _lastHealth.Alive,
            _lastTickUtc,
            _lastWantPhrase,
            _lastJournalCrumb);
    }

    private async void EmailGateToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (_toolsAccessHydrating) return;
        if (EmailGateReadCheck is null || EmailGateSendCheck is null || EmailGateDeleteCheck is null)
            return;

        if (ToolsAllowEmailReadCheck is not null)
            ToolsAllowEmailReadCheck.IsChecked = EmailGateReadCheck.IsChecked;
        if (ToolsAllowEmailSendCheck is not null)
            ToolsAllowEmailSendCheck.IsChecked = EmailGateSendCheck.IsChecked;
        if (ToolsAllowEmailDeleteCheck is not null)
            ToolsAllowEmailDeleteCheck.IsChecked = EmailGateDeleteCheck.IsChecked;

        var snap = await _toolsSettings.PatchAsync(
            allowEmailRead: EmailGateReadCheck.IsChecked == true,
            allowEmailSend: EmailGateSendCheck.IsChecked == true,
            allowEmailDelete: EmailGateDeleteCheck.IsChecked == true).ConfigureAwait(true);

        ApplyToolsAccess(snap, saved: true);
        SyncEmailGateChecks(snap);
    }

    private async Task RefreshEmailAccountsAsync()
    {
        var snap = await _emailSettings.GetAsync().ConfigureAwait(true);
        _emailAccounts = snap.Accounts ?? Array.Empty<EmailAccountSnapshot>();
        ApplySystemMailboxesList();
        var tools = await _toolsSettings.GetAsync().ConfigureAwait(true);
        SyncEmailGateChecks(tools);
    }

    private void ApplySystemMailboxesList()
    {
        if (SystemMailboxesText is null) return;
        if (_emailAccounts.Count == 0)
        {
            SystemMailboxesText.Text = "No mailboxes yet — Settings → Advanced → Mailbox form.";
            return;
        }

        SystemMailboxesText.Text = string.Join(
            "\n",
            _emailAccounts.Select(EmailMailboxChoice.FormatSystemListLine));
    }

    private void SyncEmailGateChecks(ToolsAccessSnapshot snap)
    {
        _toolsAccessHydrating = true;
        try
        {
            if (EmailGateReadCheck is not null) EmailGateReadCheck.IsChecked = snap.AllowEmailRead;
            if (EmailGateSendCheck is not null) EmailGateSendCheck.IsChecked = snap.AllowEmailSend;
            if (EmailGateDeleteCheck is not null) EmailGateDeleteCheck.IsChecked = snap.AllowEmailDelete;
            if (EmailGateStatusText is not null)
            {
                EmailGateStatusText.Text = snap.Reachable
                    ? (snap.AllowEmailRead || snap.AllowEmailSend || snap.AllowEmailDelete
                        ? "Permissions saved for this Host session"
                        : "All mail actions are off until you turn one on")
                    : (snap.Detail ?? "Host unreachable");
            }
        }
        finally
        {
            _toolsAccessHydrating = false;
        }
    }

    private async void VmStart_Click(object? sender, RoutedEventArgs e)
    {
        if (VmStatusText is not null) VmStatusText.Text = "Starting KsDevice…";
        var result = await _stack.StartSandboxVmAsync().ConfigureAwait(true);
        if (VmStatusText is not null)
            VmStatusText.Text = result.Ok ? $"Start: {result.Detail}" : $"Start failed: {result.Detail}";
        await RefreshServicesPanelAsync().ConfigureAwait(true);
    }

    private async void VmSave_Click(object? sender, RoutedEventArgs e)
    {
        if (VmStatusText is not null) VmStatusText.Text = "Saving KsDevice state…";
        var result = await _stack.SaveSandboxVmAsync().ConfigureAwait(true);
        if (VmStatusText is not null)
            VmStatusText.Text = result.Ok ? $"Save state: {result.Detail}" : $"Save failed: {result.Detail}";
        await RefreshServicesPanelAsync().ConfigureAwait(true);
    }

    private async void VmPowerOff_Click(object? sender, RoutedEventArgs e)
    {
        var ok = await ConfirmYesNoAsync(
            "Power off KsDevice now?\n\nUnsaved guest work may be lost. Prefer Save state when you can.",
            "Power off VM").ConfigureAwait(true);
        if (!ok) return;

        if (VmStatusText is not null) VmStatusText.Text = "Powering off KsDevice…";
        var result = await _stack.PowerOffSandboxVmAsync().ConfigureAwait(true);
        if (VmStatusText is not null)
            VmStatusText.Text = result.Ok ? $"Power off: {result.Detail}" : $"Power off failed: {result.Detail}";
        await RefreshServicesPanelAsync().ConfigureAwait(true);
    }

    private async void ToolsAccessToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (_toolsAccessHydrating) return;
        if (ToolsAllowDesktopCaptureCheck is null
            || ToolsAllowBrowserCaptureCheck is null
            || ToolsAllowComputerControlCheck is null
            || ToolsSoftCursorRestoreCheck is null
            || ToolsVmEmbedPaneCheck is null
            || ToolsAllowMt4ReadCheck is null
            || ToolsAllowMt4TradeCheck is null
            || ToolsAllowEmailReadCheck is null
            || ToolsAllowEmailSendCheck is null
            || ToolsAllowEmailDeleteCheck is null
            || ToolsAllowGuestSudoCheck is null)
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
            vmEmbedPane: ToolsVmEmbedPaneCheck.IsChecked == true,
            allowMt4Read: ToolsAllowMt4ReadCheck.IsChecked == true,
            allowMt4Trade: ToolsAllowMt4TradeCheck.IsChecked == true,
            allowEmailRead: ToolsAllowEmailReadCheck.IsChecked == true,
            allowEmailSend: ToolsAllowEmailSendCheck.IsChecked == true,
            allowEmailDelete: ToolsAllowEmailDeleteCheck.IsChecked == true,
            allowGuestSudo: ToolsAllowGuestSudoCheck.IsChecked == true).ConfigureAwait(true);

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
            || ToolsVmEmbedPaneCheck is null
            || ToolsAllowMt4ReadCheck is null
            || ToolsAllowMt4TradeCheck is null
            || ToolsAllowEmailReadCheck is null
            || ToolsAllowEmailSendCheck is null
            || ToolsAllowEmailDeleteCheck is null
            || ToolsAllowGuestSudoCheck is null)
        {
            return;
        }

        // HTTP 401/4xx: keep current checkbox state (empty DTO defaults would snap everything off).
        if (snap.IsHttpError)
        {
            if (ToolsAccessStatusText is not null)
            {
                ToolsAccessStatusText.Text = snap.Detail ?? "HTTP error";
                ToolsAccessStatusText.Foreground = _badBrush;
            }

            ApplyServiceIndicators(tools: snap);
            return;
        }

        _toolsAccessHydrating = true;
        try
        {
            ToolsAllowDesktopCaptureCheck.IsChecked = snap.Reachable ? snap.AllowDesktopCapture : true;
            ToolsAllowBrowserCaptureCheck.IsChecked = snap.Reachable ? snap.AllowBrowserCapture : true;
            ToolsAllowComputerControlCheck.IsChecked = snap.Reachable ? snap.AllowComputerControl : true;
            ToolsSoftCursorRestoreCheck.IsChecked = snap.Reachable ? snap.SoftCursorRestore : true;
            ToolsVmEmbedPaneCheck.IsChecked = snap.Reachable ? snap.VmEmbedPane : true;
            ToolsAllowMt4ReadCheck.IsChecked = snap.AllowMt4Read;
            ToolsAllowMt4TradeCheck.IsChecked = snap.AllowMt4Trade;
            ToolsAllowEmailReadCheck.IsChecked = snap.AllowEmailRead;
            ToolsAllowEmailSendCheck.IsChecked = snap.AllowEmailSend;
            ToolsAllowEmailDeleteCheck.IsChecked = snap.AllowEmailDelete;
            ToolsAllowGuestSudoCheck.IsChecked = snap.AllowGuestSudo;

            if (EmailGateReadCheck is not null) EmailGateReadCheck.IsChecked = snap.AllowEmailRead;
            if (EmailGateSendCheck is not null) EmailGateSendCheck.IsChecked = snap.AllowEmailSend;
            if (EmailGateDeleteCheck is not null) EmailGateDeleteCheck.IsChecked = snap.AllowEmailDelete;
            if (EmailGateStatusText is not null)
            {
                EmailGateStatusText.Text = snap.Reachable
                    ? (snap.AllowEmailRead || snap.AllowEmailSend || snap.AllowEmailDelete
                        ? "Permissions loaded"
                        : "All mail actions are off until you turn one on")
                    : (snap.Detail ?? "Host unreachable");
            }

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

        ToolsAccessStatusText.Text = saved
            ? $"Saved · session until Host restart · {DateTimeOffset.Now:h:mm tt}"
            : $"Loaded · {(snap.Scope ?? "session")} · {DateTimeOffset.Now:h:mm tt}";
        ToolsAccessStatusText.Foreground = Res("MutedBrush");
        ApplyServiceIndicators(tools: snap);
    }

    private void ApplySystemStatus(SoulCoreHealthSnapshot snap)
    {
        if (SystemBindBox is null || SystemPortBox is null
            || SystemInferenceBox is null
            || SystemMemoryPathBox is null || SystemMemoryOpenBox is null
            || SystemSoulLoopBox is null)
        {
            return;
        }

        if (SystemEndpointBox is not null)
            SystemEndpointBox.Text = ConnectionDefaults.DisplayEndpoint;
        // PROP-22.3: the Host bind is fixed loopback — shown, never editable.
        SystemBindBox.Text = ConnectionDefaults.DefaultHost;
        SystemPortBox.Text = ConnectionDefaults.Port.ToString();
        if (SystemVersionsBox is not null)
        {
            var presence = _updates?.CurrentVersion ?? "—";
            var host = string.IsNullOrWhiteSpace(snap.HostVersion) ? "—" : snap.HostVersion.Trim();
            SystemVersionsBox.Text = $"Presence {presence} · Host {host}";
        }

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

        SystemInferenceBox.Text = snap.ActiveModelLabel();
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
        }
        else if (snap.Alive && snap.Warm)
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

        RefreshBuildVersionChrome(snap.HostVersion);
        RefreshStatusOrb();
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
                _lastTickUtc = DateTimeOffset.UtcNow;
                RefreshHonestyBand();
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
                AppendSystemNotice($"{code ?? "error"}: {msg}");
                break;
            case SoulCoreFrameTypes.Pong:
                break;
            case SoulCoreFrameTypes.VoiceSpeaking:
                ApplyVoiceSpeaking(frame);
                break;
            case SoulCoreFrameTypes.VoiceAudio:
                ApplyVoiceAudio(frame);
                break;
            case SoulCoreFrameTypes.VoiceTranscript:
                ApplyVoiceTranscript(frame);
                break;
            default:
                break;
        }
    }

    private async void CallToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (_callActive)
        {
            await EndCallAsync().ConfigureAwait(true);
            return;
        }

        await StartCallAsync().ConfigureAwait(true);
    }

    private async void CallEnd_Click(object? sender, RoutedEventArgs e) =>
        await EndCallAsync().ConfigureAwait(true);

    private async Task StartCallAsync()
    {
        if (_callActive) return;

        // PROP-22.4: connecting = glass scrim over the whole content area.
        SetCallPhase(CallPhase.Connecting);
        if (CallStatusText is not null) CallStatusText.Text = "Connecting…";
        if (CallTranscriptPreview is not null) CallTranscriptPreview.Text = "";

        var ok = await _ws.SendVoiceCallStartAsync().ConfigureAwait(true);
        if (!ok)
        {
            // Overlay stays up (full) so the reason is readable; End call dismisses it.
            if (CallStatusText is not null)
                CallStatusText.Text = _ws.LastError ?? "Call start failed — Host WS?";
            return;
        }

        _callActive = true;
        // Connected = overlay shrinks to the top third; the transcript stays usable below.
        SetCallPhase(CallPhase.Connected);
        if (CallStatusText is not null) CallStatusText.Text = "Listening…";
        CallSphere?.SetSpeaking(false);

        _callMic.ChunkReady -= OnCallMicChunk;
        _callMic.ChunkReady += OnCallMicChunk;
        _callMic.Start();
    }

    private async Task EndCallAsync()
    {
        _callMic.ChunkReady -= OnCallMicChunk;
        _callMic.Stop();
        _callPlayer.Stop();
        if (_callActive)
            await _ws.SendVoiceCallEndAsync().ConfigureAwait(true);
        _callActive = false;
        SetCallPhase(CallPhase.None);
        CallSphere?.SetSpeaking(false);
    }

    private void OnCallMicChunk(byte[] pcm)
    {
        if (!_callActive || pcm.Length == 0) return;
        var b64 = Convert.ToBase64String(pcm);
        _ = _ws.SendVoiceCallAudioAsync(b64);
    }

    private void ApplyVoiceSpeaking(SoulCoreFrame frame)
    {
        var speaking = ReadPayloadBool(frame, "speaking") == true;
        var level = ReadPayloadDouble(frame, "level") ?? (speaking ? 0.55 : 0.1);
        CallSphere?.SetSpeaking(speaking, level);
        if (CallStatusText is not null)
            CallStatusText.Text = speaking ? "Speaking…" : (_callActive ? "Listening…" : "");
    }

    private void ApplyVoiceAudio(SoulCoreFrame frame)
    {
        var end = ReadPayloadBool(frame, "end") == true;
        var rate = (int)(ReadPayloadDouble(frame, "sampleRate") ?? 24000);
        var b64 = ReadPayloadString(frame, "audio");
        if (!string.IsNullOrEmpty(b64))
        {
            try
            {
                var pcm = Convert.FromBase64String(b64);
                _callPlayer.EnsureFormat(rate);
                _callPlayer.AddPcm(pcm);
                CallSphere?.SetSpeaking(true, 0.7);
            }
            catch
            {
                /* ignore bad chunk */
            }
        }

        if (end)
            CallSphere?.SetSpeaking(false);
    }

    private void ApplyVoiceTranscript(SoulCoreFrame frame)
    {
        var text = ReadPayloadString(frame, "text");
        var final = ReadPayloadBool(frame, "final") == true;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (CallTranscriptPreview is not null)
            CallTranscriptPreview.Text = text;
        if (final && _callActive)
        {
            // Final user utterance → chat.send so Victoria answers over Call TTS stream.
            _ = SendChatFromCallAsync(text);
        }
    }

    private async Task SendChatFromCallAsync(string text)
    {
        if (CallStatusText is not null)
            CallStatusText.Text = "Thinking…";
        _messages.Add(new ChatMessage { Role = "user", Text = text });
        ScrollTranscriptToEnd();
        await _ws.SendChatAsync(text).ConfigureAwait(true);
    }

    private void ApplyPresenceStatus(SoulCoreFrame frame)
    {
        _presenceFromWs = true;
        var activity = ReadPayloadString(frame, "activity");
        if (!string.IsNullOrWhiteSpace(activity))
        {
            _lastActivityPhrase = activity;
            _presenceCurrentActivity = activity.Trim();
            if (ActivityText is not null)
                ActivityText.Text = activity;
            if (TypingLabel is not null)
                TypingLabel.Text = activity;
        }

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
        _lastWantPhrase = string.IsNullOrWhiteSpace(parsed.Phrase) ? want?.Trim() : parsed.Phrase;
        _lastWantUtc = DateTimeOffset.UtcNow;
        // A want frame implies a tick completed on Host.
        _lastTickUtc ??= DateTimeOffset.UtcNow;

        UpdateEngagementState();
        ApplyHonestActivity(preferHost: true);
        RefreshHonestyBand();
    }

    /// <summary>
    /// PROP-4: activity = doing-now from Host presence.currentActivity / desktop LastAction / chat.
    /// Never raw SoulLoop want slogans.
    /// </summary>
    private void ApplyHonestActivity(bool preferHost = false)
    {
        // Host currentActivity is the only authority. Empty means nothing is open —
        // never invent "Working" / "No open assignment" (PROP-22 honesty rule).
        if (preferHost)
        {
            var host = _lastHealth.CurrentActivity;
            _presenceCurrentActivity = string.IsNullOrWhiteSpace(host) ? null : host.Trim();
            _lastActivityPhrase = _presenceCurrentActivity;
        }

        if (ActivityText is not null)
            ActivityText.Text = _presenceCurrentActivity ?? NothingRightNow;
        RefreshActivityBand();
        RefreshStatusOrb();
    }

    /// <summary>Victoria drawer: doing-now, on her mind, last activity, mood.</summary>
    private void RefreshActivityBand()
    {
        if (DoingNowText is not null)
            DoingNowText.Text = _presenceCurrentActivity ?? NothingRightNow;
        if (BandDoingText is not null)
            BandDoingText.Text = _presenceCurrentActivity ?? NothingRightNow;

        var mind = string.IsNullOrWhiteSpace(_lastHealth.OnHerMind)
            ? "quiet between talks"
            : _lastHealth.OnHerMind.Trim();
        if (BandOnHerMindText is not null)
            BandOnHerMindText.Text = $"On her mind — {mind}";

        RefreshBandMoodLine();
    }
    private bool _taskMutateBusy;

    private async Task RefreshBandTasksAsync()
    {
        var fetch = await _taskClient.ListAsync().ConfigureAwait(true);
        if (BandTaskList is not null)
            BandTaskList.ItemsSource = fetch.Tasks;
        if (BandTasksText is null)
            return;

        if (!string.IsNullOrWhiteSpace(fetch.Problem))
        {
            BandTasksText.IsVisible = true;
            BandTasksText.Text = fetch.Problem;
            return;
        }

        if (fetch.Tasks.Count == 0)
        {
            BandTasksText.IsVisible = true;
            var hostCount = _lastHealth.TaskCount;
            BandTasksText.Text = hostCount is > 0
                ? $"No tasks in list, but Host health reports {hostCount}. Restart Host / Presence."
                : "No tasks.";
            return;
        }

        BandTasksText.IsVisible = false;
        BandTasksText.Text = "No tasks.";
    }

    private async void BandTaskAdd_Click(object? sender, RoutedEventArgs e)
    {
        if (_taskMutateBusy)
            return;

        var dlg = new TaskAddWindow();
        var accepted = await dlg.ShowDialog<bool>(this).ConfigureAwait(true);
        if (!accepted || string.IsNullOrWhiteSpace(dlg.TitleText))
            return;

        _taskMutateBusy = true;
        try
        {
            var result = await _taskClient.CreateAsync(dlg.TitleText).ConfigureAwait(true);
            await RefreshBandTasksAsync().ConfigureAwait(true);
            if (!result.Ok && BandTasksText is not null)
            {
                BandTasksText.IsVisible = true;
                BandTasksText.Text = result.Problem ?? "Could not add task.";
            }
        }
        finally
        {
            _taskMutateBusy = false;
        }
    }

    private async void BandTaskSuspend_Click(object? sender, RoutedEventArgs e)
    {
        if (_taskMutateBusy)
            return;
        if (sender is not Button { DataContext: PresenceTaskRow row } || row.Id <= 0)
            return;

        _taskMutateBusy = true;
        try
        {
            var result = await _taskClient.SuspendAsync(row.Id).ConfigureAwait(true);
            await RefreshBandTasksAsync().ConfigureAwait(true);
            if (!result.Ok && BandTasksText is not null)
            {
                BandTasksText.IsVisible = true;
                BandTasksText.Text = result.Problem ?? $"Could not suspend task #{row.Id}.";
            }
        }
        finally
        {
            _taskMutateBusy = false;
        }
    }

    private async void BandTaskRelease_Click(object? sender, RoutedEventArgs e)
    {
        if (_taskMutateBusy)
            return;
        if (sender is not Button { DataContext: PresenceTaskRow row } || row.Id <= 0)
            return;

        _taskMutateBusy = true;
        try
        {
            var result = await _taskClient.ReleaseAsync(row.Id).ConfigureAwait(true);
            await RefreshBandTasksAsync().ConfigureAwait(true);
            if (!result.Ok && BandTasksText is not null)
            {
                BandTasksText.IsVisible = true;
                BandTasksText.Text = result.Problem ?? $"Could not release task #{row.Id}.";
            }
        }
        finally
        {
            _taskMutateBusy = false;
        }
    }

    private async void BandTaskDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (_taskMutateBusy)
            return;
        if (sender is not Button { DataContext: PresenceTaskRow row } || row.Id <= 0)
            return;

        var title = string.IsNullOrWhiteSpace(row.Title) ? "(untitled)" : row.Title.Trim();
        var ok = await ConfirmYesNoAsync(
            $"Delete task #{row.Id} permanently?\n\n{title}\n\nShe can also delete via task_delete. Neither of you can restore it.",
            "Delete task").ConfigureAwait(true);
        if (!ok)
            return;

        _taskMutateBusy = true;
        try
        {
            var removed = await _taskClient.DeleteAsync(row.Id).ConfigureAwait(true);
            await RefreshBandTasksAsync().ConfigureAwait(true);
            if (!removed && BandTasksText is not null)
            {
                BandTasksText.IsVisible = true;
                BandTasksText.Text = $"Could not delete task #{row.Id}.";
            }
        }
        finally
        {
            _taskMutateBusy = false;
        }
    }

    private void RefreshBandMoodLine()
    {
        if (BandMoodText is null) return;
        var label = MoodLabelText?.Text;
        if (string.IsNullOrWhiteSpace(label) || label == "—")
            label = "—";
        var warmCool = _lastValence >= 0.25 ? "warm" : _lastValence <= -0.25 ? "cool" : "neutral";
        var keyed = _lastArousal >= 0.55 ? "keyed-up" : "calm";
        var steady = Math.Abs(_lastDominance - 0.5) < 0.2 ? "steady" : (_lastDominance >= 0.5 ? "firm" : "soft");
        var focused = _lastFocus >= 0.55 ? "focused" : "open";
        BandMoodText.Text = $"Mood — {label} · {warmCool} · {keyed} · {steady} · {focused}";
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

        if (MoodColorDot is not null)
        {
            MoodColorDot.Fill = valence >= 0.25 ? _okBrush
                : valence <= -0.25 ? _warnBrush
                : Res("AccentBrush");
        }

        RefreshBandMoodLine();
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
        var activityLine = ActivityText?.Text?.Trim() ?? "";
        if (activityLine.StartsWith("Working", StringComparison.OrdinalIgnoreCase))
        {
            state = "Working";
            brush = _okBrush;
        }
        else if (activityLine.StartsWith("Waiting on you", StringComparison.OrdinalIgnoreCase)
                 || activityLine.StartsWith("Stuck", StringComparison.OrdinalIgnoreCase))
        {
            state = "Waiting";
            brush = _warnBrush;
        }
        else if (soulOff || sleepCat || (deepQuiet && _lastArousal < 0.15 && !recentChat))
        {
            state = "Sleeping";
            brush = _mutedBrush;
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
        // PROP-1.2: SMS/MMS from operator mirrored into Presence transcript.
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
        var continuing = ReadPayloadBool(frame, "continuing") == true;
        SetTyping(!finalize || continuing);
        if (TypingLabel is not null && (continuing || !finalize))
        {
            var activity = ReadPayloadString(frame, "activity");
            if (!string.IsNullOrWhiteSpace(activity))
                TypingLabel.Text = activity;
            else if (continuing)
                TypingLabel.Text = "Still working";
        }
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
                SetTyping(ReadPayloadBool(frame, "continuing") == true);
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
            SetTyping(ReadPayloadBool(frame, "continuing") == true);
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
