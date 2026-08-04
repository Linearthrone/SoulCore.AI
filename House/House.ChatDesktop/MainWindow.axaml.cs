using System.Collections.ObjectModel;
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
using House.ChatDesktop.Models;
using SoulCore.Protocol;
using House.ChatDesktop.Services;

namespace House.ChatDesktop;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ChatMessage> _messages = new();
    private readonly SoulCoreHealthClient _health = new();
    private readonly SoulCoreWsClient _ws = new();
    private readonly SoulCoreToolsSettingsClient _toolsSettings = new();
    private readonly SoulCoreDesktopViewClient _desktopView = new();
    private readonly LocalStackControl _stack = new();
    private readonly ChatHistoryStore _chatHistory = new();
    private readonly SoulCoreVoiceClient _voice = new();
    private readonly PushToTalkRecorder _ptt = new();
    private bool _pttBusy;
    private bool _toolsAccessHydrating;
    private bool _servicesBusy;
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _desktopViewTimer;
    private bool _desktopViewBusy;
    private int _desktopImageWidth;
    private int _desktopImageHeight;
    private int? _desktopCursorX;
    private int? _desktopCursorY;
    private string? _lastDesktopImageHash;
    private Window? _desktopPopOut;
    private Image? _desktopPopOutImage;
    private Canvas? _desktopPopOutCursorLayer;
    private Ellipse? _desktopPopOutCursor;
    private readonly IBrush _okBrush;
    private readonly IBrush _warnBrush;
    private readonly IBrush _badBrush;
    private readonly IBrush _mutedBrush;
    private LocalUiSettings _uiSettings = LocalUiSettings.Load();
    private readonly NotificationService _notifications;
    private SoulCoreHealthSnapshot _lastHealth = new();
    private bool? _lastAllowComputerControl;
    private bool? _lastAllowDesktopCapture;
    private bool _presenceFromWs;
    private ChatMessage? _streamingAssistant;
    private double _lastValence;
    private double _lastArousal;
    private double _lastDominance = 0.5;
    private double _lastFocus = 0.5;

    public MainWindow()
    {
        InitializeComponent();
        TranscriptList.ItemsSource = _messages;
        EndpointText.Text = ConnectionDefaults.DisplayEndpoint;
        DisplayNameBox.Text = _uiSettings.DisplayName;

        _okBrush = Res("OkBrush");
        _warnBrush = Res("WarnBrush");
        _badBrush = Res("BadBrush");
        _mutedBrush = Res("MutedBrush");

        _notifications = new NotificationService(_uiSettings.Notifications);
        _notifications.ReloadPlayer();

        _ws.StateChanged += OnWsStateChanged;
        _ws.FrameReceived += OnFrameReceived;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _pollTimer.Tick += async (_, _) => await ProbeHealthAsync();

        _desktopViewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _desktopViewTimer.Tick += async (_, _) => await RefreshDesktopViewAsync();

        Opened += async (_, _) =>
        {
            WirePushToTalk();
            await RefreshVoiceStatusAsync();
        };

        Opened += async (_, _) =>
        {
            LoadChatHistory();
            await _ws.ConnectAsync();
            await ProbeHealthAsync();
            _pollTimer.Start();
            _desktopViewTimer.Start();
            await RefreshDesktopViewAsync();
        };

        Closed += async (_, _) =>
        {
            _pollTimer.Stop();
            _desktopViewTimer.Stop();
            _desktopPopOut?.Close();
            SaveDisplayNameFromEditor();
            await _ws.DisposeAsync();
            _notifications.Dispose();
            _health.Dispose();
            _toolsSettings.Dispose();
            _desktopView.Dispose();
            _stack.Dispose();
            _chatHistory.Dispose();
        };
    }

    private void LoadChatHistory()
    {
        try
        {
            var prior = _chatHistory.LoadRecent();
            _messages.Clear();
            foreach (var m in prior)
                _messages.Add(m);
            if (_messages.Count > 0)
                ScrollTranscriptToEnd();
        }
        catch (Exception ex)
        {
            AppendSystem($"Chat history load failed: {ex.Message}", persist: false);
        }
    }

    private static IBrush Res(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var v) && v is IBrush b
            ? b
            : Brushes.Gray;

    private void Nav_Changed(object? sender, RoutedEventArgs e)
    {
        if (PresenceView is null || SettingsView is null || NavPresence is null) return;

        var presence = NavPresence.IsChecked == true;
        PresenceView.IsVisible = presence;
        SettingsView.IsVisible = !presence;
        Title = presence ? "House Victoria — Presence" : "House Victoria — Settings";

        if (!presence)
        {
            // Populate System tab from the last health probe (no new network call).
            ApplySystemStatus(_lastHealth);
            SeedNotificationControls();
            _ = RefreshToolsAccessAsync();
        }
        else
        {
            _ = RefreshDesktopViewAsync();
        }
    }

    private void OpenPresenceFromIdentity_Click(object? sender, RoutedEventArgs e)
    {
        NavPresence.IsChecked = true;
    }

    private void IdentitySave_Click(object? sender, RoutedEventArgs e) => SaveDisplayNameFromEditor();

    private void DisplayNameBox_LostFocus(object? sender, RoutedEventArgs e) => SaveDisplayNameFromEditor();

    private void SaveDisplayNameFromEditor()
    {
        if (DisplayNameBox is null) return;
        var name = (DisplayNameBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = "Victoria";
            DisplayNameBox.Text = name;
        }

        if (string.Equals(_uiSettings.DisplayName, name, StringComparison.Ordinal))
        {
            return;
        }

        _uiSettings.DisplayName = name;
        _uiSettings.Save();
        AppendSystem($"Identity display name saved locally: {name}");
    }

    private async void SoulLoopTick_Click(object? sender, RoutedEventArgs e)
    {
        await ForceSoulLoopTickAsync(restartWs: false).ConfigureAwait(true);
    }

    private async void SoulLoopRestart_Click(object? sender, RoutedEventArgs e)
    {
        await ForceSoulLoopTickAsync(restartWs: true).ConfigureAwait(true);
    }

    private async Task ForceSoulLoopTickAsync(bool restartWs)
    {
        if (_lastHealth.SoulLoopEnabled == false)
        {
            if (WantStatusText is not null)
            {
                WantStatusText.Text = "SoulLoop is off on Host — enable SoulLoop:Enabled and recycle Host.";
                WantStatusText.Foreground = _badBrush;
            }
            if (SoulLoopHintText is not null)
                SoulLoopHintText.Text = "Focus refresh skipped (kill switch).";
            return;
        }

        if (restartWs)
        {
            if (SoulLoopHintText is not null)
                SoulLoopHintText.Text = "Reconnecting Presence WS…";
            await _ws.ConnectAsync().ConfigureAwait(true);
        }

        if (_ws.State != WsConnectionState.Connected)
        {
            if (SoulLoopHintText is not null)
                SoulLoopHintText.Text = $"Focus refresh skipped — WS not connected ({_ws.LastError})";
            return;
        }

        if (WantStatusText is not null)
        {
            WantStatusText.Text = "Refreshing focus…";
            WantStatusText.Foreground = Res("MutedBrush");
        }
        if (SoulLoopHintText is not null)
            SoulLoopHintText.Text = "Waiting for Host SoulLoop tick…";

        var ok = await _ws.SendLoopTickAsync().ConfigureAwait(true);
        if (!ok && SoulLoopHintText is not null)
            SoulLoopHintText.Text = $"Focus refresh failed — {_ws.LastError}";
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        await _ws.ConnectAsync();
        await ProbeHealthAsync();
    }

    private async void Send_Click(object? sender, RoutedEventArgs e) => await SendCurrentAsync();

    private void CorrectEmotion_Click(object? sender, RoutedEventArgs e)
    {
        if (EmotionCorrectPanel.IsVisible)
        {
            EmotionCorrectPanel.IsVisible = false;
            return;
        }

        SeedCorrectionEditorsFromLastSnapshot();
        EmotionCorrectPanel.IsVisible = true;
        CorrectNoteBox.Focus();
    }

    private void CorrectEmotionCancel_Click(object? sender, RoutedEventArgs e)
    {
        EmotionCorrectPanel.IsVisible = false;
    }

    private async void CorrectEmotionSave_Click(object? sender, RoutedEventArgs e)
    {
        var valence = CorrectValenceSlider.Value;
        var arousal = CorrectArousalSlider.Value;
        var dominance = CorrectDominanceSlider.Value;
        var focus = CorrectFocusSlider.Value;
        var note = CorrectNoteBox.Text;

        var sent = await _ws.SendEmotionCorrectAsync(valence, arousal, dominance, focus, note);
        if (!sent)
        {
            AppendSystem(
                $"emotion.correct not sent — {_ws.LastError}");
            ScrollTranscriptToEnd();
            return;
        }

        // Optimistic strip update until Host echoes emotion.snapshot.
        _lastValence = valence;
        _lastArousal = arousal;
        _lastDominance = dominance;
        _lastFocus = focus;
        ValenceText.Text = valence.ToString("0.0");
        ArousalText.Text = arousal.ToString("0.0");
        if (string.IsNullOrWhiteSpace(EmotionLabelText.Text) || EmotionLabelText.Text == "—")
        {
            EmotionLabelText.Text = "corrected";
        }

        var notePreview = string.IsNullOrWhiteSpace(note)
            ? "(no note)"
            : $"\"{note.Trim()}\"";
        AppendSystem(
            $"emotion.correct sent · v={valence:0.00} a={arousal:0.00} d={dominance:0.00} f={focus:0.00} · {notePreview}");
        EmotionCorrectPanel.IsVisible = false;
        ScrollTranscriptToEnd();
    }

    private void SeedCorrectionEditorsFromLastSnapshot()
    {
        // Slider value labels are bound to the sliders in XAML; only seed the values.
        CorrectValenceSlider.Value = _lastValence;
        CorrectArousalSlider.Value = _lastArousal;
        CorrectDominanceSlider.Value = _lastDominance;
        CorrectFocusSlider.Value = _lastFocus;
        if (string.IsNullOrWhiteSpace(CorrectNoteBox.Text))
        {
            CorrectNoteBox.Text = "that wasn’t how I felt";
        }
    }

    private async void ChatInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            await SendCurrentAsync();
        }
    }

    private async Task SendCurrentAsync()
    {
        var text = (ChatInput.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text)) return;

        ChatInput.Text = string.Empty;
        _streamingAssistant = null;
        var userMsg = new ChatMessage { Role = "user", Text = text };
        _messages.Add(userMsg);
        PersistMessage(userMsg);
        ScrollTranscriptToEnd();

        var sent = await _ws.SendChatAsync(text);
        if (!sent)
        {
            var err = new ChatMessage
            {
                Role = "system",
                Text =
                    "Host WS unavailable — message not sent. " +
                    $"Start SoulCore.Host on {ConnectionDefaults.WsUri}, then Refresh. " +
                    $"Detail: {_ws.LastError}"
            };
            _messages.Add(err);
            PersistMessage(err);
            ScrollTranscriptToEnd();
        }
    }

    private async Task ProbeHealthAsync()
    {
        var snap = await _health.ProbeAsync();
        _lastHealth = snap;

        // Prefer presence.status from WS when connected; otherwise HTTP /health.
        if (!_presenceFromWs || _ws.State != WsConnectionState.Connected)
        {
            AliveText.Text = snap.Alive ? "yes" : "no";
            AliveText.Foreground = snap.Alive ? _okBrush : _badBrush;

            WarmText.Text = snap.Warm ? "yes" : "no";
            WarmText.Foreground = snap.Warm ? _okBrush : (snap.Alive ? _warnBrush : _badBrush);
        }

        MemoryOpenBox.Text = snap.Reachable ? (snap.MemoryOpen ? "true" : "false") : "unreachable";
        if (!snap.Reachable)
        {
            MemoryPathBox.Text = snap.Detail ?? "unreachable";
            MemoryOpenBox.Foreground = _badBrush;
        }
        else
        {
            MemoryPathBox.Text = string.IsNullOrWhiteSpace(snap.MemoryPath)
                ? "(missing memory.path)"
                : snap.MemoryPath!;
            MemoryOpenBox.Foreground = snap.MemoryOpen ? _okBrush : _warnBrush;
        }

        ApplyUnrealStatus(snap);
        ApplyServiceIndicators(snap);
        await RefreshServicesPanelAsync(snap).ConfigureAwait(true);

        // Auto-reconnect attempt when Host comes back while we were unavailable.
        if (snap.Alive
            && _ws.State is WsConnectionState.Unavailable or WsConnectionState.Disconnected)
        {
            await _ws.ConnectAsync();
        }

        UpdateConnectionChrome(snap);
    }

    /// <summary>
    /// Emotion-row indicator lights: CUA / HST / OLL / HER / UE.
    /// Green = ready, amber = partial, red = down, muted = off/unknown.
    /// </summary>
    private void ApplyServiceIndicators(SoulCoreHealthSnapshot? health = null, ToolsAccessSnapshot? tools = null)
    {
        if (IndCuaDot is null || IndHstDot is null || IndOllDot is null || IndHerDot is null || IndUeDot is null)
            return;

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

        var backend = (snap.DesktopBackend ?? tools?.DesktopBackend ?? "cua").Trim();
        var isCuaBackend = backend.Equals("cua", StringComparison.OrdinalIgnoreCase)
            || backend.Equals("auto", StringComparison.OrdinalIgnoreCase);
        var driverOk = snap.CuaDriverAvailable == true
            || tools?.CuaDriverAvailable == true
            || !isCuaBackend;

        // CUA — computer-use write path / driver
        if (!snap.Reachable)
        {
            IndCuaDot.Fill = _badBrush;
            ToolTip.SetTip(IndCuaDot.Parent as Control ?? IndCuaDot, "CUA — Host unreachable");
        }
        else if (isCuaBackend && snap.CuaDriverAvailable == false)
        {
            IndCuaDot.Fill = _badBrush;
            ToolTip.SetTip(IndCuaDot.Parent as Control ?? IndCuaDot,
                "CUA — cua-driver not found (install or set SOULCORE_CUA_DRIVER)");
        }
        else if (_lastAllowComputerControl == true && driverOk)
        {
            IndCuaDot.Fill = _okBrush;
            ToolTip.SetTip(IndCuaDot.Parent as Control ?? IndCuaDot,
                $"CUA — computer-use ON ({backend})");
        }
        else if (_lastAllowDesktopCapture == true)
        {
            IndCuaDot.Fill = _warnBrush;
            ToolTip.SetTip(IndCuaDot.Parent as Control ?? IndCuaDot,
                "CUA — capture only (AllowComputerControl off)");
        }
        else
        {
            IndCuaDot.Fill = _mutedBrush;
            ToolTip.SetTip(IndCuaDot.Parent as Control ?? IndCuaDot, "CUA — computer-use off");
        }

        // HST — Host
        IndHstDot.Fill = snap.Alive ? _okBrush : _badBrush;
        ToolTip.SetTip(IndHstDot.Parent as Control ?? IndHstDot,
            snap.Alive ? "HST — Host OK" : "HST — Host down");

        // OLL — Ollama / inference
        if (!snap.Reachable)
            IndOllDot.Fill = _badBrush;
        else
            IndOllDot.Fill = snap.InferenceEnabled ? _okBrush : _mutedBrush;
        ToolTip.SetTip(IndOllDot.Parent as Control ?? IndOllDot,
            snap.InferenceEnabled ? "OLL — inference enabled" : "OLL — inference off");

        // HER — gateway up preferred; Host Hermes.Enabled is secondary
        if (snap.HermesGatewayUp == true)
        {
            IndHerDot.Fill = _okBrush;
            var clientNote = snap.HermesEnabled ? "Host client on" : "Host Hermes.Enabled=false (gateway still up)";
            ToolTip.SetTip(IndHerDot.Parent as Control ?? IndHerDot, $"HER — gateway :8642 up · {clientNote}");
        }
        else if (!snap.Reachable)
        {
            IndHerDot.Fill = _badBrush;
            ToolTip.SetTip(IndHerDot.Parent as Control ?? IndHerDot, "HER — Host unreachable (gateway unknown)");
        }
        else if (snap.HermesEnabled)
        {
            IndHerDot.Fill = _warnBrush;
            ToolTip.SetTip(IndHerDot.Parent as Control ?? IndHerDot,
                "HER — Host client enabled but gateway :8642 down");
        }
        else
        {
            IndHerDot.Fill = _mutedBrush;
            ToolTip.SetTip(IndHerDot.Parent as Control ?? IndHerDot, "HER — gateway down / Hermes off");
        }

        // UE — Unreal bridge
        if (!snap.Reachable)
            IndUeDot.Fill = _badBrush;
        else if (snap.UnrealConnected == true)
            IndUeDot.Fill = _okBrush;
        else if (snap.UnrealEnabled == true)
            IndUeDot.Fill = _warnBrush;
        else
            IndUeDot.Fill = _mutedBrush;
        ToolTip.SetTip(IndUeDot.Parent as Control ?? IndUeDot,
            snap.UnrealConnected == true
                ? "UE — Unreal connected"
                : snap.UnrealEnabled == true
                    ? "UE — Unreal enabled, not connected"
                    : "UE — Unreal off");
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
            ? (snap.InferenceEnabled ? "tags OK · Host inference on" : "tags OK · Host inference off")
            : "unreachable :11434 — Start runs ollama serve";

        SvcHermesDot.Fill = hermesUp
            ? _okBrush
            : (snap.HermesEnabled ? _warnBrush : _mutedBrush);
        SvcHermesStatus.Text = hermesUp ? "gateway up" : "gateway down";
        SvcHermesDetail.Text = hermesUp
            ? (snap.HermesEnabled
                ? "http://127.0.0.1:8642 · Host Hermes client enabled"
                : "http://127.0.0.1:8642 · Host Hermes.Enabled=false (OK for MCP later)")
            : "start via start-hermes.ps1 (soft-fail in ALLSTART)";

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
            SvcCuaDetail.Text = "cua-driver.exe not found";
        }
        else if (_lastAllowComputerControl == true && driverOk)
        {
            SvcCuaDot.Fill = _okBrush;
            SvcCuaStatus.Text = "control on";
            SvcCuaDetail.Text = string.IsNullOrWhiteSpace(snap.CuaDriverPath)
                ? $"backend={backend}"
                : $"backend={backend} · {snap.CuaDriverPath}";
        }
        else if (_lastAllowDesktopCapture == true)
        {
            SvcCuaDot.Fill = _warnBrush;
            SvcCuaStatus.Text = "capture only";
            SvcCuaDetail.Text = "AllowComputerControl off — Enable to turn green";
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
        SvcUeDetail.Text = snap.UnrealTarget ?? "avatar bridge (status only)";

        SvcComfyDot.Fill = comfyUp ? _okBrush : _mutedBrush;
        SvcComfyStatus.Text = comfyUp ? "up" : "down";
        SvcComfyDetail.Text = comfyUp
            ? "http://127.0.0.1:8188"
            : ":8188 not answering (Companion media generate needs ComfyUI)";
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
        if (_servicesBusy)
            return;
        if (sender is not Button { Tag: string tag })
            return;

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
                    result = await _stack.StartHermesAsync().ConfigureAwait(true);
                    break;
                case "hermes-stop":
                    result = await _stack.StopHermesAsync().ConfigureAwait(true);
                    break;
                case "hermes-restart":
                    result = await _stack.RestartHermesAsync().ConfigureAwait(true);
                    break;
                case "ollama-start":
                    result = await _stack.StartOllamaAsync().ConfigureAwait(true);
                    break;
                case "gui-restart":
                    result = await _stack.RestartChatDesktopAsync().ConfigureAwait(true);
                    break;
                case "cua-enable":
                {
                    var snap = await _toolsSettings.PatchAsync(allowComputerControl: true).ConfigureAwait(true);
                    ApplyToolsAccess(snap, saved: true);
                    ApplyServiceIndicators(_lastHealth, snap);
                    result = snap.Reachable && snap.AllowComputerControl
                        ? LocalStackActionResult.Succeed("AllowComputerControl=true")
                        : LocalStackActionResult.Fail(snap.Detail ?? "patch failed");
                    break;
                }
                case "cua-disable":
                {
                    var snap = await _toolsSettings.PatchAsync(allowComputerControl: false).ConfigureAwait(true);
                    ApplyToolsAccess(snap, saved: true);
                    ApplyServiceIndicators(_lastHealth, snap);
                    result = snap.Reachable && !snap.AllowComputerControl
                        ? LocalStackActionResult.Succeed("AllowComputerControl=false")
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

    private void ApplyUnrealStatus(SoulCoreHealthSnapshot snap)
    {
        if (UnrealEnabledBox is null || UnrealTargetBox is null || UnrealConnectedBox is null)
        {
            return;
        }

        if (!snap.Reachable)
        {
            UnrealEnabledBox.Text = "unreachable";
            UnrealTargetBox.Text = snap.Detail ?? "—";
            UnrealConnectedBox.Text = "unreachable";
            UnrealEnabledBox.Foreground = _badBrush;
            UnrealConnectedBox.Foreground = _badBrush;
            return;
        }

        UnrealEnabledBox.Text = snap.UnrealEnabled is null
            ? "(missing)"
            : (snap.UnrealEnabled.Value ? "true" : "false");
        UnrealTargetBox.Text = string.IsNullOrWhiteSpace(snap.UnrealTarget)
            ? "(missing)"
            : snap.UnrealTarget!;
        UnrealConnectedBox.Text = snap.UnrealConnected is null
            ? "(missing)"
            : (snap.UnrealConnected.Value ? "true" : "false");

        UnrealEnabledBox.Foreground = snap.UnrealEnabled == true ? _okBrush : _warnBrush;
        UnrealConnectedBox.Foreground = snap.UnrealConnected == true ? _okBrush : _warnBrush;
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

    private async void RefreshVoice_Click(object? sender, RoutedEventArgs e) =>
        await RefreshVoiceStatusAsync();

    private async Task RefreshVoiceStatusAsync()
    {
        if (VoiceStatusBox is null) return;
        var h = await _voice.GetHealthAsync();
        var stt = h.Stt?.Ok == true ? "STT ok" : "STT down";
        var tts = h.Tts?.Ok == true ? "TTS ok" : "TTS down";
        VoiceStatusBox.Text = h.Enabled
            ? $"Enabled · {stt} · {tts} · speakers={(h.PlayOnHostSpeakers ? "on" : "off")} · unreal={(h.PlayInUnreal ? "on" : "off")}"
            : "Voice disabled in Host config";
        VoiceStatusBox.Foreground = h.Enabled && h.Stt?.Ok == true && h.Tts?.Ok == true ? _okBrush : _warnBrush;
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
                if (PttHintText is not null) PttHintText.Text = "Too short — hold longer and try again.";
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

    private void ViewCharterAnchors_Click(object? sender, RoutedEventArgs e)
    {
        if (CharterAnchorsPanel is null || CharterAnchorsText is null) return;
        CharterAnchorsPanel.IsVisible = !CharterAnchorsPanel.IsVisible;
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
        if (NotifSoundPathBox is null) return;

        var provider = StorageProvider;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pick a notification sound (.wav)",
            FileTypeFilter = new[]
            {
                new FilePickerFileType("WAV audio") { Patterns = new[] { "*.wav" } }
            },
            AllowMultiple = false
        });

        if (files is null || files.Count == 0) return;

        var path = files[0].Path.LocalPath;
        _uiSettings.Notifications.SoundPath = path;
        _uiSettings.Notifications.UseSystemBeep = false;
        _uiSettings.Save();

        NotifSoundPathBox.Text = path;
        _notifications.ReloadPlayer();
        UpdateNotifStatusText();
    }

    private void NotifClear_Click(object? sender, RoutedEventArgs e)
    {
        if (NotifSoundPathBox is null) return;

        _uiSettings.Notifications.SoundPath = null;
        _uiSettings.Notifications.UseSystemBeep = true;
        _uiSettings.Save();

        NotifSoundPathBox.Text = "(system beep)";
        _notifications.ReloadPlayer();
        UpdateNotifStatusText();
    }

    private void NotifTest_Click(object? sender, RoutedEventArgs e)
    {
        var ok = _notifications.Play();
        UpdateNotifStatusText(ok ? "played" : "muted or failed");
    }

    private void UpdateNotifStatusText(string? message = null)
    {
        if (NotifStatusText is null) return;
        if (message is not null)
        {
            NotifStatusText.Text = message;
            return;
        }

        NotifStatusText.Text = _uiSettings.Notifications.Enabled
            ? "Enabled"
            : "Disabled";
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

    private async void ToolsAccessRefresh_Click(object? sender, RoutedEventArgs e)
    {
        await RefreshToolsAccessAsync().ConfigureAwait(true);
    }

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
            || ToolsAllowMt4TradeCheck is null
            || ToolsDesktopBackendBox is null
            || ToolsBrowserBackendBox is null
            || ToolsMt4BackendBox is null)
        {
            return;
        }

        _toolsAccessHydrating = true;
        try
        {
            ToolsAllowDesktopCaptureCheck.IsChecked = snap.AllowDesktopCapture;
            ToolsAllowBrowserCaptureCheck.IsChecked = snap.AllowBrowserCapture;
            ToolsAllowComputerControlCheck.IsChecked = snap.AllowComputerControl;
            ToolsSoftCursorRestoreCheck.IsChecked = snap.SoftCursorRestore;
            ToolsAllowMt4ReadCheck.IsChecked = snap.AllowMt4Read;
            ToolsAllowMt4TradeCheck.IsChecked = snap.AllowMt4Trade;
            ToolsDesktopBackendBox.Text = snap.DesktopBackend ?? "—";
            ToolsBrowserBackendBox.Text = snap.BrowserBackend ?? "—";
            ToolsMt4BackendBox.Text = snap.Mt4Backend ?? "—";
        }
        finally
        {
            _toolsAccessHydrating = false;
        }

        if (ToolsAccessStatusText is null) return;

        if (!snap.Reachable)
        {
            ToolsAccessStatusText.Text = snap.Detail ?? "Host unreachable";
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

    private async Task RefreshDesktopViewAsync()
    {
        if (_desktopViewBusy) return;
        if (PresenceView is { IsVisible: false } && _desktopPopOut is null) return;

        _desktopViewBusy = true;
        try
        {
            var snap = await _desktopView.GetAsync(includeImage: true).ConfigureAwait(true);
            ApplyDesktopView(snap);
        }
        finally
        {
            _desktopViewBusy = false;
        }
    }

    private void ApplyDesktopView(DesktopViewSnapshot snap)
    {
        if (DesktopViewActionText is null || DesktopViewMetaText is null) return;

        if (!snap.Reachable)
        {
            DesktopViewActionText.Text = snap.Detail ?? "Host unreachable";
            DesktopViewMetaText.Text = "Start SoulCore.Host to see Victoria's screen.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(snap.Detail) && snap.Detail.StartsWith("HTTP", StringComparison.Ordinal))
        {
            DesktopViewActionText.Text = snap.Detail;
            return;
        }

        _desktopImageWidth = snap.Width;
        _desktopImageHeight = snap.Height;
        _desktopCursorX = snap.CursorX;
        _desktopCursorY = snap.CursorY;

        DesktopViewActionText.Text = string.IsNullOrWhiteSpace(snap.LastAction)
            ? "Waiting for desktop activity…"
            : snap.LastAction;

        var when = snap.UpdatedAt?.ToLocalTime().ToString("h:mm:ss tt") ?? "—";
        var soft = snap.SoftCursorRestore ? "agent/background" : "foreground ok";
        DesktopViewMetaText.Text = snap.HasImage
            ? $"{snap.Width}×{snap.Height} · {soft} · updated {when}"
            : $"No capture yet · {soft} · cua overlay on real desktop when she works";

        if (snap.ImageBytes is { Length: > 0 })
        {
            var hash = $"{snap.ImageBytes.Length}:{snap.UpdatedAt:O}:{snap.Width}x{snap.Height}";
            if (!string.Equals(hash, _lastDesktopImageHash, StringComparison.Ordinal))
            {
                _lastDesktopImageHash = hash;
                try
                {
                    using var ms = new MemoryStream(snap.ImageBytes);
                    var bmp = new Bitmap(ms);
                    if (DesktopViewImage is not null)
                    {
                        DesktopViewImage.Source = bmp;
                        DesktopViewImage.IsVisible = true;
                    }

                    if (DesktopViewEmptyText is not null)
                        DesktopViewEmptyText.IsVisible = false;

                    if (_desktopPopOutImage is not null)
                        _desktopPopOutImage.Source = bmp;
                }
                catch (Exception ex)
                {
                    DesktopViewActionText.Text = $"Image decode failed: {ex.Message}";
                }
            }
        }
        else if (DesktopViewEmptyText is not null && DesktopViewImage is not null)
        {
            DesktopViewEmptyText.IsVisible = true;
            DesktopViewImage.IsVisible = false;
        }

        PositionDesktopCursor(DesktopViewSurface, DesktopViewCursorLayer, DesktopViewCursor);
        PositionDesktopCursor(_desktopPopOutImage?.Parent as Control, _desktopPopOutCursorLayer, _desktopPopOutCursor);
    }

    private void DesktopViewSurface_SizeChanged(object? sender, SizeChangedEventArgs e) =>
        PositionDesktopCursor(DesktopViewSurface, DesktopViewCursorLayer, DesktopViewCursor);

    private void PositionDesktopCursor(Control? surface, Canvas? layer, Ellipse? cursor)
    {
        if (surface is null || layer is null || cursor is null) return;

        if (_desktopCursorX is not int cx || _desktopCursorY is not int cy
            || _desktopImageWidth <= 0 || _desktopImageHeight <= 0)
        {
            layer.IsVisible = false;
            return;
        }

        var bounds = surface.Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            layer.IsVisible = false;
            return;
        }

        var scale = Math.Min(bounds.Width / _desktopImageWidth, bounds.Height / _desktopImageHeight);
        var drawW = _desktopImageWidth * scale;
        var drawH = _desktopImageHeight * scale;
        var offsetX = (bounds.Width - drawW) / 2;
        var offsetY = (bounds.Height - drawH) / 2;

        var left = offsetX + (cx * scale) - (cursor.Width / 2);
        var top = offsetY + (cy * scale) - (cursor.Height / 2);
        Canvas.SetLeft(cursor, left);
        Canvas.SetTop(cursor, top);
        layer.IsVisible = true;
        layer.Width = bounds.Width;
        layer.Height = bounds.Height;
    }

    private void DesktopViewPopOut_Click(object? sender, RoutedEventArgs e)
    {
        if (_desktopPopOut is not null)
        {
            _desktopPopOut.Activate();
            return;
        }

        var cursor = new Ellipse
        {
            Width = 22,
            Height = 22,
            Stroke = new SolidColorBrush(Color.Parse("#E8C547")),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.Parse("#66E8C547"))
        };
        var layer = new Canvas { IsHitTestVisible = false };
        layer.Children.Add(cursor);
        var image = new Image { Stretch = Stretch.Uniform };
        if (DesktopViewImage?.Source is { } src)
            image.Source = src;

        var surface = new Grid { Background = new SolidColorBrush(Color.Parse("#1a1a1a")) };
        surface.Children.Add(image);
        surface.Children.Add(layer);
        surface.SizeChanged += (_, _) => PositionDesktopCursor(surface, layer, cursor);

        _desktopPopOutImage = image;
        _desktopPopOutCursorLayer = layer;
        _desktopPopOutCursor = cursor;

        var hint = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            FontSize = 11,
            Foreground = Res("MutedBrush"),
            Text = "Her blue agent cursor is drawn by cua-driver on the real desktop (same as LLMOD). Your OS mouse never moves.",
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(hint, 1);

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        root.Children.Add(surface);
        root.Children.Add(hint);

        var win = new Window
        {
            Title = "Victoria's screen",
            Width = 960,
            Height = 640,
            Content = new Border
            {
                Padding = new Thickness(12),
                Child = root
            }
        };
        win.Closed += (_, _) =>
        {
            _desktopPopOut = null;
            _desktopPopOutImage = null;
            _desktopPopOutCursorLayer = null;
            _desktopPopOutCursor = null;
        };
        _desktopPopOut = win;
        win.Show(this);
        PositionDesktopCursor(surface, layer, cursor);
    }

    private void ApplySystemStatus(SoulCoreHealthSnapshot snap)
    {
        // Null-guard all System tab controls — during InitializeComponent some
        // are still null when early health probes fire (ISSUE-002 lesson).
        if (SystemEndpointBox is null || SystemBindBox is null || SystemPortBox is null
            || SystemInferenceBox is null || SystemHermesBox is null
            || SystemMemoryPathBox is null || SystemMemoryOpenBox is null
            || SystemSoulLoopBox is null || SystemUnrealTargetBox is null
            || SystemUnrealConnectedBox is null
            || CharterLockBox is null || CharterDriftBox is null || CharterSpendBox is null)
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
            SystemUnrealTargetBox.Text = snap.Detail ?? "—";
            SystemUnrealConnectedBox.Text = "unreachable";
            SystemInferenceBox.Foreground = _badBrush;
            SystemHermesBox.Foreground = _badBrush;
            SystemMemoryOpenBox.Foreground = _badBrush;
            SystemUnrealConnectedBox.Foreground = _badBrush;
            CharterLockBox.Text = "unreachable";
            CharterLockBox.Foreground = _badBrush;
            CharterDriftBox.Text = "unreachable";
            CharterDriftBox.Foreground = _badBrush;
            CharterSpendBox.Text = "unreachable";
            CharterSpendBox.Foreground = _badBrush;
            return;
        }

        SystemInferenceBox.Text = snap.InferenceEnabled ? "enabled (Ollama)" : "disabled";
        SystemInferenceBox.Foreground = snap.InferenceEnabled ? _okBrush : _warnBrush;

        SystemHermesBox.Text = snap.HermesEnabled ? "enabled" : "disabled";
        SystemHermesBox.Foreground = snap.HermesEnabled ? _okBrush : _warnBrush;

        SystemMemoryPathBox.Text = string.IsNullOrWhiteSpace(snap.MemoryPath)
            ? "(missing memory.path)"
            : snap.MemoryPath!;
        SystemMemoryOpenBox.Text = snap.MemoryOpen ? "true" : "false";
        SystemMemoryOpenBox.Foreground = snap.MemoryOpen ? _okBrush : _warnBrush;

        // SoulLoop status from /health soulLoop.enabled (no longer a phase proxy).
        if (snap.SoulLoopEnabled is null)
        {
            SystemSoulLoopBox.Text = "(not reported)";
            SystemSoulLoopBox.Foreground = Res("MutedBrush");
            if (SoulLoopStateText is not null)
            {
                SoulLoopStateText.Text = "SoulLoop —";
                SoulLoopStateText.Foreground = Res("MutedBrush");
            }
        }
        else
        {
            SystemSoulLoopBox.Text = snap.SoulLoopEnabled.Value ? "enabled" : "disabled";
            SystemSoulLoopBox.Foreground = snap.SoulLoopEnabled.Value ? _okBrush : _badBrush;
            if (SoulLoopStateText is not null)
            {
                SoulLoopStateText.Text = snap.SoulLoopEnabled.Value ? "SoulLoop on" : "SoulLoop off";
                SoulLoopStateText.Foreground = snap.SoulLoopEnabled.Value ? _okBrush : _badBrush;
            }
            if (SoulLoopHintText is not null && snap.SoulLoopEnabled == false)
            {
                SoulLoopHintText.Text =
                    "SoulLoop:Enabled=false on Host (kill switch). Set true in appsettings/.env and recycle Host, then Tick.";
            }
        }

        SystemUnrealTargetBox.Text = string.IsNullOrWhiteSpace(snap.UnrealTarget)
            ? "(missing)"
            : snap.UnrealTarget!;
        SystemUnrealConnectedBox.Text = snap.UnrealConnected is null
            ? "(missing)"
            : (snap.UnrealConnected.Value ? "true" : "false");
        SystemUnrealConnectedBox.Foreground = snap.UnrealConnected == true ? _okBrush : _warnBrush;

        // Charter lock from /health charter.* (DB is_locked).
        if (string.IsNullOrWhiteSpace(snap.CharterMode) && snap.CharterFullyLocked is null)
        {
            CharterLockBox.Text = "(not reported)";
            CharterLockBox.Foreground = Res("MutedBrush");
        }
        else if (snap.CharterFullyLocked == true || string.Equals(snap.CharterMode, "locked", StringComparison.OrdinalIgnoreCase))
        {
            var n = snap.CharterLocked ?? snap.CharterAnchors;
            CharterLockBox.Text = n is null ? "locked" : $"locked ({n}/{n})";
            CharterLockBox.Foreground = _okBrush;
        }
        else if (string.Equals(snap.CharterMode, "empty", StringComparison.OrdinalIgnoreCase))
        {
            CharterLockBox.Text = "empty";
            CharterLockBox.Foreground = _warnBrush;
        }
        else
        {
            var locked = snap.CharterLocked ?? 0;
            var total = snap.CharterAnchors ?? 0;
            CharterLockBox.Text = total > 0 ? $"calibration ({locked}/{total} locked)" : "calibration";
            CharterLockBox.Foreground = _warnBrush;
        }

        // Drift report from /health safety.drift.
        if (snap.DriftActiveCount is null && snap.DriftSloExceeded is null)
        {
            CharterDriftBox.Text = "(not reported)";
            CharterDriftBox.Foreground = Res("MutedBrush");
        }
        else
        {
            var driftCount = snap.DriftActiveCount ?? 0;
            var slo = snap.DriftSloExceeded ?? false;
            CharterDriftBox.Text = $"{driftCount} active · SLO {(slo ? "exceeded" : "ok")}";
            CharterDriftBox.Foreground = slo ? _badBrush
                : (driftCount > 0 ? _warnBrush : _okBrush);
        }

        // Spend meter from /health safety.spend.
        if (snap.SpendTokensIn is null && snap.SpendTokensOut is null
            && snap.SpendEstimatedCost is null && snap.SpendMonthlyCap is null)
        {
            CharterSpendBox.Text = "(not reported)";
            CharterSpendBox.Foreground = Res("MutedBrush");
        }
        else
        {
            var tokensIn = snap.SpendTokensIn ?? 0;
            var tokensOut = snap.SpendTokensOut ?? 0;
            var cost = snap.SpendEstimatedCost ?? 0m;
            var cap = snap.SpendMonthlyCap ?? 0m;
            var capHit = snap.SpendCapExceeded ?? false;
            CharterSpendBox.Text = $"{tokensIn}/{tokensOut} tokens · ${cost:0.0000} / ${cap:0}";
            CharterSpendBox.Foreground = capHit ? _badBrush
                : (cap > 0m && cost / cap > 0.8m ? _warnBrush : _okBrush);
        }
    }

    private void UpdateConnectionChrome(SoulCoreHealthSnapshot snap)
    {
        string wsLabel = _ws.State switch
        {
            WsConnectionState.Connected => "WS connected",
            WsConnectionState.Connecting => "WS connecting",
            WsConnectionState.Unavailable => "WS down (Host offline)",
            WsConnectionState.Blocked => "blocked (non-loopback)",
            _ => "WS disconnected"
        };

        if (_ws.State == WsConnectionState.Connected && _presenceFromWs)
        {
            ConnDot.Fill = _okBrush;
            ConnStatusText.Text = $"Alive · {_ws.State} · {ConnectionDefaults.WsUri}";
            return;
        }

        if (snap.Alive && snap.Warm)
        {
            ConnDot.Fill = _okBrush;
            ConnStatusText.Text = $"Alive · Warm · {wsLabel} · {ConnectionDefaults.DisplayEndpoint}";
        }
        else if (snap.Alive)
        {
            ConnDot.Fill = _warnBrush;
            ConnStatusText.Text = $"Alive · Cool · {wsLabel} · {ConnectionDefaults.DisplayEndpoint}";
        }
        else
        {
            ConnDot.Fill = _badBrush;
            ConnStatusText.Text = $"Offline · {wsLabel} · {ConnectionDefaults.DisplayEndpoint}";
        }
    }

    private void OnWsStateChanged(WsConnectionState state, string detail)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Connection noise stays out of the SMS thread except first connect / hard faults.
            if (state is WsConnectionState.Unavailable or WsConnectionState.Disconnected)
            {
                _presenceFromWs = false;
                _streamingAssistant = null;
            }

            UpdateConnectionChrome(_lastHealth);
        });
    }

    private void OnFrameReceived(SoulCoreFrame frame)
    {
        Dispatcher.UIThread.Post(() => ApplyFrame(frame));
    }

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
                if (SoulLoopHintText is not null)
                    SoulLoopHintText.Text = "Tick acknowledged — waiting for focus update…";
                break;
            case SoulCoreFrameTypes.ChatDelta:
                AppendOrUpdateAssistant(frame, finalize: false);
                break;
            case SoulCoreFrameTypes.ChatDone:
                AppendOrUpdateAssistant(frame, finalize: true);
                break;
            case SoulCoreFrameTypes.Error:
                AppendSystem(
                    $"error: {ReadPayloadString(frame, "message") ?? frame.Payload?.ToString() ?? frame.Type}",
                    persist: false);
                break;
            case SoulCoreFrameTypes.Pong:
                break;
            default:
                // Ignore protocol chatter in the SMS thread.
                break;
        }
    }

    private void ApplyPresenceStatus(SoulCoreFrame frame)
    {
        _presenceFromWs = true;
        var alive = ReadPayloadBool(frame, "alive") ?? true;
        var warm = ReadPayloadBool(frame, "warm") ?? false;

        AliveText.Text = alive ? "yes" : "no";
        AliveText.Foreground = alive ? _okBrush : _badBrush;
        WarmText.Text = warm ? "yes" : "no";
        WarmText.Foreground = warm ? _okBrush : (alive ? _warnBrush : _badBrush);

        UpdateConnectionChrome(_lastHealth);
    }

    private void ApplyLoopWant(SoulCoreFrame frame)
    {
        var want = ReadPayloadString(frame, "want");
        var category = ReadPayloadString(frame, "category");
        var label = ReadPayloadString(frame, "emotionLabel");
        var episodic = ReadPayloadInt(frame, "episodicCount");
        var valence = ReadPayloadDouble(frame, "valence");
        var arousal = ReadPayloadDouble(frame, "arousal");
        var driftAlert = ReadPayloadBool(frame, "driftAlert") == true;

        // Host still ships a wire dump (want[cat]: … (emotion=…); recent=…).
        // Panel shows Mode / Feeling / Focus — Focus is the human phrase only.
        var parsed = ParseWantWire(want, category);

        if (WantStatusText is not null)
        {
            WantStatusText.Text = string.IsNullOrWhiteSpace(parsed.Phrase)
                ? "(no focus text)"
                : parsed.Phrase;
            WantStatusText.Foreground = Res("TextBrush");
        }

        if (WantModeText is not null)
        {
            var mode = parsed.Category;
            if (string.IsNullOrWhiteSpace(mode))
                WantModeText.Text = "—";
            else
                WantModeText.Text = char.ToUpperInvariant(mode[0]) + mode[1..];
        }

        if (WantFeelingText is not null)
        {
            var feeling = string.IsNullOrWhiteSpace(label) ? "—" : label.Trim();
            if (valence is not null || arousal is not null)
            {
                feeling +=
                    $"  ·  calm/intense {FormatMoodAxis(valence)} / energy {FormatMoodAxis(arousal)}";
            }
            WantFeelingText.Text = feeling;
        }

        if (SoulLoopHintText is not null)
        {
            var parts = new List<string>
            {
                $"Updated {DateTimeOffset.Now:h:mm tt}"
            };
            if (episodic is not null)
                parts.Add(episodic.Value == 1 ? "1 recent memory" : $"{episodic.Value} recent memories");
            if (driftAlert)
                parts.Add("drift alert");
            SoulLoopHintText.Text = string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// Pulls category + readable phrase out of the Host want dump.
    /// Example wire:
    /// <c>want[recall]: recall the recent thread… (holding 3 recent beats) (emotion=neutral v=…); recent=…</c>
    /// → phrase = <c>recall the recent thread… (holding 3 recent beats)</c>
    /// </summary>
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

    private static string FormatMoodAxis(double? value) =>
        value is null ? "—" : value.Value.ToString("0.00");

    private void ApplyEmotionSnapshot(SoulCoreFrame frame)
    {
        var label = ReadPayloadString(frame, "label") ?? "—";
        var valence = ReadPayloadDouble(frame, "valence");
        var arousal = ReadPayloadDouble(frame, "arousal");
        var dominance = ReadPayloadDouble(frame, "dominance");
        var focus = ReadPayloadDouble(frame, "focus");

        EmotionLabelText.Text = label;
        ValenceText.Text = valence is null ? "—" : valence.Value.ToString("0.0");
        ArousalText.Text = arousal is null ? "—" : arousal.Value.ToString("0.0");

        if (valence is not null) _lastValence = valence.Value;
        if (arousal is not null) _lastArousal = arousal.Value;
        if (dominance is not null) _lastDominance = dominance.Value;
        if (focus is not null) _lastFocus = focus.Value;
    }

    private void AppendOrUpdateAssistant(SoulCoreFrame frame, bool finalize)
    {
        var text = ReadPayloadString(frame, "text");
        if (string.IsNullOrEmpty(text) && finalize)
        {
            // done with empty text — keep existing streamed bubble
            _streamingAssistant = null;
            return;
        }

        text ??= string.Empty;

        if (_streamingAssistant is not null
            && (string.IsNullOrEmpty(frame.Id) || _streamingAssistant.FrameId == frame.Id))
        {
            // Host may send full text on each delta/done; replace rather than concatenate.
            _streamingAssistant.Text = text;
            if (finalize)
            {
                PersistMessage(_streamingAssistant);
                _streamingAssistant = null;
            }

            ScrollTranscriptToEnd();
            return;
        }

        // New assistant bubble — notify if the window isn't focused.
        var bubble = new ChatMessage
        {
            Role = "assistant",
            Text = text,
            FrameId = frame.Id
        };
        _messages.Add(bubble);
        if (finalize)
        {
            PersistMessage(bubble);
            _streamingAssistant = null;
        }
        else
        {
            _streamingAssistant = bubble;
        }
        NotifyIfUnfocused();
        ScrollTranscriptToEnd();
    }

    private void NotifyIfUnfocused()
    {
        // Only ding when the user isn't already looking at the chat.
        var focused = IsActive && IsVisible && WindowState != WindowState.Minimized;
        if (!focused)
        {
            _notifications.Play();
        }
    }

    private void PersistMessage(ChatMessage message)
    {
        try
        {
            _chatHistory.Save(message);
        }
        catch
        {
            // History must never break the live chat path.
        }
    }

    private void AppendSystem(string text, bool persist = true)
    {
        var msg = new ChatMessage { Role = "system", Text = text };
        _messages.Add(msg);
        if (persist)
            PersistMessage(msg);
        ScrollTranscriptToEnd();
    }

    private void ScrollTranscriptToEnd()
    {
        Dispatcher.UIThread.Post(
            () => TranscriptScroll.Offset = new Vector(TranscriptScroll.Offset.X, TranscriptScroll.Extent.Height),
            DispatcherPriority.Background);
    }

    private static string? ReadPayloadString(SoulCoreFrame frame, string name)
    {
        if (frame.Payload is not { } payload) return null;
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (!payload.TryGetProperty(name, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
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

    private static int? ReadPayloadInt(SoulCoreFrame frame, string name)
    {
        if (frame.Payload is not { } payload) return null;
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (!payload.TryGetProperty(name, out var prop)) return null;
        return prop.TryGetInt32(out var n) ? n : null;
    }

    private static double? ReadPayloadDouble(SoulCoreFrame frame, string name)
    {
        if (frame.Payload is not { } payload) return null;
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (!payload.TryGetProperty(name, out var prop)) return null;
        return prop.TryGetDouble(out var n) ? n : null;
    }
}
