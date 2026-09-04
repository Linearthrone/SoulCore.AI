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
    private readonly SoulCoreWsClient _ws = new();
    private readonly SoulCoreToolsSettingsClient _toolsSettings = new();
    private readonly SoulCoreEmailSettingsClient _emailSettings = new();
    private readonly SoulCoreDesktopViewClient _desktopView = new();
    private readonly SoulCoreBrowserViewClient _browserView = new();
    private readonly CompanionMediaClient _media = new();
    private readonly LocalStackControl _stack = new();
    private readonly ChatHistoryStore _chatHistory = new();
    private readonly SoulCoreVoiceClient _voice = new();
    private readonly PushToTalkRecorder _ptt = new();
    private readonly PanelPopOutService _popOuts = new();
    private readonly ToggleButtonState _hudTop = new();
    private readonly ToggleButtonState _chatTop = new();
    private readonly ToggleButtonState _servicesTop = new();
    private readonly ToggleButtonState _screenTop = new();

    private bool _pttBusy;
    private bool _toolsAccessHydrating;
    private bool _toolsDefaultsApplied;
    private bool _emailAccountsHydrating;
    private string? _pendingQuote;
    private IReadOnlyList<EmailAccountSnapshot> _emailAccounts = Array.Empty<EmailAccountSnapshot>();
    private bool _servicesBusy;
    private bool _houseDrawerOpen;
    private DateTimeOffset? _soulCoreHoldStarted;
    private DispatcherTimer? _soulCoreHoldTimer;
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
    private string? _lastWantCategory;
    private string? _lastActivityPhrase;
    private DateTimeOffset? _lastChatActivityUtc;
    private DateTimeOffset? _lastWantUtc;
    private string? _pendingImagePath;
    private Bitmap? _pendingImageBitmap;
    private Panel? _hudDock;
    private Panel? _chatDock;
    private Panel? _servicesDock;
    private Panel? _screenDock;

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

        _hudDock = HudPanel.Parent as Panel;
        _chatDock = ChatPanel.Parent as Panel;
        _servicesDock = ServicesPanel.Parent as Panel;
        _screenDock = ScreenPanel.Parent as Panel;

        _ws.StateChanged += OnWsStateChanged;
        _ws.FrameReceived += OnFrameReceived;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _pollTimer.Tick += async (_, _) => await ProbeHealthAsync();

        _desktopViewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _desktopViewTimer.Tick += async (_, _) => await RefreshDesktopViewAsync();

        // FED-196: near-live Victoria Playwright pane (~2 fps when frames arrive).
        _browserViewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _browserViewTimer.Tick += async (_, _) => await RefreshVictoriaBrowserViewAsync();

        Opened += async (_, _) =>
        {
            WirePushToTalk();
            await RefreshVoiceStatusAsync();
        };

        Opened += async (_, _) =>
        {
            LoadChatHistory();
            UpdateIdentityDetail();
            await _ws.ConnectAsync();
            await ProbeHealthAsync();
            _pollTimer.Start();
            _desktopViewTimer.Start();
            _browserViewTimer.Start();
            await RefreshDesktopViewAsync();
            await RefreshVictoriaBrowserViewAsync();
            await EnsureDesktopBrowserDefaultsAsync();
        };

        Closed += async (_, _) =>
        {
            _pollTimer.Stop();
            _desktopViewTimer.Stop();
            _browserViewTimer.Stop();
            _desktopPopOut?.Close();
            _popOuts.CloseAll();
            SaveDisplayNameFromEditor();
            await _ws.DisposeAsync();
            _notifications.Dispose();
            _health.Dispose();
            _toolsSettings.Dispose();
            _emailSettings.Dispose();
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

    private void Nav_Changed(object? sender, RoutedEventArgs e)
    {
        // Legacy radio path — Presence is default; Settings toggled via NavSettings_Click.
        if (PresenceView is null || SettingsView is null) return;
        if (NavPresence is not null && NavPresence.IsChecked == true)
            ShowPresenceView();
    }

    private void NavSettings_Click(object? sender, RoutedEventArgs e)
    {
        if (SettingsView is { IsVisible: true })
        {
            ShowPresenceView();
            return;
        }

        ShowSettingsView();
    }

    private void ShowPresenceView()
    {
        if (PresenceView is null || SettingsView is null) return;
        PresenceView.IsVisible = true;
        SettingsView.IsVisible = false;
        if (NavSettings is not null)
            NavSettings.Classes.Remove("active");
        if (NavPresence is not null)
            NavPresence.IsChecked = true;
        Title = "House Victoria — Presence";
        _ = RefreshDesktopViewAsync();
        _ = RefreshVictoriaBrowserViewAsync();
    }

    private void ShowSettingsView()
    {
        if (PresenceView is null || SettingsView is null) return;
        PresenceView.IsVisible = false;
        SettingsView.IsVisible = true;
        if (NavSettings is not null && !NavSettings.Classes.Contains("active"))
            NavSettings.Classes.Add("active");
        if (NavPresence is not null)
            NavPresence.IsChecked = false;
        Title = "House Victoria — Settings";
        ApplySystemStatus(_lastHealth);
        SeedNotificationControls();
        UpdateIdentityDetail();
        _ = RefreshToolsAccessAsync();
        _ = RefreshEmailAccountsAsync();
    }

    private void OpenPresenceFromIdentity_Click(object? sender, RoutedEventArgs e) =>
        ShowPresenceView();

    private void TitleDragRegion_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

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
            return;

        _uiSettings.DisplayName = name;
        _uiSettings.Save();
        UpdateIdentityDetail();
    }

    private void UpdateIdentityDetail()
    {
        if (IdentityDetailBox is null) return;

        var name = string.IsNullOrWhiteSpace(_uiSettings.DisplayName) ? "Victoria" : _uiSettings.DisplayName.Trim();
        var snap = _lastHealth;
        var charter = FormatCharterLine(snap);

        IdentityDetailBox.Text =
            $"{name}\n\n" +
            "Persistent AI companion for House Victoria (SoulCore).\n" +
            "She chats over the Presence WebSocket, keeps episodic memory in Host SQLite, " +
            "and optionally drives desktop tools and an Unreal avatar.\n\n" +
            "Identity anchors (persona / safety envelope) live in SoulCore charter storage. " +
            "This shell shows Host-reported charter lock state and the local display name — " +
            "it does not rewrite Host persona YAML.\n\n" +
            $"Endpoint: {ConnectionDefaults.DisplayEndpoint}\n" +
            $"Alive: {(snap.Alive ? "yes" : "no")} · Warm: {(snap.Warm ? "yes" : "no")}\n" +
            $"SoulLoop: {FormatSoulLoop(snap)}\n" +
            $"Charter: {charter}";

        if (IdentityCharterBox is not null)
            IdentityCharterBox.Text = charter;
    }

    private static string FormatSoulLoop(SoulCoreHealthSnapshot snap) =>
        snap.SoulLoopEnabled is null ? "(not reported)"
        : snap.SoulLoopEnabled.Value ? "enabled" : "disabled";

    private static string FormatCharterLine(SoulCoreHealthSnapshot snap)
    {
        if (!snap.Reachable)
            return snap.Detail ?? "Host unreachable";

        if (string.IsNullOrWhiteSpace(snap.CharterMode) && snap.CharterFullyLocked is null)
            return "(charter not reported on /health)";

        var locked = snap.CharterLocked ?? 0;
        var total = snap.CharterAnchors ?? 0;
        var mode = snap.CharterMode ?? (snap.CharterFullyLocked == true ? "locked" : "calibration");
        return $"{mode} · {locked}/{total} anchors locked";
    }

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

        // Host chat.send is text-only today — UI ready; attach note in text when image present.
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

        var sent = await _ws.SendChatAsync(text, quoted);
        if (!sent)
        {
            SetTyping(false);
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
