using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using House.ChatDesktop.Controls;
using House.ChatDesktop.Services;

namespace House.ChatDesktop;

public partial class MainWindow
{
    private int _browserImagePixelWidth;
    private int _browserImagePixelHeight;
    private string? _lastHoverClickHint;
    private long _lastEmbedHwnd;
    private string? _lastEmbedMode;
    private VictoriaBrowserEmbedHost? _victoriaBrowserEmbedHost;
    private int? _browserCursorX;
    private int? _browserCursorY;
    private int? _hoverGuestX;
    private int? _hoverGuestY;
    private string? _browserCursorState;
    private DateTimeOffset? _browserCursorAt;
    private bool _browserCursorLayerHooked;
    private VictoriaHerScreenOverlayWindow? _herScreenOverlay;
    private HerScreenHwndOverlay? _herScreenHwndOverlay;

    // PROP-14.1 palette — match PlaywrightClickCursor IdleHex / ClickHex.
    private static readonly IBrush SoftCursorIdleStroke = new SolidColorBrush(Color.Parse("#FF2D55"));
    private static readonly IBrush SoftCursorIdleFill = new SolidColorBrush(Color.Parse("#73FF2D55"));
    private static readonly IBrush SoftCursorClickStroke = new SolidColorBrush(Color.Parse("#2EC4B6"));
    private static readonly IBrush SoftCursorClickFill = new SolidColorBrush(Color.Parse("#802EC4B6"));
    private const int SoftCursorFlashMs = 650; // match Playwright AimDwell so ~5fps polls catch teal

    private async Task RefreshVictoriaBrowserViewAsync()
    {
        if (_browserViewBusy) return;
        if (PresenceView is { IsVisible: false }) return;
        // Skip Host poll while What she saw tab is selected (HWND embed stays attached).
        if (PresenceSideTabs is { SelectedItem: TabItem tab } && !ReferenceEquals(tab, HerScreenTab))
            return;

        _browserViewBusy = true;
        try
        {
            var snap = await _browserView.GetAsync(includeImage: true).ConfigureAwait(true);
            BrowserEmbedSnapshot? embed = null;
            if (snap.EmbedPane && OperatingSystem.IsWindows())
                embed = await _browserView.GetEmbedAsync().ConfigureAwait(true);
            ApplyVictoriaBrowserView(snap, embed);
        }
        finally
        {
            _browserViewBusy = false;
        }
    }

    private void PresenceSideTabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PresenceSideTabs?.SelectedItem is TabItem tab && ReferenceEquals(tab, HerScreenTab))
        {
            // Re-measure embed after the tab becomes visible again.
            _ = RefreshVictoriaBrowserViewAsync();
            _victoriaBrowserEmbedHost?.SyncSizeToSlot();
            PositionVictoriaBrowserSoftCursor();
            UpdateEmbedHoverCoordsFromSystemCursor();
            return;
        }

        // Overlay is a separate HWND — hide when Her screen tab is not showing.
        HideHerScreenOverlay();
    }

    private void ApplyVictoriaBrowserView(BrowserViewSnapshot snap, BrowserEmbedSnapshot? embed = null)
    {
        if (VictoriaBrowserActionText is null) return;

        EnsureBrowserCursorLayerHooked();

        if (!snap.Reachable)
        {
            VictoriaBrowserActionText.Text = snap.Detail ?? "Host unreachable";
            if (VictoriaBrowserUrlText is not null) VictoriaBrowserUrlText.Text = "—";
            if (VictoriaBrowserTitleText is not null) VictoriaBrowserTitleText.Text = "";
            if (VictoriaBrowserWaitingText is not null)
            {
                VictoriaBrowserWaitingText.Text = "";
                VictoriaBrowserWaitingText.IsVisible = false;
            }

            ClearVictoriaBrowserImage();
            ClearVictoriaBrowserEmbed("Host unreachable");
            ClearVictoriaBrowserSoftCursor();
            return;
        }

        if (VictoriaBrowserUrlText is not null)
            VictoriaBrowserUrlText.Text = string.IsNullOrWhiteSpace(snap.Url) ? "—" : snap.Url!;
        if (VictoriaBrowserTitleText is not null)
            VictoriaBrowserTitleText.Text = snap.Title ?? "";

        var when = snap.UpdatedAt?.ToLocalTime().ToString("h:mm:ss tt") ?? "-";
        var embedded = embed is { Mode: "embedded", Hwnd: > 0 } && OperatingSystem.IsWindows();
        var modeLabel = embedded
            ? (string.IsNullOrWhiteSpace(embed!.Surface) || embed.Surface == "none"
                ? "embedded"
                : $"embedded · {embed.Surface}")
            : embed?.Mode is { Length: > 0 } m && m != "disabled"
                ? m
                : (snap.Backend ?? "playwright");

        VictoriaBrowserActionText.Text = string.IsNullOrWhiteSpace(snap.LastAction)
            ? $"No frame yet · {modeLabel} · {when}"
            : $"{snap.LastAction} · {modeLabel} · {when}";

        if (VictoriaBrowserWaitingText is not null)
        {
            var waiting = snap.WaitingOnYou?.Trim();
            var embedNote = embed?.Mode is "fallback" or "capture_off"
                ? embed.Detail
                : null;
            if (!string.IsNullOrWhiteSpace(waiting) || !string.IsNullOrWhiteSpace(embedNote))
            {
                VictoriaBrowserWaitingText.Text = !string.IsNullOrWhiteSpace(waiting)
                    ? "Waiting on you: " + waiting
                    : "Embed: " + embedNote;
                VictoriaBrowserWaitingText.IsVisible = true;
            }
            else
            {
                VictoriaBrowserWaitingText.Text = "";
                VictoriaBrowserWaitingText.IsVisible = false;
            }
        }

        // Frame size for soft-cursor mapping (guest framebuffer or last JPEG).
        if (snap.FrameWidth > 0) _browserImagePixelWidth = snap.FrameWidth;
        if (snap.FrameHeight > 0) _browserImagePixelHeight = snap.FrameHeight;

        if (embedded)
        {
            ApplyVictoriaBrowserEmbed(embed!);
            // Hide JPEG while HWND is live; hover coords come from system-cursor poll
            // (Avalonia PointerMoved never fires over the SetParent child).
            if (VictoriaBrowserImage is not null)
                VictoriaBrowserImage.IsVisible = false;
            if (VictoriaBrowserEmptyText is not null)
                VictoriaBrowserEmptyText.IsVisible = false;
            EnsureGuestFrameSizeForEmbed();
            ApplyVictoriaBrowserSoftCursor(snap, embed);
            UpdateEmbedHoverCoordsFromSystemCursor();
            return;
        }

        // After SetParent the VM HWND is no longer top-level. Host may briefly report
        // fallback/hwnd=0 if cache is cold — do not tear down a live embed (that made
        // the VirtualBox window flash on the desktop then vanish).
        if (_victoriaBrowserEmbedHost is { NativeHostUnavailable: false }
            && _lastEmbedHwnd > 0
            && embed is { Mode: "fallback", Hwnd: <= 0 }
            && string.Equals(embed.Surface, "vm", StringComparison.OrdinalIgnoreCase))
        {
            if (VictoriaBrowserWaitingText is not null)
            {
                VictoriaBrowserWaitingText.Text = "Embedded VirtualBox (holding HWND while Host re-resolves)...";
                VictoriaBrowserWaitingText.IsVisible = true;
            }

            EnsureGuestFrameSizeForEmbed();
            ApplyVictoriaBrowserSoftCursor(snap, embed);
            UpdateEmbedHoverCoordsFromSystemCursor();
            return;
        }

        ClearVictoriaBrowserEmbed(embed?.Detail);
        if (snap.ImageBytes is { Length: > 0 })
        {
            var hash = $"{snap.ImageBytes.Length}:{snap.UpdatedAt:O}:{snap.Url}";
            if (!string.Equals(hash, _lastBrowserImageHash, StringComparison.Ordinal))
            {
                _lastBrowserImageHash = hash;
                ShowVictoriaBrowserBitmap(snap.ImageBytes);
            }
        }
        else
        {
            ClearVictoriaBrowserImage();
            // Surface Host embed status in the big empty label (not only the tiny footer).
            if (VictoriaBrowserEmptyText is not null)
            {
                VictoriaBrowserEmptyText.Text = BuildHerScreenEmptyHint(snap, embed);
                VictoriaBrowserEmptyText.IsVisible = true;
            }
        }

        ApplyVictoriaBrowserSoftCursor(snap, embed);
    }

    private static string BuildHerScreenEmptyHint(BrowserViewSnapshot snap, BrowserEmbedSnapshot? embed)
    {
        if (embed is { Mode: "embedded", Hwnd: > 0 })
            return "Embedding…";
        if (embed is { Mode: "fallback" or "capture_off" or "disabled" }
            && !string.IsNullOrWhiteSpace(embed.Detail))
            return embed.Detail!;
        if (!string.IsNullOrWhiteSpace(snap.Detail))
            return snap.Detail!;
        if (snap.EmbedPane)
            return "Waiting for VirtualBox victoria-sandbox (visible, not minimized) — or a desktop_screenshot JPEG fallback.";
        return "Waiting for Victoria's browser — pink cursor idle, teal on click";
    }

    private void ApplyVictoriaBrowserEmbed(BrowserEmbedSnapshot embed)
    {
        if (embed.Hwnd == _lastEmbedHwnd
            && string.Equals(_lastEmbedMode, embed.Mode, StringComparison.Ordinal)
            && _victoriaBrowserEmbedHost is { NativeHostUnavailable: false })
        {
            // Same HWND — still sync size (splitter / window resize).
            _victoriaBrowserEmbedHost.SyncSizeToSlot();
            return;
        }

        _lastEmbedHwnd = embed.Hwnd;
        _lastEmbedMode = embed.Mode;

        try
        {
            // Recreate each bind so CreateNativeControlCore runs with the HWND already set.
            // NativeControlHost must not sit in the visual tree on cold start (CreateWindowEx).
            DestroyVictoriaBrowserEmbedHost();

            if (VictoriaBrowserEmbedSlot is null || !OperatingSystem.IsWindows() || embed.Hwnd <= 0)
                return;

            var host = new VictoriaBrowserEmbedHost
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                IsVisible = false
            };
            // Bind before Add so OnAttachedToVisualTree / CreateNativeControlCore see _hwnd.
            host.Bind((nint)embed.Hwnd);
            host.SoftCursorSiblingRaised += (_, _) =>
            {
                if (_herScreenHwndOverlay is { IsAttached: true })
                    _herScreenHwndOverlay.SyncToParent();
            };
            VictoriaBrowserEmbedSlot.Children.Add(host);
            VictoriaBrowserEmbedSlot.IsVisible = true;
            _victoriaBrowserEmbedHost = host;

            if (host.NativeHostUnavailable)
            {
                PresenceStartupLog.Write(
                    "VictoriaBrowserEmbedHost unavailable after attach — JPEG fallback");
                NoteEmbedUnavailable();
                DestroyVictoriaBrowserEmbedHost();
            }
        }
        catch (Exception ex)
        {
            PresenceStartupLog.WriteException("ApplyVictoriaBrowserEmbed", ex);
            NoteEmbedUnavailable();
            DestroyVictoriaBrowserEmbedHost();
        }
    }

    private void NoteEmbedUnavailable()
    {
        if (VictoriaBrowserWaitingText is null)
            return;
        if (!string.IsNullOrWhiteSpace(VictoriaBrowserWaitingText.Text))
            return;
        VictoriaBrowserWaitingText.Text = "Embed: native host unavailable — JPEG fallback";
        VictoriaBrowserWaitingText.IsVisible = true;
    }

    private void ClearVictoriaBrowserEmbed(string? detail)
    {
        _lastEmbedHwnd = 0;
        _lastEmbedMode = detail;
        DestroyVictoriaBrowserEmbedHost();
        HideHerScreenOverlay();
    }

    private void DestroyVictoriaBrowserEmbedHost()
    {
        try
        {
            _victoriaBrowserEmbedHost?.Bind(0);
        }
        catch
        {
            // ignore
        }

        try
        {
            VictoriaBrowserEmbedSlot?.Children.Clear();
        }
        catch
        {
            // ignore
        }

        _victoriaBrowserEmbedHost = null;
        if (VictoriaBrowserEmbedSlot is not null)
            VictoriaBrowserEmbedSlot.IsVisible = false;
    }

    /// <summary>
    /// Prefer Win32 sibling overlay (above VirtualBox GPU). Fall back to owned Avalonia window.
    /// </summary>
    private void EnsureHerScreenOverlay()
    {
        if (!OperatingSystem.IsWindows() || !IsVictoriaVmEmbedLive())
        {
            HideHerScreenOverlay();
            return;
        }

        try
        {
            var parent = _victoriaBrowserEmbedHost?.EmbedParentHwnd ?? 0;
            if (parent != 0)
            {
                _herScreenHwndOverlay ??= new HerScreenHwndOverlay();
                _herScreenHwndOverlay.Attach(parent);
                // Avalonia popup is redundant when HWND sibling works.
                if (_herScreenOverlay is { IsVisible: true })
                    _herScreenOverlay.Hide();
                return;
            }

            _herScreenOverlay ??= new VictoriaHerScreenOverlayWindow { Topmost = true };
            if (!_herScreenOverlay.IsVisible)
                _herScreenOverlay.Show(this);
        }
        catch (Exception ex)
        {
            PresenceStartupLog.WriteException("EnsureHerScreenOverlay", ex);
        }
    }

    private void HideHerScreenOverlay()
    {
        try
        {
            _herScreenHwndOverlay?.Hide();
        }
        catch
        {
            // ignore
        }

        try
        {
            if (_herScreenOverlay is { IsVisible: true })
                _herScreenOverlay.Hide();
        }
        catch
        {
            // ignore
        }
    }

    private void CloseHerScreenOverlay()
    {
        try
        {
            _herScreenHwndOverlay?.Dispose();
        }
        catch
        {
            // ignore
        }

        _herScreenHwndOverlay = null;

        try
        {
            _herScreenOverlay?.Close();
        }
        catch
        {
            // ignore
        }

        _herScreenOverlay = null;
    }

    private void ApplyVictoriaBrowserSoftCursor(BrowserViewSnapshot snap, BrowserEmbedSnapshot? embed)
    {
        // Playwright DOM + burn-in already paint the cursor — overlay only for VM / guest.
        var surface = embed?.Surface ?? snap.EmbedSurface;
        var wants = string.Equals(surface, "vm", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(snap.Backend, "vbox-guest", StringComparison.OrdinalIgnoreCase);
        if (!wants)
        {
            ClearVictoriaBrowserSoftCursor();
            return;
        }

        // Prefer Host cursor; if she hasn't aimed yet, seed center so pink is visible on embed.
        EnsureGuestFrameSizeForEmbed();
        var cx = snap.CursorX ?? (_browserImagePixelWidth > 0 ? _browserImagePixelWidth / 2 : null);
        var cy = snap.CursorY ?? (_browserImagePixelHeight > 0 ? _browserImagePixelHeight / 2 : null);
        if (cx is null || cy is null)
        {
            ClearVictoriaBrowserSoftCursor();
            return;
        }

        _browserCursorX = cx;
        _browserCursorY = cy;
        _browserCursorState = snap.CursorState;
        _browserCursorAt = snap.CursorAt;
        PositionVictoriaBrowserSoftCursor();
        // Option C: her coords always on the badge (hover filled in by poll / PointerMoved).
        RefreshVictoriaBrowserCoordBadge();
    }

    private void ClearVictoriaBrowserSoftCursor()
    {
        _browserCursorX = null;
        _browserCursorY = null;
        _browserCursorState = null;
        _browserCursorAt = null;
        if (VictoriaBrowserCursorLayer is not null)
            VictoriaBrowserCursorLayer.IsVisible = false;
        _herScreenOverlay?.SetSoftCursor(false, 0, 0, 28, SoftCursorIdleStroke, SoftCursorIdleFill);
        _herScreenHwndOverlay?.SetSoftCursor(false, 0, 0, 28, false);
        RefreshVictoriaBrowserCoordBadge();
    }

    private void EnsureBrowserCursorLayerHooked()
    {
        if (_browserCursorLayerHooked || VictoriaBrowserSurface is null)
            return;
        VictoriaBrowserSurface.SizeChanged += (_, _) =>
        {
            _victoriaBrowserEmbedHost?.SyncSizeToSlot();
            PositionVictoriaBrowserSoftCursor();
            UpdateEmbedHoverCoordsFromSystemCursor();
        };
        _browserCursorLayerHooked = true;
    }

    private bool IsVictoriaVmEmbedLive() =>
        _victoriaBrowserEmbedHost is { NativeHostUnavailable: false } && _lastEmbedHwnd > 0;

    private void EnsureGuestFrameSizeForEmbed()
    {
        if (_browserImagePixelWidth > 0 && _browserImagePixelHeight > 0)
            return;
        _browserImagePixelWidth = VictoriaBrowserCoordMap.DefaultGuestWidth;
        _browserImagePixelHeight = VictoriaBrowserCoordMap.DefaultGuestHeight;
    }

    private void PositionVictoriaBrowserSoftCursor()
    {
        var surface = VictoriaBrowserSurface;
        var layer = VictoriaBrowserCursorLayer;
        var cursor = VictoriaBrowserCursor;
        if (surface is null || layer is null || cursor is null)
            return;

        if (_browserCursorX is not int cx || _browserCursorY is not int cy
            || _browserImagePixelWidth <= 0 || _browserImagePixelHeight <= 0)
        {
            layer.IsVisible = false;
            _herScreenOverlay?.SetSoftCursor(false, 0, 0, 28, SoftCursorIdleStroke, SoftCursorIdleFill);
            return;
        }

        var bounds = surface.Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            layer.IsVisible = false;
            _herScreenOverlay?.SetSoftCursor(false, 0, 0, 28, SoftCursorIdleStroke, SoftCursorIdleFill);
            return;
        }

        var flash = string.Equals(_browserCursorState, "click", StringComparison.OrdinalIgnoreCase)
                    && _browserCursorAt is DateTimeOffset at
                    && (DateTimeOffset.UtcNow - at).TotalMilliseconds < SoftCursorFlashMs;

        var stroke = flash ? SoftCursorClickStroke : SoftCursorIdleStroke;
        var fill = flash ? SoftCursorClickFill : SoftCursorIdleFill;
        var size = flash ? 36.0 : 28.0;

        // HWND embed fills the slot (stretch); JPEG uses Uniform letterbox.
        var mapped = IsVictoriaVmEmbedLive()
            ? VictoriaBrowserCoordMap.TryMapGuestToSurfaceStretchFill(
                cx, cy, bounds.Width, bounds.Height,
                _browserImagePixelWidth, _browserImagePixelHeight,
                size, size)
            : VictoriaBrowserCoordMap.TryMapGuestToSurfaceUniform(
                cx, cy, bounds.Width, bounds.Height,
                _browserImagePixelWidth, _browserImagePixelHeight,
                size, size);

        if (mapped is null)
        {
            layer.IsVisible = false;
            _herScreenOverlay?.SetSoftCursor(false, 0, 0, size, stroke, fill);
            return;
        }

        // Live VM HWND covers in-tree Avalonia — paint on Win32 sibling (or Avalonia fallback).
        if (IsVictoriaVmEmbedLive())
        {
            layer.IsVisible = false;
            EnsureHerScreenOverlay();
            var scaling = TopLevel.GetTopLevel(surface)?.RenderScaling ?? 1.0;
            var centerX = (int)Math.Round((mapped.Value.Left + size / 2) * scaling);
            var centerY = (int)Math.Round((mapped.Value.Top + size / 2) * scaling);
            var sizePx = (int)Math.Round(size * scaling);
            if (_herScreenHwndOverlay is { IsAttached: true })
            {
                _herScreenHwndOverlay.SyncToParent();
                _herScreenHwndOverlay.SetSoftCursor(true, centerX, centerY, sizePx, flash);
            }
            else
            {
                _herScreenOverlay?.SyncToSurface(surface);
                _herScreenOverlay?.SetSoftCursor(true, mapped.Value.Left, mapped.Value.Top, size, stroke, fill);
            }

            return;
        }

        HideHerScreenOverlay();
        cursor.Stroke = stroke;
        cursor.Fill = fill;
        cursor.Width = size;
        cursor.Height = size;
        Canvas.SetLeft(cursor, mapped.Value.Left);
        Canvas.SetTop(cursor, mapped.Value.Top);
        layer.Width = bounds.Width;
        layer.Height = bounds.Height;
        layer.IsVisible = true;
    }

    /// <summary>
    /// Option C: poll operator mouse over the VM HWND (Win32 — no Avalonia steal) and refresh
    /// the badge with her agent coords (always) + yours while hovering.
    /// </summary>
    private void UpdateEmbedHoverCoordsFromSystemCursor()
    {
        if (!IsVictoriaVmEmbedLive() || VictoriaBrowserSurface is null)
        {
            _hoverGuestX = null;
            _hoverGuestY = null;
            RefreshVictoriaBrowserCoordBadge();
            return;
        }

        EnsureGuestFrameSizeForEmbed();
        var local = HerScreenCursorProbe.TryGetPointerInSurface(VictoriaBrowserSurface);
        if (local is null)
        {
            _hoverGuestX = null;
            _hoverGuestY = null;
            RefreshVictoriaBrowserCoordBadge();
            return;
        }

        var mapped = VictoriaBrowserCoordMap.TryMapPointerStretchFill(
            local.Value.X,
            local.Value.Y,
            VictoriaBrowserSurface.Bounds.Width,
            VictoriaBrowserSurface.Bounds.Height,
            _browserImagePixelWidth,
            _browserImagePixelHeight);

        if (mapped is null)
        {
            _hoverGuestX = null;
            _hoverGuestY = null;
            RefreshVictoriaBrowserCoordBadge();
            return;
        }

        _hoverGuestX = mapped.Value.X;
        _hoverGuestY = mapped.Value.Y;
        _lastHoverClickHint = VictoriaBrowserCoordMap.FormatClickHint(mapped.Value.X, mapped.Value.Y);
        RefreshVictoriaBrowserCoordBadge();
    }

    private void ShowVictoriaBrowserBitmap(byte[] imageBytes)
    {
        try
        {
            using var ms = new System.IO.MemoryStream(imageBytes);
            var bmp = new Bitmap(ms);
            _browserImagePixelWidth = bmp.PixelSize.Width;
            _browserImagePixelHeight = bmp.PixelSize.Height;
            if (VictoriaBrowserImage is not null)
            {
                VictoriaBrowserImage.Source = bmp;
                VictoriaBrowserImage.IsVisible = true;
            }

            if (VictoriaBrowserEmptyText is not null)
                VictoriaBrowserEmptyText.IsVisible = false;
        }
        catch (Exception ex)
        {
            if (VictoriaBrowserActionText is not null)
                VictoriaBrowserActionText.Text = $"Image decode failed: {ex.Message}";
        }
    }

    private void ClearVictoriaBrowserImage()
    {
        if (VictoriaBrowserImage is not null)
        {
            VictoriaBrowserImage.Source = null;
            VictoriaBrowserImage.IsVisible = false;
        }

        if (VictoriaBrowserEmptyText is not null)
            VictoriaBrowserEmptyText.IsVisible = true;
        _lastBrowserImageHash = null;
        // Keep guest frame size while VM HWND embed is live (soft cursor + hover map).
        if (!IsVictoriaVmEmbedLive())
        {
            _browserImagePixelWidth = 0;
            _browserImagePixelHeight = 0;
            HideVictoriaBrowserCoords();
        }
    }

    private void VictoriaBrowserSurface_PointerMoved(object? sender, PointerEventArgs e)
    {
        // VM embed: HWND steals pointer — coords updated via UpdateEmbedHoverCoordsFromSystemCursor.
        if (IsVictoriaVmEmbedLive())
            return;

        if (VictoriaBrowserSurface is null || VictoriaBrowserImage is null || !VictoriaBrowserImage.IsVisible)
        {
            _hoverGuestX = null;
            _hoverGuestY = null;
            RefreshVictoriaBrowserCoordBadge();
            return;
        }

        var pos = e.GetPosition(VictoriaBrowserSurface);
        var mapped = VictoriaBrowserCoordMap.TryMapPointerToPage(
            pos.X,
            pos.Y,
            VictoriaBrowserSurface.Bounds.Width,
            VictoriaBrowserSurface.Bounds.Height,
            _browserImagePixelWidth,
            _browserImagePixelHeight);

        if (mapped is null)
        {
            _hoverGuestX = null;
            _hoverGuestY = null;
            RefreshVictoriaBrowserCoordBadge();
            return;
        }

        _hoverGuestX = mapped.Value.X;
        _hoverGuestY = mapped.Value.Y;
        _lastHoverClickHint = VictoriaBrowserCoordMap.FormatClickHint(mapped.Value.X, mapped.Value.Y);
        RefreshVictoriaBrowserCoordBadge();
    }

    private void VictoriaBrowserSurface_PointerExited(object? sender, PointerEventArgs e)
    {
        _hoverGuestX = null;
        _hoverGuestY = null;
        _lastHoverClickHint = null;
        RefreshVictoriaBrowserCoordBadge();
    }

    private async void VictoriaBrowserSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Prefer operator hover hint for clipboard; else her agent coords.
        var text = _lastHoverClickHint
                   ?? (_browserCursorX is int hx && _browserCursorY is int hy
                       ? VictoriaBrowserCoordMap.FormatClickHint(hx, hy)
                       : null);
        if (string.IsNullOrWhiteSpace(text))
            return;
        if (!e.GetCurrentPoint(VictoriaBrowserSurface).Properties.IsLeftButtonPressed)
            return;

        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
                await clipboard.SetTextAsync(text).ConfigureAwait(true);

            if (VictoriaBrowserActionText is not null)
                VictoriaBrowserActionText.Text = $"Copied — tell her: {text}";
        }
        catch
        {
            if (VictoriaBrowserActionText is not null)
                VictoriaBrowserActionText.Text = $"Tell her: {text}";
        }
    }

    /// <summary>
    /// Option C badge: her agent soft-cursor guest coords always; operator hover while present.
    /// </summary>
    private void RefreshVictoriaBrowserCoordBadge()
    {
        var text = VictoriaBrowserCoordMap.FormatCoordBadge(
            _browserCursorX, _browserCursorY, _hoverGuestX, _hoverGuestY);

        if (IsVictoriaVmEmbedLive())
        {
            // Badge under HWND is invisible — drive the click-through overlay.
            if (VictoriaBrowserCoordBadge is not null)
                VictoriaBrowserCoordBadge.IsVisible = false;
            EnsureHerScreenOverlay();
            if (_herScreenHwndOverlay is { IsAttached: true })
            {
                _herScreenHwndOverlay.SyncToParent();
                _herScreenHwndOverlay.SetBadge(text);
            }
            else
            {
                if (VictoriaBrowserSurface is not null)
                    _herScreenOverlay?.SyncToSurface(VictoriaBrowserSurface);
                _herScreenOverlay?.SetBadge(text);
            }

            return;
        }

        HideHerScreenOverlay();
        if (text is null)
        {
            if (VictoriaBrowserCoordBadge is not null)
                VictoriaBrowserCoordBadge.IsVisible = false;
            return;
        }

        if (VictoriaBrowserCoordText is not null)
            VictoriaBrowserCoordText.Text = text;
        if (VictoriaBrowserCoordBadge is not null)
            VictoriaBrowserCoordBadge.IsVisible = true;
    }

    private void HideVictoriaBrowserCoords()
    {
        _hoverGuestX = null;
        _hoverGuestY = null;
        _lastHoverClickHint = null;
        if (VictoriaBrowserCoordBadge is not null)
            VictoriaBrowserCoordBadge.IsVisible = false;
        _herScreenOverlay?.SetBadge(null);
    }
}
