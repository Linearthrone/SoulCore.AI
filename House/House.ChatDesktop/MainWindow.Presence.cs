using System.IO;
using System.Text.Json;
using Avalonia.Controls;
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
        UpdateIdentityDetail();
        UpdateEngagementState();

        if (snap.Alive
            && _ws.State is WsConnectionState.Unavailable or WsConnectionState.Disconnected)
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
        if (SvcHostDot is null)
            return;

        var snap = health ?? _lastHealth;
        var ollamaUp = await _stack.ProbeOllamaAsync().ConfigureAwait(true);
        var hermesUp = snap.HermesGatewayUp ?? await _stack.ProbeHermesAsync().ConfigureAwait(true);
        var comfyUp = await _stack.ProbeComfyAsync().ConfigureAwait(true);

        SvcHostDot.Fill = snap.Alive ? _okBrush : _badBrush;
        SvcHostStatus.Text = snap.Alive ? "up" : "down";
        SvcHostDetail.Text = snap.Alive
            ? $"http://{ConnectionDefaults.DisplayEndpoint}/health"
            : (snap.Detail ?? "Host not answering");

        SvcOllamaDot.Fill = ollamaUp ? _okBrush : _badBrush;
        SvcOllamaStatus.Text = ollamaUp ? "up" : "down";
        SvcOllamaDetail.Text = ollamaUp
            ? (snap.InferenceEnabled ? "tags OK · inference on" : "tags OK · inference off")
            : "unreachable :11434";

        SvcHermesDot.Fill = hermesUp
            ? _okBrush
            : (snap.HermesEnabled ? _warnBrush : _mutedBrush);
        SvcHermesStatus.Text = "retired";
        SvcHermesDetail.Text = "BED-185: unused — open Chrome via desktop_open_app (Ollama)";
        SvcHermesDot.Fill = _mutedBrush;
        _ = hermesUp; // legacy probe retained; UI no longer steers Kurt to start Hermes

        var backend = snap.DesktopBackend ?? "cua";
        var driverOk = snap.CuaDriverAvailable != false || !backend.Equals("cua", StringComparison.OrdinalIgnoreCase);
        if (!snap.Reachable)
        {
            SvcCuaDot.Fill = _badBrush;
            SvcCuaStatus.Text = "host down";
            SvcCuaDetail.Text = "Enable needs Host /settings/tools";
        }
        else if (snap.CuaDriverAvailable == false && backend.Equals("cua", StringComparison.OrdinalIgnoreCase))
        {
            SvcCuaDot.Fill = _badBrush;
            SvcCuaStatus.Text = "driver missing";
            SvcCuaDetail.Text = "cua-driver not found";
        }
        else if (_lastAllowComputerControl == true && driverOk)
        {
            SvcCuaDot.Fill = _okBrush;
            SvcCuaStatus.Text = "control on";
            SvcCuaDetail.Text = $"backend={backend}";
        }
        else if (_lastAllowDesktopCapture == true)
        {
            SvcCuaDot.Fill = _warnBrush;
            SvcCuaStatus.Text = "capture only";
            SvcCuaDetail.Text = "computer control off";
        }
        else
        {
            SvcCuaDot.Fill = _mutedBrush;
            SvcCuaStatus.Text = "off";
            SvcCuaDetail.Text = "desktop capture + control off";
        }

        if (snap.UnrealConnected == true)
        {
            SvcUeDot.Fill = _okBrush;
            SvcUeStatus.Text = "connected";
        }
        else if (snap.UnrealEnabled == true)
        {
            SvcUeDot.Fill = _warnBrush;
            SvcUeStatus.Text = "enabled, not connected";
        }
        else
        {
            SvcUeDot.Fill = snap.Reachable ? _mutedBrush : _badBrush;
            SvcUeStatus.Text = "off / unknown";
        }

        SvcUeDetail.Text = snap.UnrealTarget ?? "avatar bridge";

        SvcComfyDot.Fill = comfyUp ? _okBrush : _mutedBrush;
        SvcComfyStatus.Text = comfyUp ? "up" : "down";
        SvcComfyDetail.Text = comfyUp
            ? "http://127.0.0.1:8188"
            : ":8188 not answering";
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
        if (_servicesBusy) return;
        if (sender is not Button { Tag: string tag }) return;

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
                case "hermes-start":
                case "hermes-stop":
                case "hermes-restart":
                    result = new LocalStackActionResult(
                        false,
                        "Hermes retired (BED-185) — not started. Open Chrome via desktop_open_app.");
                    break;
                case "ollama-start":
                    result = await _stack.StartOllamaAsync().ConfigureAwait(true);
                    break;
                case "gui-restart":
                    result = await _stack.RestartChatDesktopAsync().ConfigureAwait(true);
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

    private async void ToolsAccessToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (_toolsAccessHydrating) return;
        if (ToolsAllowDesktopCaptureCheck is null
            || ToolsAllowBrowserCaptureCheck is null
            || ToolsAllowComputerControlCheck is null
            || ToolsSoftCursorRestoreCheck is null
            || ToolsAllowMt4ReadCheck is null
            || ToolsAllowMt4TradeCheck is null)
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
            allowMt4Trade: ToolsAllowMt4TradeCheck.IsChecked == true).ConfigureAwait(true);

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
            || ToolsAllowMt4TradeCheck is null)
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
            || SystemInferenceBox is null || SystemHermesBox is null
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
            SystemHermesBox.Text = "unreachable";
            SystemMemoryPathBox.Text = snap.Detail ?? "unreachable";
            SystemMemoryOpenBox.Text = "unreachable";
            SystemSoulLoopBox.Text = "unreachable";
            SystemInferenceBox.Foreground = _badBrush;
            SystemHermesBox.Foreground = _badBrush;
            SystemMemoryOpenBox.Foreground = _badBrush;
            return;
        }

        SystemInferenceBox.Text = snap.InferenceEnabled ? "enabled (Ollama)" : "disabled";
        SystemInferenceBox.Foreground = snap.InferenceEnabled ? _okBrush : _warnBrush;

        SystemHermesBox.Text = "retired (BED-185)";
        SystemHermesBox.Foreground = _mutedBrush;

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
            if (state is WsConnectionState.Unavailable or WsConnectionState.Disconnected)
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
        var want = ReadPayloadString(frame, "want");
        var category = ReadPayloadString(frame, "category");
        var label = ReadPayloadString(frame, "emotionLabel");
        var valence = ReadPayloadDouble(frame, "valence");
        var arousal = ReadPayloadDouble(frame, "arousal");
        var parsed = ParseWantWire(want, category);

        _lastWantCategory = parsed.Category;
        _lastWantUtc = DateTimeOffset.UtcNow;
        _lastActivityPhrase = HumanizeActivity(parsed.Category, parsed.Phrase);

        if (ActivityText is not null)
            ActivityText.Text = _lastActivityPhrase;

        if (!string.IsNullOrWhiteSpace(label))
            ApplyMoodToHud(label.Trim(), valence ?? _lastValence, arousal ?? _lastArousal);
        else if (valence is not null || arousal is not null)
            ApplyMoodToHud(MoodLabelText?.Text ?? "—", valence ?? _lastValence, arousal ?? _lastArousal);

        UpdateEngagementState();
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
                ActivityText.Text = sleepCat ? HumanizeActivity(cat, _lastActivityPhrase ?? "") : "Resting";
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
                ActivityText.Text = "Waiting";
        }
        else
        {
            state = "Idle";
            brush = Res("MutedBrush");
        }

        PresenceStateText.Text = state;
        PresenceStateText.Foreground = brush;
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
