using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using House.ChatDesktop.Controls;
using House.ChatDesktop.Services;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace House.ChatDesktop;

/// <summary>
/// PROP-22.4–22.6 — glass shell plumbing: status orb + radial popup, VM drawer popup,
/// call overlay phases, push-to-talk lamp, performance log.
/// </summary>
public partial class MainWindow
{
    internal const string NothingRightNow = "Nothing right now";

    private enum CallPhase
    {
        None,
        Connecting,
        Connected
    }

    private readonly PerformanceLog _performanceLog = new();

    // Constructed (cheap, never shown) so the BrowserView/Desktop partials can address the Her-screen
    // controls through the shims below. The NativeControlHost is only added when an embed binds.
    private readonly VmDrawerWindow _vmDrawer = new();

    private RadialStatusWindow? _radial;
    private DispatcherTimer? _orbTimer;
    private SoulCoreHealthSnapshot? _initialHealth;
    private IReadOnlyList<ServiceStatus> _orbServices = Array.Empty<ServiceStatus>();
    private OrbState? _orbState;
    private DateTime _orbAnimStartUtc = DateTime.UtcNow;
    private bool _glassShuttingDown;
    private CallPhase _callPhase;

    private bool _typingActive;
    private bool _ollamaUp;
    private bool _comfyUp;
    private bool _voiceEnabled;
    private bool _voiceSttOk;
    private bool _voiceTtsOk;
    private string? _presenceCurrentActivity;

    // ── Her-screen shims (controls live in VmDrawerWindow) ───────────────────────────────────────
    internal Button HerScreenHeaderButton => _vmDrawer.HerScreenHeaderButton;
    internal Button SightHeaderButton => _vmDrawer.SightHeaderButton;
    internal Border VictoriaBrowserCoordBadge => _vmDrawer.VictoriaBrowserCoordBadge;
    internal TextBlock VictoriaBrowserCoordText => _vmDrawer.VictoriaBrowserCoordText;
    internal Button VmPowerButton => _vmDrawer.VmPowerButton;
    internal Border VictoriaBrowserPanel => _vmDrawer.VictoriaBrowserPanel;
    internal Border VictoriaBrowserSurface => _vmDrawer.VictoriaBrowserSurface;
    internal TextBlock VictoriaBrowserEmptyText => _vmDrawer.VictoriaBrowserEmptyText;
    internal Image VictoriaBrowserImage => _vmDrawer.VictoriaBrowserImage;
    internal Panel VictoriaBrowserEmbedSlot => _vmDrawer.VictoriaBrowserEmbedSlot;
    internal Canvas VictoriaBrowserCursorLayer => _vmDrawer.VictoriaBrowserCursorLayer;
    internal ShapePath VictoriaBrowserCursor => _vmDrawer.VictoriaBrowserCursor;
    internal TextBlock VictoriaBrowserUrlText => _vmDrawer.VictoriaBrowserUrlText;
    internal TextBlock VictoriaBrowserTitleText => _vmDrawer.VictoriaBrowserTitleText;
    internal TextBlock VictoriaBrowserActionText => _vmDrawer.VictoriaBrowserActionText;
    internal TextBlock VictoriaBrowserWaitingText => _vmDrawer.VictoriaBrowserWaitingText;
    internal Border ScreenPanel => _vmDrawer.ScreenPanel;
    internal Button DesktopViewFolderButton => _vmDrawer.DesktopViewFolderButton;
    internal Grid DesktopViewSurface => _vmDrawer.DesktopViewSurface;
    internal TextBlock DesktopViewEmptyText => _vmDrawer.DesktopViewEmptyText;
    internal Image DesktopViewImage => _vmDrawer.DesktopViewImage;
    internal Canvas DesktopViewCursorLayer => _vmDrawer.DesktopViewCursorLayer;
    internal Ellipse DesktopViewCursor => _vmDrawer.DesktopViewCursor;
    internal TextBlock DesktopViewStampText => _vmDrawer.DesktopViewStampText;
    internal TextBlock DesktopViewActionText => _vmDrawer.DesktopViewActionText;
    internal TextBlock DesktopViewMetaText => _vmDrawer.DesktopViewMetaText;
    internal TextBlock DesktopViewPathText => _vmDrawer.DesktopViewPathText;
    internal StackPanel DesktopViewGalleryPanel => _vmDrawer.DesktopViewGalleryPanel;

    private void InitGlassShell()
    {
        _initialHealth = _lastHealth;

        // Her screen drawer: handlers that used to be XAML attributes.
        _vmDrawer.CloseRequested += (_, _) => HideVmDrawer();
        _vmDrawer.Closing += (_, e) =>
        {
            if (_glassShuttingDown)
                return;
            e.Cancel = true;
            HideVmDrawer();
        };
        _vmDrawer.HerScreenHeaderButton.Click += HerScreenHeader_Click;
        _vmDrawer.SightHeaderButton.Click += SightHeader_Click;
        _vmDrawer.VmPowerButton.Click += VmPowerButton_Click;
        _vmDrawer.DesktopViewFolderButton.Click += DesktopViewOpenFolder_Click;
        _vmDrawer.VictoriaBrowserSurface.PointerMoved += VictoriaBrowserSurface_PointerMoved;
        _vmDrawer.VictoriaBrowserSurface.PointerExited += VictoriaBrowserSurface_PointerExited;
        _vmDrawer.VictoriaBrowserSurface.PointerPressed += VictoriaBrowserSurface_PointerPressed;
        _vmDrawer.DesktopViewSurface.SizeChanged += DesktopViewSurface_SizeChanged;

        _performanceLog.Changed += (_, _) => Dispatcher.UIThread.Post(RefreshLogScreenIfVisible);

        _orbTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _orbTimer.Tick += (_, _) => TickOrbAnimation();

        ApplyVmDrawerSide();
        InitSettingsScreens();
        InitOperatorScreens();
        RefreshStatusOrb();
    }

    private void DisposeGlassShell()
    {
        _glassShuttingDown = true;
        _orbTimer?.Stop();
        try { _radial?.Close(); } catch { /* ignore */ }
        try { _vmDrawer.Close(); } catch { /* ignore */ }
        DisposeScreens();
        DisposeOperatorClients();
    }

    // ── Status orb (PROP-22.5) ───────────────────────────────────────────────────────────────────

    private StatusFacts BuildStatusFacts()
    {
        var h = _lastHealth;
        var enabled = _emailAccounts.Count(a => a.Enabled);
        var configured = _emailAccounts.Count(a => a.Enabled && a.IsConfigured);
        return new StatusFacts
        {
            HostUp = h.Alive,
            HostDetail = h.Detail,
            WsConnected = _ws.State == WsConnectionState.Connected,
            WsDetail = string.IsNullOrWhiteSpace(_ws.LastError) ? _ws.State.ToString() : _ws.LastError,
            InferenceEnabled = h.InferenceEnabled,
            InferenceCloud = h.InferenceCloud,
            OllamaUp = _ollamaUp,
            InferenceModel = h.InferenceModel,
            UnrealEnabled = h.UnrealEnabled,
            UnrealConnected = h.UnrealConnected,
            ComfyUp = _comfyUp,
            VmRunning = _sandboxRunning == true,
            VoiceEnabled = _voiceEnabled,
            VoiceSttOk = _voiceSttOk,
            VoiceTtsOk = _voiceTtsOk,
            MailboxesEnabled = enabled,
            MailboxesConfigured = configured,
            CurrentActivity = _presenceCurrentActivity,
            CallActive = _callActive || _callPhase != CallPhase.None,
            TypingActive = _typingActive
        };
    }

    /// <summary>Recompute the orb from the latest real probes. Safe to call from any UI-thread path.</summary>
    private void RefreshStatusOrb()
    {
        if (StatusOrbLight is null)
            return;

        var facts = BuildStatusFacts();
        var services = SystemStatusAggregator.BuildServices(facts);
        _orbServices = services;

        // Until the first /health answer comes back we know nothing — stay dim instead of flashing red.
        var probed = _initialHealth is not null && !ReferenceEquals(_lastHealth, _initialHealth);
        OrbState? next = probed
            ? SystemStatusAggregator.ComputeOrb(services, SystemStatusAggregator.IsBusy(facts))
            : null;

        if (next != _orbState)
            _orbAnimStartUtc = DateTime.UtcNow;
        _orbState = next;

        StatusOrbLight.Fill = next is { } s ? RadialStatusWindow.OrbBrush(s) : Res("OffDimBrush");
        ToolTip.SetTip(StatusOrbButton, next is { } d ? SystemStatusAggregator.Describe(d) : "Checking…");

        var animated = next is OrbState.FastRedFlash or OrbState.PinkPulse;
        if (animated && _orbTimer is { IsEnabled: false })
            _orbTimer.Start();
        else if (!animated && _orbTimer is { IsEnabled: true })
        {
            _orbTimer.Stop();
            StatusOrbLight.Opacity = 1.0;
        }

        if (_radial is { IsVisible: true } && next is { } shown)
            _radial.SetStatus(shown, services);
    }

    private void TickOrbAnimation()
    {
        if (StatusOrbLight is null || _orbState is not { } state)
            return;

        var t = (DateTime.UtcNow - _orbAnimStartUtc).TotalSeconds;
        switch (state)
        {
            case OrbState.FastRedFlash:
                // ~3 Hz hard flash.
                StatusOrbLight.Opacity = ((int)(t * 6) % 2) == 0 ? 1.0 : 0.12;
                break;
            case OrbState.PinkPulse:
                // Slow ~2.4 s breathing pulse.
                StatusOrbLight.Opacity = 0.35 + 0.65 * (0.5 + 0.5 * Math.Sin(t * 2 * Math.PI / 2.4));
                break;
            case OrbState.Green:
            case OrbState.Yellow:
            case OrbState.Amber:
            case OrbState.Red:
                StatusOrbLight.Opacity = 1.0;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    private void StatusOrb_Click(object? sender, RoutedEventArgs e)
    {
        if (_radial is { IsVisible: true })
        {
            _radial.Close();
            return;
        }

        // The click that just deactivated (closed) the popup is the same click that landed here.
        if (RadialStatusWindow.ClosedRecently())
            return;

        RefreshStatusOrb();
        var radial = new RadialStatusWindow();
        radial.Closed += (_, _) =>
        {
            if (ReferenceEquals(_radial, radial))
                _radial = null;
        };
        _radial = radial;
        radial.SetStatus(_orbState ?? OrbState.Green, _orbServices);
        radial.PositionOutside(this);
        radial.Show(this);
        radial.PositionOutside(this);
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_radial, radial))
                radial.PositionOutside(this);
        }, DispatcherPriority.Loaded);
    }

    private void RepositionRadial()
    {
        if (_radial is { IsVisible: true } r)
            r.PositionOutside(this);
    }

    // ── VM drawer (PROP-22.6) ────────────────────────────────────────────────────────────────────

    private void ApplyVmDrawerSide()
    {
        var left = _uiSettings.VmDrawerOnLeft();
        VmDrawerTab.HorizontalAlignment = left
            ? Avalonia.Layout.HorizontalAlignment.Left
            : Avalonia.Layout.HorizontalAlignment.Right;
        VmDrawerTab.Margin = left ? new Thickness(5, 0, 0, 0) : new Thickness(0, 0, 5, 0);
        UpdateVmDrawerTabIcon();
        RepositionVmDrawer();
    }

    private void UpdateVmDrawerTabIcon()
    {
        // The arrow points where the drawer will go (closed) or where it will retreat to (open).
        var left = _uiSettings.VmDrawerOnLeft();
        var open = _vmDrawer.IsVisible;
        var pointsLeft = left != open;
        var key = pointsLeft ? "IconChevronLeft" : "IconChevronRight";
        if (this.TryFindResource(key, out var geo) && geo is Geometry g)
            VmDrawerTabIcon.Data = g;
        SetClass(VmDrawerTab, "open", open);
    }

    private void VmDrawerTab_Click(object? sender, RoutedEventArgs e)
    {
        if (_vmDrawer.IsVisible)
            HideVmDrawer();
        else
            ShowVmDrawer();
    }

    private void ShowVmDrawer()
    {
        if (_vmDrawer.IsVisible)
            return;

        _vmDrawer.DockTo(this, _uiSettings.VmDrawerOnLeft());
        _vmDrawer.Show(this);
        Dispatcher.UIThread.Post(RepositionVmDrawer, DispatcherPriority.Loaded);

        ApplyVmPowerButton(_sandboxRunning == true, _vmPowerBusy);
        ShowPresenceSide(herScreen: true);
        UpdateVmDrawerTabIcon();
    }

    private void HideVmDrawer()
    {
        if (!_vmDrawer.IsVisible)
            return;

        // Same clear pattern the old tab switch used: detach the VirtualBox HWND before the slot goes.
        ClearVictoriaBrowserEmbed(null);
        _vmDrawer.Hide();
        UpdateVmDrawerTabIcon();
    }

    private void RepositionVmDrawer()
    {
        if (_vmDrawer.IsVisible)
            _vmDrawer.DockTo(this, _uiSettings.VmDrawerOnLeft());
    }

    // ── Call overlay + PTT lamp (PROP-22.4) ──────────────────────────────────────────────────────

    private void SetCallPhase(CallPhase phase)
    {
        _callPhase = phase;
        if (CallOverlay is null)
            return;

        switch (phase)
        {
            case CallPhase.None:
                CallOverlay.IsVisible = false;
                break;
            case CallPhase.Connecting:
                // Full glass while connecting.
                Grid.SetRow(CallOverlay, 0);
                Grid.SetRowSpan(CallOverlay, 3);
                CallOverlay.IsVisible = true;
                break;
            case CallPhase.Connected:
                // Top third once the call is up; the transcript stays usable below.
                Grid.SetRow(CallOverlay, 0);
                Grid.SetRowSpan(CallOverlay, 1);
                CallOverlay.IsVisible = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
        }

        SetClass(CallButton, "live", phase != CallPhase.None);
        RefreshStatusOrb();
    }

    private void SetPttLive(bool live) => SetClass(PttButton, "live", live);

    private static void SetClass(StyledElement element, string name, bool on)
    {
        if (on)
        {
            if (!element.Classes.Contains(name))
                element.Classes.Add(name);
        }
        else
        {
            element.Classes.Remove(name);
        }
    }

    // ── Performance log ──────────────────────────────────────────────────────────────────────────

    private void RefreshLogScreenIfVisible()
    {
        if (_screen == Screen.Log && _logKind == LogKind.Performance)
            RenderPerformanceLog();
    }
}
