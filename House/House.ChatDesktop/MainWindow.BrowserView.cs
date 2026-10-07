using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
    private string? _browserCursorState;
    private DateTimeOffset? _browserCursorAt;
    private bool _browserCursorLayerHooked;

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
            // Hide JPEG while HWND is live — coord hover is for JPEG fallback only.
            if (VictoriaBrowserImage is not null)
                VictoriaBrowserImage.IsVisible = false;
            if (VictoriaBrowserEmptyText is not null)
                VictoriaBrowserEmptyText.IsVisible = false;
            HideVictoriaBrowserCoords();
            ApplyVictoriaBrowserSoftCursor(snap, embed);
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

            ApplyVictoriaBrowserSoftCursor(snap, embed);
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
            return;

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

    private void ApplyVictoriaBrowserSoftCursor(BrowserViewSnapshot snap, BrowserEmbedSnapshot? embed)
    {
        // Playwright DOM + burn-in already paint the cursor — overlay only for VM / guest.
        var surface = embed?.Surface ?? snap.EmbedSurface;
        var wants = string.Equals(surface, "vm", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(snap.Backend, "vbox-guest", StringComparison.OrdinalIgnoreCase);
        if (!wants || snap.CursorX is not int cx || snap.CursorY is not int cy)
        {
            ClearVictoriaBrowserSoftCursor();
            return;
        }

        _browserCursorX = cx;
        _browserCursorY = cy;
        _browserCursorState = snap.CursorState;
        _browserCursorAt = snap.CursorAt;
        PositionVictoriaBrowserSoftCursor();
    }

    private void ClearVictoriaBrowserSoftCursor()
    {
        _browserCursorX = null;
        _browserCursorY = null;
        _browserCursorState = null;
        _browserCursorAt = null;
        if (VictoriaBrowserCursorLayer is not null)
            VictoriaBrowserCursorLayer.IsVisible = false;
    }

    private void EnsureBrowserCursorLayerHooked()
    {
        if (_browserCursorLayerHooked || VictoriaBrowserSurface is null)
            return;
        VictoriaBrowserSurface.SizeChanged += (_, _) => PositionVictoriaBrowserSoftCursor();
        _browserCursorLayerHooked = true;
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
            return;
        }

        var bounds = surface.Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            layer.IsVisible = false;
            return;
        }

        // HWND embed fills the slot; JPEG uses Uniform letterbox — same Uniform map either way
        // when frame size is known (guest framebuffer).
        var scale = Math.Min(bounds.Width / _browserImagePixelWidth, bounds.Height / _browserImagePixelHeight);
        var drawW = _browserImagePixelWidth * scale;
        var drawH = _browserImagePixelHeight * scale;
        var offsetX = (bounds.Width - drawW) / 2;
        var offsetY = (bounds.Height - drawH) / 2;

        var flash = string.Equals(_browserCursorState, "click", StringComparison.OrdinalIgnoreCase)
                    && _browserCursorAt is DateTimeOffset at
                    && (DateTimeOffset.UtcNow - at).TotalMilliseconds < SoftCursorFlashMs;

        cursor.Stroke = flash ? SoftCursorClickStroke : SoftCursorIdleStroke;
        cursor.Fill = flash ? SoftCursorClickFill : SoftCursorIdleFill;
        cursor.Width = flash ? 36 : 28;
        cursor.Height = flash ? 36 : 28;

        var left = offsetX + (cx * scale) - (cursor.Width / 2);
        var top = offsetY + (cy * scale) - (cursor.Height / 2);
        Canvas.SetLeft(cursor, left);
        Canvas.SetTop(cursor, top);
        layer.Width = bounds.Width;
        layer.Height = bounds.Height;
        layer.IsVisible = true;
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
        _browserImagePixelWidth = 0;
        _browserImagePixelHeight = 0;
        HideVictoriaBrowserCoords();
    }

    private void VictoriaBrowserSurface_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (VictoriaBrowserSurface is null || VictoriaBrowserImage is null || !VictoriaBrowserImage.IsVisible)
        {
            HideVictoriaBrowserCoords();
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
            HideVictoriaBrowserCoords();
            return;
        }

        var (x, y) = mapped.Value;
        _lastHoverClickHint = VictoriaBrowserCoordMap.FormatClickHint(x, y);
        if (VictoriaBrowserCoordText is not null)
            VictoriaBrowserCoordText.Text = _lastHoverClickHint;
        if (VictoriaBrowserCoordBadge is not null)
            VictoriaBrowserCoordBadge.IsVisible = true;
    }

    private void VictoriaBrowserSurface_PointerExited(object? sender, PointerEventArgs e) =>
        HideVictoriaBrowserCoords();

    private async void VictoriaBrowserSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastHoverClickHint))
            return;
        if (!e.GetCurrentPoint(VictoriaBrowserSurface).Properties.IsLeftButtonPressed)
            return;

        var text = _lastHoverClickHint!;
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

    private void HideVictoriaBrowserCoords()
    {
        if (VictoriaBrowserCoordBadge is not null)
            VictoriaBrowserCoordBadge.IsVisible = false;
        _lastHoverClickHint = null;
    }
}
