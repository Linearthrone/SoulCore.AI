using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using House.ChatDesktop.Services;

namespace House.ChatDesktop;

public partial class MainWindow
{
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
            DesktopViewMetaText.Text = "Start SoulCore.Host to see her last capture.";
            if (DesktopViewImage is not null)
            {
                DesktopViewImage.Source = null;
                DesktopViewImage.IsVisible = false;
            }

            if (DesktopViewEmptyText is not null)
                DesktopViewEmptyText.IsVisible = true;
            _lastDesktopImageHash = null;
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

        var sourceLabel = (snap.Source ?? "desktop").Trim().ToLowerInvariant() switch
        {
            "eyes" or "eye" => "Her eyes",
            "browser" => "Browser tab",
            _ => "Desktop"
        };

        DesktopViewActionText.Text = string.IsNullOrWhiteSpace(snap.LastAction)
            ? "Waiting for a real capture (eyes / desktop / browser)…"
            : $"{sourceLabel}: {snap.LastAction}";

        if (!string.IsNullOrWhiteSpace(snap.LastAction)
            && !snap.LastAction.Contains("Waiting", StringComparison.OrdinalIgnoreCase))
        {
            _lastChatActivityUtc = DateTimeOffset.UtcNow;
            if (string.IsNullOrWhiteSpace(_lastActivityPhrase)
                || _lastActivityPhrase.Equals("Idle", StringComparison.OrdinalIgnoreCase))
            {
                _lastActivityPhrase = sourceLabel switch
                {
                    "Her eyes" => "Looking through her eyes",
                    "Browser tab" => "Looking at a browser tab",
                    _ => "Using the desktop"
                };
            }

            UpdateEngagementState();
        }

        var when = snap.UpdatedAt?.ToLocalTime().ToString("h:mm:ss tt") ?? "—";
        var soft = snap.SoftCursorRestore ? "agent/background" : "foreground ok";
        DesktopViewMetaText.Text = snap.HasImage
            ? $"{sourceLabel} · {snap.Width}×{snap.Height} · {soft} · {when}"
            : $"No capture yet · {soft}";

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
            Stroke = new SolidColorBrush(Color.Parse("#C4B06A")),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.Parse("#55C4B06A"))
        };
        var layer = new Canvas { IsHitTestVisible = false };
        layer.Children.Add(cursor);
        var image = new Image { Stretch = Stretch.Uniform };
        if (DesktopViewImage?.Source is { } src)
            image.Source = src;

        var surface = new Grid { Background = new SolidColorBrush(Color.Parse("#12101C")) };
        surface.Children.Add(image);
        surface.Children.Add(layer);
        surface.SizeChanged += (_, _) => PositionDesktopCursor(surface, layer, cursor);

        _desktopPopOutImage = image;
        _desktopPopOutCursorLayer = layer;
        _desktopPopOutCursor = cursor;

        var topToggle = new CheckBox
        {
            Content = "Always on top",
            IsChecked = _screenTop.IsOn,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(topToggle, Dock.Top);
        root.Children.Add(topToggle);
        root.Children.Add(surface);

        var win = new Window
        {
            Title = "Victoria's screen",
            Width = 960,
            Height = 640,
            Background = Res("BgBrush"),
            Foreground = Res("TextBrush"),
            Content = root,
            Topmost = _screenTop.IsOn
        };
        topToggle.IsCheckedChanged += (_, _) =>
        {
            _screenTop.IsOn = topToggle.IsChecked == true;
            win.Topmost = _screenTop.IsOn;
            if (ScreenAlwaysOnTop is not null)
                ScreenAlwaysOnTop.IsChecked = _screenTop.IsOn;
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

    private void PanelPopOut_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        switch (tag)
        {
            case "hud":
                _popOuts.Toggle("hud", "Victoria — HUD", HudPanel, _hudDock, _hudTop);
                break;
            case "chat":
                _popOuts.Toggle("chat", "Messages", ChatPanel, _chatDock, _chatTop);
                break;
            case "services":
                _popOuts.Toggle("services", "Services", ServicesPanel, _servicesDock, _servicesTop);
                break;
        }
    }

    private void HudAlwaysOnTop_Changed(object? sender, RoutedEventArgs e)
    {
        _hudTop.IsOn = HudAlwaysOnTop?.IsChecked == true;
        if (_hudTop.IsOn) Topmost = true;
    }

    private void ChatAlwaysOnTop_Changed(object? sender, RoutedEventArgs e)
    {
        _chatTop.IsOn = ChatAlwaysOnTop?.IsChecked == true;
        if (_chatTop.IsOn) Topmost = true;
    }

    private void ServicesAlwaysOnTop_Changed(object? sender, RoutedEventArgs e)
    {
        _servicesTop.IsOn = ServicesAlwaysOnTop?.IsChecked == true;
        if (_servicesTop.IsOn) Topmost = true;
    }

    private void ScreenAlwaysOnTop_Changed(object? sender, RoutedEventArgs e)
    {
        _screenTop.IsOn = ScreenAlwaysOnTop?.IsChecked == true;
        if (_desktopPopOut is not null)
            _desktopPopOut.Topmost = _screenTop.IsOn;
        else if (_screenTop.IsOn)
            Topmost = true;
    }
}
