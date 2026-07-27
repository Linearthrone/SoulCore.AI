using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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
    private readonly DispatcherTimer _pollTimer;
    private readonly IBrush _okBrush;
    private readonly IBrush _warnBrush;
    private readonly IBrush _badBrush;
    private LocalUiSettings _uiSettings = LocalUiSettings.Load();
    private SoulCoreHealthSnapshot _lastHealth = new();
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

        _ws.StateChanged += OnWsStateChanged;
        _ws.FrameReceived += OnFrameReceived;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _pollTimer.Tick += async (_, _) => await ProbeHealthAsync();

        Opened += async (_, _) =>
        {
            AppendSystem(
                $"Presence shell → SoulCore WS {ConnectionDefaults.WsUri}. " +
                "Chat/emotion via Host only (no direct LLM from UI).");
            await _ws.ConnectAsync();
            await ProbeHealthAsync();
            _pollTimer.Start();
        };

        Closed += async (_, _) =>
        {
            _pollTimer.Stop();
            SaveDisplayNameFromEditor();
            await _ws.DisposeAsync();
            _health.Dispose();
        };
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
        _messages.Add(new ChatMessage { Role = "user", Text = text });
        ScrollTranscriptToEnd();

        var sent = await _ws.SendChatAsync(text);
        if (!sent)
        {
            _messages.Add(new ChatMessage
            {
                Role = "system",
                Text =
                    "Host WS unavailable — message not sent. " +
                    $"Start SoulCore.Host on {ConnectionDefaults.WsUri}, then Refresh. " +
                    $"Detail: {_ws.LastError}"
            });
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

        // Auto-reconnect attempt when Host comes back while we were unavailable.
        if (snap.Alive
            && _ws.State is WsConnectionState.Unavailable or WsConnectionState.Disconnected)
        {
            await _ws.ConnectAsync();
        }

        UpdateConnectionChrome(snap);
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

    private void ViewCharterAnchors_Click(object? sender, RoutedEventArgs e)
    {
        if (CharterAnchorsPanel is null || CharterAnchorsText is null) return;
        CharterAnchorsPanel.IsVisible = !CharterAnchorsPanel.IsVisible;
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
        }
        else
        {
            SystemSoulLoopBox.Text = snap.SoulLoopEnabled.Value ? "enabled" : "disabled";
            SystemSoulLoopBox.Foreground = snap.SoulLoopEnabled.Value ? _okBrush : _badBrush;
        }

        SystemUnrealTargetBox.Text = string.IsNullOrWhiteSpace(snap.UnrealTarget)
            ? "(missing)"
            : snap.UnrealTarget!;
        SystemUnrealConnectedBox.Text = snap.UnrealConnected is null
            ? "(missing)"
            : (snap.UnrealConnected.Value ? "true" : "false");
        SystemUnrealConnectedBox.Foreground = snap.UnrealConnected == true ? _okBrush : _warnBrush;

        // Charter lock: all anchors use is_locked=0 in the current Host build, so the
        // charter is in calibration mode rather than locked. This is informational; the
        // shell never rewrites charter anchors.
        CharterLockBox.Text = "calibration";
        CharterLockBox.Foreground = _warnBrush;

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
            if (state is WsConnectionState.Unavailable or WsConnectionState.Disconnected)
            {
                _presenceFromWs = false;
                _streamingAssistant = null;
                if (_messages.All(m => !m.Text.StartsWith("Host WS", StringComparison.Ordinal)
                                       && !m.Text.StartsWith("WS receive", StringComparison.Ordinal)
                                       && !m.Text.StartsWith("Host closed", StringComparison.Ordinal)
                                       && !m.Text.StartsWith("Disconnected from SoulCore", StringComparison.Ordinal)))
                {
                    AppendSystem(detail);
                }
            }
            else if (state == WsConnectionState.Connected)
            {
                AppendSystem(detail);
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
                // quiet ack — want arrives via loop.want
                break;
            case SoulCoreFrameTypes.ChatDelta:
                AppendOrUpdateAssistant(frame, finalize: false);
                break;
            case SoulCoreFrameTypes.ChatDone:
                AppendOrUpdateAssistant(frame, finalize: true);
                break;
            case SoulCoreFrameTypes.Error:
                AppendSystem($"error: {ReadPayloadString(frame, "message") ?? frame.Payload?.ToString() ?? frame.Type}");
                break;
            case SoulCoreFrameTypes.Pong:
                // quiet
                break;
            default:
                AppendSystem($"frame {frame.Type} id={frame.Id}");
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
        var label = ReadPayloadString(frame, "emotionLabel");
        var episodic = ReadPayloadInt(frame, "episodicCount");

        var wantText = string.IsNullOrWhiteSpace(want) ? "(empty want)" : want.Trim();
        if (WantStatusText is not null)
        {
            WantStatusText.Text = wantText;
            WantStatusText.Foreground = Res("TextBrush");
        }

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(label)) meta.Add($"emotion={label}");
        if (episodic is not null) meta.Add($"episodic={episodic}");
        var suffix = meta.Count > 0 ? $" · {string.Join(" · ", meta)}" : string.Empty;
        AppendSystem($"loop.want · {wantText}{suffix}");
    }

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
                _streamingAssistant = null;
            }

            ScrollTranscriptToEnd();
            return;
        }

        var bubble = new ChatMessage
        {
            Role = "assistant",
            Text = text,
            FrameId = frame.Id
        };
        _messages.Add(bubble);
        _streamingAssistant = finalize ? null : bubble;
        ScrollTranscriptToEnd();
    }

    private void AppendSystem(string text)
    {
        _messages.Add(new ChatMessage { Role = "system", Text = text });
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
