using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Platform;

namespace House.ChatDesktop.Controls;

/// <summary>
/// Owned, click-through window that paints her soft cursor + coords badge above the
/// SetParent'd VirtualBox HWND (Avalonia siblings of NativeControlHost stay under it).
/// </summary>
public sealed class VictoriaHerScreenOverlayWindow : Window
{
    private readonly Canvas _cursorLayer;
    private readonly Ellipse _cursor;
    private readonly Border _badge;
    private readonly TextBlock _badgeText;
    private bool _clickThroughApplied;

    public VictoriaHerScreenOverlayWindow()
    {
        Title = "Her screen overlay";
        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        // Topmost fallback when Win32 sibling overlay cannot attach.
        Topmost = true;
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        TransparencyLevelHint = new[]
        {
            WindowTransparencyLevel.Transparent,
            WindowTransparencyLevel.None
        };
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaTitleBarHeightHint = -1;

        _cursor = new Ellipse
        {
            Width = 28,
            Height = 28,
            Stroke = new SolidColorBrush(Color.Parse("#FF2D55")),
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.Parse("#73FF2D55")),
            IsVisible = false
        };
        _cursorLayer = new Canvas { IsHitTestVisible = false };
        _cursorLayer.Children.Add(_cursor);

        _badgeText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#FFFF2D55")),
            Text = ""
        };
        _badge = new Border
        {
            IsVisible = false,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(8),
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.Parse("#CC0A0A0A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#FFFF2D55")),
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false,
            Child = _badgeText
        };

        Content = new Panel
        {
            IsHitTestVisible = false,
            Children = { _cursorLayer, _badge }
        };

        Opened += (_, _) => ApplyClickThrough();
    }

    public void SyncToSurface(Control surface)
    {
        if (!IsVisible || surface is null)
            return;

        var bounds = surface.Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1)
            return;

        PixelPoint topLeft;
        try
        {
            topLeft = surface.PointToScreen(new Point(0, 0));
        }
        catch
        {
            return;
        }

        Position = topLeft;
        Width = bounds.Width;
        Height = bounds.Height;
        _cursorLayer.Width = bounds.Width;
        _cursorLayer.Height = bounds.Height;
        ApplyClickThrough();
    }

    public void SetSoftCursor(
        bool visible,
        double left,
        double top,
        double size,
        IBrush stroke,
        IBrush fill)
    {
        _cursor.IsVisible = visible;
        if (!visible)
            return;

        _cursor.Width = size;
        _cursor.Height = size;
        _cursor.Stroke = stroke;
        _cursor.Fill = fill;
        Canvas.SetLeft(_cursor, left);
        Canvas.SetTop(_cursor, top);
    }

    public void SetBadge(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _badge.IsVisible = false;
            return;
        }

        _badgeText.Text = text;
        _badge.IsVisible = true;
    }

    private void ApplyClickThrough()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var handle = TryGetPlatformHandle()?.Handle ?? nint.Zero;
            if (handle == 0)
                return;

            // Re-apply after move/size — Avalonia may reset extended styles.
            var ex = GetWindowLong(handle, GWL_EXSTYLE);
            var next = HerScreenOverlayWin32.WithClickThrough(ex);
            if (next != ex || !_clickThroughApplied)
            {
                _ = SetWindowLong(handle, GWL_EXSTYLE, next);
                _clickThroughApplied = true;
            }
        }
        catch
        {
            // Overlay must never crash Presence.
        }
    }

    private const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}

/// <summary>Win32 extended-style bits for click-through Her-screen overlay.</summary>
public static class HerScreenOverlayWin32
{
    public const int WsExTransparent = 0x00000020;
    public const int WsExLayered = 0x00080000;
    public const int WsExNoActivate = 0x08000000;
    public const int WsExToolWindow = 0x00000080;

    public static int WithClickThrough(int exStyle) =>
        exStyle | WsExTransparent | WsExLayered | WsExNoActivate | WsExToolWindow;
}
