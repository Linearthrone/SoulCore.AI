using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Drawers;

/// <summary>
/// VM view as a tab on the top edge of the work area.
/// Closed, only the tab shows. A click or a downward drag pulls the drawer down from that edge.
/// The tab, border, and header use the radial tab fill and stroke. The still stays opaque and unfrosted.
/// This drawer does not parent an HWND.
/// </summary>
public sealed class ViewportDrawer
{
    private readonly ViewportInputPolicy _policy;
    private readonly LocalUiSettings _settings;
    private readonly Window _tabWindow;
    private readonly Window _stillWindow;
    private readonly Border _chrome;
    private readonly Border _tabRow;
    private readonly StackPanel _header;
    private readonly Grid _pictureHost;
    private readonly Image _still;
    private readonly TextBlock _empty;
    private readonly TextBlock _status;
    private readonly TextBlock _clickNote;
    private readonly TextBlock _personaName;
    private readonly ToggleButton _clickthrough;
    private readonly Slider _viewOpacity;
    private int _frameW;
    private int _frameH;
    private bool _frameKnown;
    private int _lastSlotW = -1;
    private int _lastSlotH = -1;
    private bool _sync;
    private bool _open;
    private bool _hiding;
    private Point? _pressAt;
    private bool _pressMoved;

    public ViewportDrawer(ViewportInputPolicy policy, LocalUiSettings settings)
    {
        _policy = policy;
        _settings = settings;
        _policy.SetViewOpacity(settings.ViewOpacity);

        var mark = RadialTabChrome.CreateMarkBrush();
        var stroke = RadialTabChrome.CreateStrokeBrush();

        _clickthrough = new ToggleButton
        {
            Content = "Clickthrough",
            IsChecked = false,
            MinWidth = 110,
            Margin = new Thickness(8, 4, 8, 4),
            Foreground = mark
        };
        _clickthrough.Classes.Add("ghost");
        ToolTip.SetTip(_clickthrough, "Default off. Not applied to this still or the chat drawer.");

        _viewOpacity = new Slider
        {
            Minimum = ViewportInputPolicy.ViewOpacityFloor,
            Maximum = 1,
            Value = _policy.ViewOpacity,
            Width = 120,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(_viewOpacity, "Does not fade this still. The picture stays opaque.");

        var viewLabel = new TextBlock
        {
            Text = "View",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 6, 0),
            Foreground = mark
        };
        var hide = new Button
        {
            Content = "Hide",
            Margin = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = mark
        };
        hide.Classes.Add("ghost");

        var stripBody = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _clickthrough, viewLabel, _viewOpacity, hide }
        };

        _personaName = new TextBlock
        {
            Text = "Companion",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 4, 12, 0),
            Foreground = mark,
            FontWeight = FontWeight.SemiBold
        };
        var caption = new TextBlock
        {
            Text = ViewportStill.Label,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 2, 12, 4),
            Foreground = mark
        };
        _header = new StackPanel
        {
            IsVisible = false,
            Background = Brushes.Transparent,
            Children = { _personaName, caption, stripBody }
        };

        _still = new Image
        {
            Stretch = Stretch.Uniform,
            IsVisible = false,
            Opacity = ViewportStill.WindowOpacity,
            Margin = new Thickness(8, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _empty = new TextBlock
        {
            Text = "No still yet",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12),
            Foreground = new SolidColorBrush(Color.Parse("#8B8E97")),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _status = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 8, 12, 4),
            Foreground = new SolidColorBrush(Color.Parse("#E6E7EA")),
            IsVisible = false
        };
        _clickNote = new TextBlock
        {
            Text = "Clickthrough is not applied to this still.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 0, 12, 10),
            Foreground = new SolidColorBrush(Color.Parse("#8B8E97")),
            IsVisible = false
        };

        var picture = new Grid
        {
            Background = ViewportStill.CreateFillBrush()
        };
        picture.Children.Add(_still);
        picture.Children.Add(_empty);

        _pictureHost = new Grid
        {
            Background = ViewportStill.CreateFillBrush(),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        _pictureHost.Children.Add(picture);
        _pictureHost.Children.Add(_status);
        _pictureHost.Children.Add(_clickNote);
        Grid.SetRow(_status, 1);
        Grid.SetRow(_clickNote, 2);

        var tabLabel = new TextBlock
        {
            Text = "VM",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = mark,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold
        };
        _tabRow = new Border
        {
            Height = ViewportEdgeLayout.ClosedTabHeightDip,
            Background = Brushes.Transparent,
            Child = tabLabel,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(_tabRow, "VM view");

        var tabRoot = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        tabRoot.Children.Add(_tabRow);
        tabRoot.Children.Add(_header);
        Grid.SetRow(_header, 1);

        _chrome = new Border
        {
            Background = RadialTabChrome.CreateFillBrush(),
            BorderBrush = stroke,
            BorderThickness = new Thickness(RadialTabChrome.BorderDip),
            CornerRadius = new CornerRadius(0, 0, 10, 10),
            Child = tabRoot
        };

        _tabWindow = CreateTabWindow();
        _tabWindow.Content = _chrome;

        _stillWindow = CreateStillWindow(ForegroundChromeNames.ViewportTitle(_personaName.Text));
        _stillWindow.Content = new Border
        {
            Background = RadialTabChrome.CreateFillBrush(),
            BorderBrush = stroke,
            BorderThickness = new Thickness(RadialTabChrome.BorderDip),
            Child = _pictureHost
        };

        hide.Click += (_, _) => Hide();
        _clickthrough.IsCheckedChanged += (_, _) =>
        {
            if (_sync)
                return;
            // The still is in this window. Clickthrough stays off and is not applied here.
            if (_clickthrough.IsChecked == true)
            {
                _sync = true;
                _clickthrough.IsChecked = false;
                _sync = false;
            }

            _policy.SetClickthrough(false);
        };
        _viewOpacity.PropertyChanged += (_, e) =>
        {
            if (_sync || e.Property != RangeBase.ValueProperty)
                return;
            var value = _viewOpacity.Value;
            _settings.ViewOpacity = value;
            _settings.Save();
            _policy.SetViewOpacity(value);
            KeepStillOpaque();
        };
        picture.SizeChanged += (_, _) => OnSlotSizeChanged(picture);
        _tabWindow.Opened += (_, _) => Pin();
        _stillWindow.Opened += (_, _) => Pin();
        _tabWindow.Closing += OnClosing;
        _stillWindow.Closing += OnClosing;
        _tabRow.PointerPressed += OnTabPressed;
        _tabRow.PointerMoved += OnTabMoved;
        _tabRow.PointerReleased += OnTabReleased;
        _tabRow.PointerCaptureLost += (_, _) =>
        {
            _pressAt = null;
            _pressMoved = false;
        };
    }

    public string ForegroundPersonaName => _personaName.Text ?? "Companion";

    public void SetForegroundPersona(string? name)
    {
        var shown = string.IsNullOrWhiteSpace(name) ? "Companion" : name.Trim();
        _personaName.Text = shown;
        var title = ForegroundChromeNames.ViewportTitle(shown);
        _stillWindow.Title = title;
        _tabWindow.Title = title;
    }

    public void Show()
    {
        KeepStillOpaque();
        _tabWindow.Show();
        if (_open)
            _stillWindow.Show();
        Pin();
        ApplyStill();
    }

    public void Hide() => SetOpen(false);

    public void Pin()
    {
        var screen = _tabWindow.Screens?.ScreenFromPoint(_tabWindow.Position) ?? _tabWindow.Screens?.Primary;
        var area = screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        var scale = _tabWindow.RenderScaling <= 0 ? 1 : _tabWindow.RenderScaling;
        var place = ViewportEdgeLayout.Place(area.X, area.Y, area.Width, area.Height, _open, scale);

        _tabWindow.Position = new PixelPoint(place.TabX, place.TabY);
        _tabWindow.Width = Math.Max(1, place.TabWidthPx / scale);
        _tabWindow.Height = Math.Max(1, place.TabHeightPx / scale);
        _header.IsVisible = place.Open;
        _chrome.CornerRadius = place.Open ? new CornerRadius(0) : new CornerRadius(0, 0, 10, 10);

        if (!place.DrawerVisible)
        {
            HideStill();
            return;
        }

        _stillWindow.Position = new PixelPoint(place.DrawerX, place.DrawerY);
        _stillWindow.Width = Math.Max(1, place.DrawerWidthPx / scale);
        _stillWindow.Height = Math.Max(1, place.DrawerHeightPx / scale);
        if (!_stillWindow.IsVisible)
            _stillWindow.Show();
    }

    /// <summary>
    /// Records a guest frame so the stop line can be shown. The HWND is discarded.
    /// </summary>
    public void OfferGuest(nint hwnd, int frameWidth, int frameHeight)
    {
        _ = hwnd;
        _frameKnown = true;
        _frameW = frameWidth;
        _frameH = frameHeight;
        ApplyStill();
    }

    public void ShowStill(Bitmap? bitmap)
    {
        _still.Source = bitmap;
        _still.IsVisible = bitmap is not null;
        _still.Opacity = ViewportStill.WindowOpacity;
        _empty.IsVisible = bitmap is null;
    }

    public void ClearGuest(string? reason)
    {
        _frameKnown = false;
        _frameW = 0;
        _frameH = 0;
        SetStatus(string.IsNullOrWhiteSpace(reason) ? null : reason);
    }

    public void ReleaseGuest() => ClearGuest(null);

    public void ApplyPolicy()
    {
        _sync = true;
        try
        {
            _clickthrough.IsChecked = _policy.RequestedClickthrough;
            if (Math.Abs(_viewOpacity.Value - _policy.ViewOpacity) > 0.001)
                _viewOpacity.Value = _policy.ViewOpacity;
        }
        finally
        {
            _sync = false;
        }

        KeepStillOpaque();
        ApplyStill();
    }

    private void SetOpen(bool open)
    {
        _open = open;
        if (!_tabWindow.IsVisible)
            _tabWindow.Show();
        Pin();
        if (open)
            ApplyStill();
    }

    private void HideStill()
    {
        if (_hiding || !_stillWindow.IsVisible)
            return;
        _hiding = true;
        try
        {
            _stillWindow.Hide();
        }
        finally
        {
            _hiding = false;
        }
    }

    private void OnTabPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_tabRow).Properties.IsLeftButtonPressed)
            return;
        _pressAt = e.GetPosition(_tabRow);
        _pressMoved = false;
        e.Pointer.Capture(_tabRow);
        e.Handled = true;
    }

    private void OnTabMoved(object? sender, PointerEventArgs e)
    {
        if (_pressAt is not Point start)
            return;
        var dy = e.GetPosition(_tabRow).Y - start.Y;
        if (ViewportEdgeLayout.CountsAsDrag(dy))
            _pressMoved = true;
        if (!_open && ViewportEdgeLayout.DragPullsOpen(dy))
            SetOpen(true);
    }

    private void OnTabReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressAt is null)
            return;
        var moved = _pressMoved;
        _pressAt = null;
        _pressMoved = false;
        if (e.Pointer.Captured == _tabRow)
            e.Pointer.Capture(null);
        if (!moved)
            SetOpen(!_open);
        e.Handled = true;
    }

    private void OnSlotSizeChanged(Control slot)
    {
        var size = SlotPixels(slot);
        if (size.Width == _lastSlotW && size.Height == _lastSlotH)
            return;
        _lastSlotW = size.Width;
        _lastSlotH = size.Height;
        ApplyStill();
    }

    private void ApplyStill()
    {
        KeepStillOpaque();
        _clickNote.IsVisible = _policy.RequestedClickthrough;

        if (!_frameKnown)
            return;

        if (ViewportStill.MayParent(_lastSlotW, _lastSlotH, _frameW, _frameH))
            throw new InvalidOperationException("The viewport still does not parent a guest HWND.");

        var plan = ViewportStill.Present(_lastSlotW, _lastSlotH, _frameW, _frameH, _policy.RequestedClickthrough);
        SetStatus(plan.StopLine);
    }

    private void KeepStillOpaque()
    {
        if (Math.Abs(_stillWindow.Opacity - ViewportStill.WindowOpacity) > 0.0001)
            _stillWindow.Opacity = ViewportStill.WindowOpacity;
        _still.Opacity = ViewportStill.WindowOpacity;
        _pictureHost.Background = ViewportStill.CreateFillBrush();
        if (_stillWindow.TransparencyLevelHint.Count != 1
            || _stillWindow.TransparencyLevelHint[0] != WindowTransparencyLevel.None)
        {
            _stillWindow.TransparencyLevelHint = [WindowTransparencyLevel.None];
        }
    }

    private void SetStatus(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _status.Text = "";
            _status.IsVisible = false;
            return;
        }

        if (!string.Equals(_status.Text, text, StringComparison.Ordinal))
            _status.Text = text;
        _status.IsVisible = true;
    }

    private (int Width, int Height) SlotPixels(Control slot)
    {
        var scale = _stillWindow.RenderScaling <= 0 ? 1 : _stillWindow.RenderScaling;
        var width = (int)Math.Round(slot.Bounds.Width * scale);
        var height = (int)Math.Round(slot.Bounds.Height * scale);
        return (Math.Max(0, width), Math.Max(0, height));
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (GlassShellState.IsExiting)
            return;
        e.Cancel = true;
        if (_hiding)
            return;
        if (ReferenceEquals(sender, _stillWindow) || _open)
            SetOpen(false);
    }

    private static Window CreateTabWindow()
    {
        return new Window
        {
            Title = "VM",
            SystemDecorations = SystemDecorations.None,
            Background = Brushes.Transparent,
            Opacity = 1,
            TransparencyLevelHint =
            [
                WindowTransparencyLevel.Transparent,
                WindowTransparencyLevel.None
            ],
            ExtendClientAreaToDecorationsHint = true,
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            CanResize = false
        };
    }

    private static Window CreateStillWindow(string title)
    {
        return new Window
        {
            Title = title,
            SystemDecorations = SystemDecorations.None,
            Background = ViewportStill.CreateFillBrush(),
            Opacity = ViewportStill.WindowOpacity,
            TransparencyLevelHint = [WindowTransparencyLevel.None],
            ExtendClientAreaToDecorationsHint = true,
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            CanResize = false
        };
    }
}
