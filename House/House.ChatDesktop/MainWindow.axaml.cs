using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using IoPath = System.IO.Path;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using House.ChatDesktop.Models;
using House.ChatDesktop.Services;
using SoulCore.Protocol;

namespace House.ChatDesktop;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ChatMessage> _messages = new();
    private readonly SoulCoreHealthClient _health = new();
    private readonly SoulCoreTaskClient _taskClient = new();
    private readonly SoulCoreWsClient _ws = new();
    private readonly SoulCoreToolsSettingsClient _toolsSettings = new();
    private readonly SoulCoreEmailSettingsClient _emailSettings = new();
    private readonly SoulCoreIdentitySettingsClient _identitySettings = new();
    private readonly SoulCoreActivitiesClient _activities = new();
    private readonly SoulCoreDesktopViewClient _desktopView = new();
    private readonly SoulCoreBrowserViewClient _browserView = new();
    private int _identityRefreshGeneration;
    private bool _identityPendingBusy;
    private readonly CompanionMediaClient _media = new();
    private readonly LocalStackControl _stack;
    private readonly ChatHistoryStore _chatHistory = new();
    private readonly SoulCoreVoiceClient _voice = new();
    private readonly PushToTalkRecorder _ptt = new();
    private readonly CallMicStreamer _callMic = new();
    private readonly CallAudioPlayer _callPlayer = new();
    private bool _callActive;

    private bool _pttBusy;
    private bool _toolsAccessHydrating;
    private bool _toolsDefaultsApplied;
    private bool _stackBootstrapBusy;
    private string? _pendingQuote;
    private IReadOnlyList<EmailAccountSnapshot> _emailAccounts = Array.Empty<EmailAccountSnapshot>();
    private bool _servicesBusy;
    /// <summary>Supersedes in-flight host start/restart when Stop force-kills.</summary>
    private int _hostActionGeneration;
    private string? _activeServiceTag;
    private CancellationTokenSource? _hostActionCts;
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _desktopViewTimer;
    private readonly DispatcherTimer _browserViewTimer;
    private bool _desktopViewBusy;
    private bool _browserViewBusy;
    private string? _lastBrowserImageHash;
    private int _desktopImageWidth;
    private int _desktopImageHeight;
    private int? _desktopCursorX;
    private int? _desktopCursorY;
    private string? _lastDesktopImageHash;
    private string? _lastDesktopDiskPath;
    private string? _lastDesktopGalleryDir;
    private string? _lastDesktopGallerySignature;
    private string? _lastNewestGalleryFileName;
    private string? _pinnedGalleryFileName;
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
    private string? _lastWantCategory;
    private string? _lastWantPhrase;
    private string? _lastActivityPhrase;
    private string? _lastJournalCrumb;
    private DateTimeOffset? _lastChatActivityUtc;
    private DateTimeOffset? _lastWantUtc;
    private DateTimeOffset? _lastTickUtc;
    private bool _whileYouWereGoneLoaded;
    private string? _pendingImagePath;
    private Bitmap? _pendingImageBitmap;

    public MainWindow()
    {
        PresenceStartupLog.Write("MainWindow ctor begin");
        InitializeComponent();
        ChatInput.AddHandler(InputElement.KeyDownEvent, ChatInput_KeyDown, RoutingStrategies.Tunnel);
        PresenceStartupLog.Write("InitializeComponent done");
        TranscriptList.ItemsSource = _messages;
        EndpointText.Text = ConnectionDefaults.DisplayEndpoint;
        if (DisplayNameBox is not null)
            DisplayNameBox.Text = _uiSettings.DisplayName;
        if (IdentityNameText is not null)
            IdentityNameText.Text = string.IsNullOrWhiteSpace(_uiSettings.DisplayName)
                ? "Victoria"
                : _uiSettings.DisplayName.Trim();
        _stack = new LocalStackControl(_uiSettings.SoulCoreRepoRoot);
        InitLayoutChrome();
        InitPresenceUpdates();
        InitStackSettingsUi();
        InitGlassShell();

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

        // FED-196: near-live Victoria Playwright pane (~5 fps so aim cursor is visible while she drives).
        _browserViewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _browserViewTimer.Tick += async (_, _) => await RefreshVictoriaBrowserViewAsync();

        Opened += async (_, _) =>
        {
            WirePushToTalk();
            await RefreshVoiceStatusAsync();
        };

        Opened += async (_, _) =>
        {
            LoadChatHistory();
            _ = RefreshIdentityDetailAsync();
            // PROP-22.3: API keys saved from Settings reach a Presence-started Host via process env.
            SecretOverrideStore.ApplyToProcessEnv();
            await EnsureLocalStackOnOpenAsync().ConfigureAwait(true);
            await _ws.ConnectAsync();
            await ProbeHealthAsync();
            await LoadWhileYouWereGoneOnOpenAsync().ConfigureAwait(true);
            _pollTimer.Start();
            _desktopViewTimer.Start();
            _browserViewTimer.Start();
            await RefreshDesktopViewAsync();
            await RefreshVictoriaBrowserViewAsync();
            await EnsureDesktopBrowserDefaultsAsync();
        };

        // Detach SetParent'd VirtualBox BEFORE the Avalonia host HWND dies. Closing only
        // disposed the overlay — the guest window stayed a child of a dying parent and was
        // destroyed while the VM process kept "running" (JPEG forever, no HerScreenInput).
        Closing += (_, _) =>
        {
            try { PersistPresenceSeenAt(); } catch { /* ignore */ }
            try { _browserViewTimer.Stop(); } catch { /* ignore */ }
            try { DestroyVictoriaBrowserEmbedHost(); } catch { /* ignore */ }
            try { CloseHerScreenOverlay(); } catch { /* ignore */ }
        };

        Closed += async (_, _) =>
        {
            _pollTimer.Stop();
            _desktopViewTimer.Stop();
            _browserViewTimer.Stop();
            CloseHerScreenOverlay();
            DisposeGlassShell();
            await _ws.DisposeAsync();
            _notifications.Dispose();
            _health.Dispose();
            _toolsSettings.Dispose();
            _emailSettings.Dispose();
            _identitySettings.Dispose();
            _activities.Dispose();
            _desktopView.Dispose();
            _browserView.Dispose();
            _media.Dispose();
            _stack.Dispose();
            _chatHistory.Dispose();
            _pendingImageBitmap?.Dispose();
        };
    }

    private void LoadChatHistory()
    {
        try
        {
            var prior = _chatHistory.LoadRecent();
            _messages.Clear();
            foreach (var m in prior)
            {
                // PROP-22.4: system rows from older builds never render in the transcript.
                if (m.IsSystem)
                    continue;

                if (!string.IsNullOrWhiteSpace(m.MediaPath) && File.Exists(m.MediaPath))
                {
                    try { m.Image = new Bitmap(m.MediaPath); }
                    catch { /* keep text-only */ }
                }

                _messages.Add(m);
            }

            if (_messages.Count > 0)
                ScrollTranscriptToEnd();
        }
        catch (Exception ex)
        {
            if (PttHintText is not null)
                PttHintText.Text = $"History load failed: {ex.Message}";
        }
    }

    private static IBrush Res(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var v) && v is IBrush b
            ? b
            : Brushes.Gray;

    private void TitleDragRegion_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        // Frameless: drag to move when restored. When maximized, ignore (use Restore / double-click).
        if (WindowState == WindowState.Maximized)
            return;
        BeginMoveDrag(e);
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    /// <summary>PROP-20.1: Identity tab paints live GET /settings/identity anchors (not /health lock fraction alone).</summary>
    private void UpdateIdentityDetail() => _ = RefreshIdentityDetailAsync();

    private async Task RefreshIdentityDetailAsync()
    {
        var gen = Interlocked.Increment(ref _identityRefreshGeneration);
        var localName = string.IsNullOrWhiteSpace(_uiSettings.DisplayName)
            ? "Victoria"
            : _uiSettings.DisplayName.Trim();
        ApplyIdentityDisplayName(localName);

        if (IdentityCharterKindsText is not null && IdentityCharterKindsText.Text is "Loading charter…" or null or "")
            IdentityCharterKindsText.Text = "Loading charter from Host…";
        if (IdentityRestingTraitsText is not null &&
            IdentityRestingTraitsText.Text is "Loading resting traits…" or null or "")
            IdentityRestingTraitsText.Text = "Loading resting traits from Host…";
        if (IdentityContextTraitsText is not null &&
            IdentityContextTraitsText.Text is "Loading context traits…" or null or "")
            IdentityContextTraitsText.Text = "Loading context traits from Host…";
        if (IdentityBondCountersText is not null &&
            IdentityBondCountersText.Text is "Loading bond counters…" or null or "")
            IdentityBondCountersText.Text = "Loading bond counters from Host…";
        if (IdentitySoakText is not null && IdentitySoakText.Text is "Loading soak…" or null or "")
            IdentitySoakText.Text = "Loading soak from Host…";
        if (IdentityGrowthTraitsText is not null &&
            IdentityGrowthTraitsText.Text is "Loading growth traits…" or null or "")
            IdentityGrowthTraitsText.Text = "Loading growth traits from Host…";
        if (IdentityPendingBandCrossText is not null &&
            IdentityPendingBandCrossText.Text is "Loading pending…" or null or "")
            IdentityPendingBandCrossText.Text = "Loading pending band-cross from Host…";
        if (IdentityGrowthLedgerText is not null &&
            IdentityGrowthLedgerText.Text is "Loading growth ledger…" or null or "")
            IdentityGrowthLedgerText.Text = "Loading growth ledger from Host…";

        var identity = await _identitySettings.GetAsync().ConfigureAwait(true);
        if (gen != _identityRefreshGeneration)
            return;
        _lastIdentity = identity;
        ApplyPortraitIdentity(identity);

        var name = !string.IsNullOrWhiteSpace(identity.DisplayName)
            ? identity.DisplayName.Trim()
            : localName;
        ApplyIdentityDisplayName(name);

        var charterSummary = IdentityAnchorsFormatter.FormatCharterSummary(identity);
        if (IdentityCharterBox is not null)
            IdentityCharterBox.Text = charterSummary;
        if (IdentityDetailBox is not null)
            IdentityDetailBox.Text = $"{name} · {charterSummary}";

        if (IdentityCharterKindsText is not null)
            IdentityCharterKindsText.Text = IdentityAnchorsFormatter.FormatAnchorsBlock(identity);

        // PROP-20.31: weight / band / ledgerReason from Host restingTraits (+ context/catalog).
        if (IdentityRestingTraitsText is not null)
            IdentityRestingTraitsText.Text = IdentityAnchorsFormatter.FormatRestingTraitsBlock(identity);
        if (IdentityContextTraitsText is not null)
            IdentityContextTraitsText.Text = IdentityAnchorsFormatter.FormatContextTraitsBlock(identity);

        // PROP-20.38: bond counters, soak, growth traits, pending band-cross, ledger.
        if (IdentityBondCountersText is not null)
            IdentityBondCountersText.Text = IdentityAnchorsFormatter.FormatBondCountersBlock(identity);
        if (IdentitySoakText is not null)
            IdentitySoakText.Text = IdentityAnchorsFormatter.FormatSoakBlock(identity);
        if (IdentityGrowthTraitsText is not null)
            IdentityGrowthTraitsText.Text = IdentityAnchorsFormatter.FormatGrowthTraitsBlock(identity);
        if (IdentityPendingBandCrossText is not null)
            IdentityPendingBandCrossText.Text = IdentityAnchorsFormatter.FormatPendingBandCrossStatus(identity);
        if (IdentityPendingBandCrossList is not null)
        {
            IdentityPendingBandCrossList.ItemsSource = identity.Reachable &&
                                                       string.IsNullOrWhiteSpace(identity.Detail)
                ? identity.PendingBandCross
                : Array.Empty<IdentityPendingBandCrossSnapshot>();
        }

        if (IdentityGrowthLedgerText is not null)
            IdentityGrowthLedgerText.Text = IdentityAnchorsFormatter.FormatGrowthLedgerBlock(identity);

        if (IdentityStatsText is not null)
        {
            IdentityStatsText.Text =
                "Memories — live / quarantined / newest: Host detail not on /settings/identity\n" +
                "Books — feeling · animation · environment · life-hacks · thoughts · wants · dreams (Her journal…)\n" +
                "SOP count — awaits PROP-17.2 store\n" +
                "Tasks — listed under Her screen; only you can delete one\n" +
                "Workflows — not reported on /settings/identity\n" +
                "Parked + next probe — shown on the task row when the Host reports it\n" +
                "Current interest — On her mind, in the band under Her screen";
        }

        var health = _lastHealth;
        if (IdentityPromptMixText is not null)
        {
            var soakPhase = identity.Soak is null
                ? "—"
                : (string.IsNullOrWhiteSpace(identity.Soak.Phase) ? "?" : identity.Soak.Phase);
            IdentityPromptMixText.Text =
                $"Alive {(health.Alive ? "yes" : "no")} · Warm {(health.Warm ? "yes" : "no")} · " +
                $"SoulLoop {FormatSoulLoop(health)} · Charter {charterSummary} · " +
                $"Resting {identity.RestingTraits.Count} · Context this turn {identity.ContextTraits.Count} · " +
                $"Growth {identity.GrowthTraits.Count} · Soak {soakPhase} · " +
                $"Pending band-cross {identity.PendingBandCross.Count}";
        }
    }

    private async void IdentityPendingConfirm_Click(object? sender, RoutedEventArgs e)
    {
        if (_identityPendingBusy)
            return;
        if (sender is not Button { DataContext: IdentityPendingBandCrossSnapshot row } || row.Id <= 0)
            return;

        _identityPendingBusy = true;
        try
        {
            var result = await _identitySettings.ConfirmPendingAsync(row.Id).ConfigureAwait(true);
            await RefreshIdentityDetailAsync().ConfigureAwait(true);
            if (!result.Ok && IdentityPendingBandCrossText is not null)
            {
                IdentityPendingBandCrossText.Text =
                    result.Detail ?? $"Could not confirm pending #{row.Id}.";
            }
        }
        finally
        {
            _identityPendingBusy = false;
        }
    }

    private async void IdentityPendingDismiss_Click(object? sender, RoutedEventArgs e)
    {
        if (_identityPendingBusy)
            return;
        if (sender is not Button { DataContext: IdentityPendingBandCrossSnapshot row } || row.Id <= 0)
            return;

        _identityPendingBusy = true;
        try
        {
            var result = await _identitySettings.DismissPendingAsync(row.Id).ConfigureAwait(true);
            await RefreshIdentityDetailAsync().ConfigureAwait(true);
            if (!result.Ok && IdentityPendingBandCrossText is not null)
            {
                IdentityPendingBandCrossText.Text =
                    result.Detail ?? $"Could not dismiss pending #{row.Id}.";
            }
        }
        finally
        {
            _identityPendingBusy = false;
        }
    }

    private void ApplyIdentityDisplayName(string name)
    {
        if (DisplayNameBox is not null)
            DisplayNameBox.Text = name;
        if (IdentityNameText is not null)
            IdentityNameText.Text = name;
    }

    private static string FormatSoulLoop(SoulCoreHealthSnapshot snap) =>
        snap.SoulLoopEnabled is null ? "(not reported)"
        : snap.SoulLoopEnabled.Value ? "enabled" : "disabled";

    private async void SoulLoopTick_Click(object? sender, RoutedEventArgs e) =>
        await ForceSoulLoopTickAsync(restartWs: false).ConfigureAwait(true);

    private async void SoulLoopRestart_Click(object? sender, RoutedEventArgs e) =>
        await ForceSoulLoopTickAsync(restartWs: true).ConfigureAwait(true);

    private async Task ForceSoulLoopTickAsync(bool restartWs)
    {
        if (_lastHealth.SoulLoopEnabled == false)
        {
            if (ActivityText is not null)
                ActivityText.Text = "SoulLoop off on Host";
            UpdateEngagementState();
            return;
        }

        if (restartWs)
            await _ws.ConnectAsync().ConfigureAwait(true);

        if (_ws.State != WsConnectionState.Connected)
            return;

        await _ws.SendLoopTickAsync().ConfigureAwait(true);
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        await _ws.ConnectAsync();
        await ProbeHealthAsync();
    }

    private async void Send_Click(object? sender, RoutedEventArgs e) => await SendCurrentAsync();

    private void ChatInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // The composer grows through MinHeight/MaxHeight in XAML — nothing to size by hand.
    }

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

    private void UpdateAvailableMark_Click(object? sender, RoutedEventArgs e) =>
        ShowScreen(Screen.Settings);

    private void CorrectEmotionCancel_Click(object? sender, RoutedEventArgs e) =>
        EmotionCorrectPanel.IsVisible = false;

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
            if (PttHintText is not null)
                PttHintText.Text = $"Mood correct not sent — {_ws.LastError}";
            return;
        }

        _lastValence = valence;
        _lastArousal = arousal;
        _lastDominance = dominance;
        _lastFocus = focus;
        ApplyMoodToHud(string.IsNullOrWhiteSpace(MoodLabelText.Text) || MoodLabelText.Text == "—"
            ? "corrected"
            : MoodLabelText.Text, valence, arousal);
        EmotionCorrectPanel.IsVisible = false;
    }

    private void SeedCorrectionEditorsFromLastSnapshot()
    {
        CorrectValenceSlider.Value = _lastValence;
        CorrectArousalSlider.Value = _lastArousal;
        CorrectDominanceSlider.Value = _lastDominance;
        CorrectFocusSlider.Value = _lastFocus;
        if (string.IsNullOrWhiteSpace(CorrectNoteBox.Text))
            CorrectNoteBox.Text = "that wasn’t how I felt";
    }

    private async void ChatInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (await TryPasteImageAsync().ConfigureAwait(true))
            {
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            await SendCurrentAsync();
        }
    }

    private Task<bool> TryPasteImageAsync()
    {
        // Avalonia clipboard file paste APIs vary by platform; Attach remains the primary MMS path.
        return Task.FromResult(false);
    }

    private async void AttachImage_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Attach image",
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
        if (string.IsNullOrWhiteSpace(path)) return;
        SetPendingImage(path);
    }

    private void ClearPendingImage_Click(object? sender, RoutedEventArgs e) => ClearPendingImage();

    private void SetPendingImage(string path)
    {
        try
        {
            _pendingImageBitmap?.Dispose();
            _pendingImageBitmap = new Bitmap(path);
            _pendingImagePath = path;
            if (PendingImageChip is not null) PendingImageChip.IsVisible = true;
            if (PendingImageLabel is not null) PendingImageLabel.Text = IoPath.GetFileName(path);
        }
        catch (Exception ex)
        {
            if (PttHintText is not null)
                PttHintText.Text = $"Image load failed: {ex.Message}";
            ClearPendingImage();
        }
    }

    private void ClearPendingImage()
    {
        _pendingImageBitmap?.Dispose();
        _pendingImageBitmap = null;
        _pendingImagePath = null;
        if (PendingImageChip is not null) PendingImageChip.IsVisible = false;
        if (PendingImageLabel is not null) PendingImageLabel.Text = "image";
    }

    private static bool IsImagePath(string path)
    {
        var ext = IoPath.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp";
    }

    private async Task SendCurrentAsync()
    {
        var text = (ChatInput.Text ?? string.Empty).Trim();
        var hasImage = !string.IsNullOrWhiteSpace(_pendingImagePath) && _pendingImageBitmap is not null;
        if (string.IsNullOrEmpty(text) && !hasImage) return;

        string? wireImageB64 = null;
        if (hasImage && _pendingImagePath is not null)
            wireImageB64 = TryReadImageBase64ForChat(_pendingImagePath);

        if (hasImage && string.IsNullOrEmpty(text))
            text = $"[image] {IoPath.GetFileName(_pendingImagePath)}";
        else if (hasImage)
            text = $"{text}\n[image] {IoPath.GetFileName(_pendingImagePath)}";

        ChatInput.Text = string.Empty;
        _streamingAssistant = null;
        var quoted = _pendingQuote;
        ClearQuote();

        string? cachedPath = null;
        Bitmap? bubbleImage = null;
        if (hasImage && _pendingImagePath is not null)
        {
            cachedPath = CacheOutboundImage(_pendingImagePath);
            try { bubbleImage = new Bitmap(cachedPath ?? _pendingImagePath); }
            catch { bubbleImage = null; }
        }

        var displayText = string.IsNullOrWhiteSpace(quoted)
            ? text
            : $"↪ {TruncateForChip(quoted, 120)}\n{text}";

        var userMsg = new ChatMessage
        {
            Role = "user",
            Text = displayText,
            MediaPath = cachedPath,
            Image = bubbleImage
        };
        _messages.Add(userMsg);
        PersistMessage(userMsg);
        ClearPendingImage();
        _lastChatActivityUtc = DateTimeOffset.UtcNow;
        SetTyping(true);
        UpdateEngagementState();
        ScrollTranscriptToEnd();

        IReadOnlyList<string>? images = string.IsNullOrWhiteSpace(wireImageB64)
            ? null
            : new[] { wireImageB64 };
        if (hasImage && images is null && PttHintText is not null)
            PttHintText.Text = "Image too large or unreadable — sent as a filename note only.";

        var sent = await _ws.SendChatAsync(text, quoted, images);
        if (!sent)
        {
            SetTyping(false);
            AppendSystemNotice(
                "Host WS unavailable — message not sent. " +
                $"Start SoulCore.Host on {ConnectionDefaults.WsUri} (Presence auto-starts it when Auto-start is on). " +
                $"Detail: {_ws.LastError}");
            ScrollTranscriptToEnd();
        }
    }

    /// <summary>
    /// Raw file → base64 for <c>chat.send images[]</c>. Host JPEG-compresses for Ollama.
    /// Cap matches ToolImagePayload.ChatAttachMaxRawBytes (8 MB).
    /// </summary>
    private static string? TryReadImageBase64ForChat(string path)
    {
        try
        {
            const int maxRaw = 8_000_000;
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > maxRaw)
                return null;
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0 || bytes.Length > maxRaw)
                return null;
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static string? CacheOutboundImage(string sourcePath)
    {
        try
        {
            var dir = IoPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HouseVictoria",
                "mms");
            Directory.CreateDirectory(dir);
            var dest = IoPath.Combine(dir, $"{Guid.NewGuid():N}{IoPath.GetExtension(sourcePath)}");
            File.Copy(sourcePath, dest, overwrite: true);
            return dest;
        }
        catch
        {
            return sourcePath;
        }
    }

    private void SetTyping(bool on)
    {
        if (TypingIndicator is not null)
            TypingIndicator.IsVisible = on;
        _typingActive = on;
        RefreshStatusOrb();
    }

    private void PersistMessage(ChatMessage message)
    {
        try { _chatHistory.Save(message); }
        catch { /* History must never break the live chat path. */ }
    }

    private void ScrollTranscriptToEnd()
    {
        Dispatcher.UIThread.Post(
            () => TranscriptScroll.Offset = new Vector(TranscriptScroll.Offset.X, TranscriptScroll.Extent.Height),
            DispatcherPriority.Background);
    }

    private void SetPendingQuote(string? excerpt)
    {
        var trimmed = (excerpt ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ClearQuote();
            return;
        }

        const int max = 2000;
        _pendingQuote = trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
        if (QuoteChip is not null) QuoteChip.IsVisible = true;
        if (QuoteChipText is not null) QuoteChipText.Text = _pendingQuote;
    }

    private void ClearQuote()
    {
        _pendingQuote = null;
        if (QuoteChip is not null) QuoteChip.IsVisible = false;
        if (QuoteChipText is not null) QuoteChipText.Text = string.Empty;
    }

    private void ClearQuote_Click(object? sender, RoutedEventArgs e) => ClearQuote();

    private void MessageText_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Selection is consumed via context menu / Quote button — no auto-quote on release.
    }

    private void QuoteSelection_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Parent: ContextMenu { PlacementTarget: SelectableTextBlock tb } })
            return;
        var selected = tb.SelectedText;
        if (string.IsNullOrWhiteSpace(selected))
            selected = tb.Text;
        SetPendingQuote(selected);
    }

    private void QuoteMessage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Parent: ContextMenu { PlacementTarget: SelectableTextBlock tb } })
            return;
        SetPendingQuote(tb.Text);
    }

    private void QuoteMessageButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ChatMessage msg })
            return;
        SetPendingQuote(msg.Text);
    }

    private static string TruncateForChip(string text, int max)
    {
        var oneLine = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return oneLine.Length <= max ? oneLine : oneLine[..max] + "…";
    }
}
