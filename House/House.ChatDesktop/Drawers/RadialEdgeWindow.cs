using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using House.ChatDesktop.Controls;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Drawers;

public sealed class RadialEdgeWindow : Window
{
    private readonly LeftEdgeRadial _radial = new();
    private readonly PersonaModeControl _mode = new();
    private readonly Grid _host = new();
    private readonly Border _noticeHost;
    private readonly TextBlock _notice;
    private bool _expanded;

    public RadialEdgeWindow()
    {
        Title = "Presence";
        _mode.HorizontalAlignment = HorizontalAlignment.Right;
        _mode.VerticalAlignment = VerticalAlignment.Bottom;
        _mode.Margin = new Thickness(0, 0, 14, 14);
        _mode.IsVisible = false;
        _mode.ModeChosen += (_, mode) => ModeChosen?.Invoke(this, mode);

        _notice = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = RadialTabChrome.CreateMarkBrush(),
            FontSize = 12
        };
        _noticeHost = new Border
        {
            IsVisible = false,
            Background = RadialTabChrome.CreateFillBrush(),
            BorderBrush = RadialTabChrome.CreateStrokeBrush(),
            BorderThickness = new Thickness(RadialTabChrome.BorderDip),
            Padding = new Thickness(10, 8),
            Margin = new Thickness(8, 0, 8, 8),
            Child = _notice
        };
        _noticeHost.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            ClearNotice();
        };

        _host.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        _host.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _host.Children.Add(_radial);
        _host.Children.Add(_mode);
        _host.Children.Add(_noticeHost);
        Grid.SetRow(_noticeHost, 1);
        Content = _host;
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint =
        [
            WindowTransparencyLevel.Transparent,
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.None
        ];
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
        ShowInTaskbar = false;
        CanResize = false;
        Topmost = true;
        ShowActivated = false;
        Width = 46;
        Height = 96;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _radial.ExpandRequested += (_, _) => SetExpanded(true);
        _radial.CollapseRequested += (_, _) => SetExpanded(false);
        _radial.CreateAgentRequested += (_, _) => CreateAgentRequested?.Invoke(this, EventArgs.Empty);
        _radial.ContactsRequested += (_, _) => ContactsRequested?.Invoke(this, EventArgs.Empty);
        _radial.FavoriteRequested += (_, slot) => FavoriteRequested?.Invoke(this, slot);

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _expanded)
            {
                e.Handled = true;
                SetExpanded(false);
            }
        };
        Closing += (_, e) =>
        {
            if (GlassShellState.IsExiting)
                return;
            e.Cancel = true;
            SetExpanded(false);
        };
        Opened += (_, _) => PinToLeftEdge();
    }

    public event EventHandler? CreateAgentRequested;
    public event EventHandler? ContactsRequested;
    public event EventHandler<FavoriteSlot>? FavoriteRequested;
    public event EventHandler<string>? ModeChosen;

    public string ShownMode => _mode.ShownMode;

    public void ApplyMode(string? mode) => _mode.Apply(mode);

    public void SetFavorites(IReadOnlyList<FavoriteSlot>? favorites) => _radial.SetFavorites(favorites);

    public void ShowNotice(string? text)
    {
        var message = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        _notice.Text = message ?? "";
        _noticeHost.IsVisible = message is not null;
        ApplySize();
    }

    public void ClearNotice() => ShowNotice(null);

    /// <summary>
    /// Drops this window behind <paramref name="dialog"/> for the life of the dialog.
    /// The caller shows the dialog on this window so it stays in front of the radial.
    /// </summary>
    public IDisposable PresentAbove(Window dialog)
    {
        if (!IsVisible)
            Show();
        var wasTopmost = Topmost;
        Topmost = false;
        dialog.Topmost = true;
        dialog.ShowActivated = true;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return new TopmostRestore(this, wasTopmost);
    }

    /// <summary>Puts this radial back at the front of the topmost band. Chat is raised after this.</summary>
    public void ReenterTopmost() => ChatDrawerFront.ReenterTopmost(this);

    public void PinToLeftEdge()
    {
        var screen = Screens?.ScreenFromPoint(Position) ?? Screens?.Primary;
        var area = screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        var scale = RenderScaling <= 0 ? 1 : RenderScaling;
        var height = (int)(Height * scale);
        var y = area.Y + Math.Max(0, (area.Height - height) / 2);
        Position = new PixelPoint(area.X, y);
    }

    private void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        _radial.IsExpanded = expanded;
        _mode.IsVisible = expanded;
        ApplySize();
        _radial.InvalidateVisual();
    }

    private void ApplySize()
    {
        var notice = _noticeHost.IsVisible;
        if (_expanded)
        {
            Width = 340;
            Height = notice ? 440 : 360;
        }
        else if (notice)
        {
            Width = 300;
            Height = 180;
        }
        else
        {
            Width = 46;
            Height = 96;
        }

        PinToLeftEdge();
    }

    private sealed class TopmostRestore : IDisposable
    {
        private readonly RadialEdgeWindow _window;
        private readonly bool _topmost;
        private bool _done;

        public TopmostRestore(RadialEdgeWindow window, bool topmost)
        {
            _window = window;
            _topmost = topmost;
        }

        public void Dispose()
        {
            if (_done)
                return;
            _done = true;
            _window.Topmost = _topmost;
        }
    }
}
