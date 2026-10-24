using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using House.ChatDesktop.Services;

namespace House.ChatDesktop;

public partial class MainWindow
{
    private Control? _chatBody;
    private Border? _chatEdgeChrome;
    private Border? _chatEdgeTab;
    private ColumnDefinition? _chatBodyColumn;
    private bool _chatOpen;
    private bool _chatEdgePlaced;
    private bool _chatEdgeOwnsPlacement;
    private Point? _chatPressAt;
    private bool _chatPressMoved;

    private void InstallChatEdgeChrome()
    {
        if (Content is not Control body || _chatEdgeChrome is not null)
            return;

        _chatBody = body;
        body.IsVisible = false;

        var label = new TextBlock
        {
            Text = "Chat",
            Foreground = RadialTabChrome.CreateMarkBrush(),
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var turned = new LayoutTransformControl
        {
            LayoutTransform = new RotateTransform(-90),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = label
        };
        _chatEdgeTab = new Border
        {
            Width = ChatEdgeLayout.ClosedTabWidthDip,
            Background = Brushes.Transparent,
            Child = turned,
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        ToolTip.SetTip(_chatEdgeTab, "Chat");
        _chatEdgeTab.PointerPressed += OnChatTabPressed;
        _chatEdgeTab.PointerMoved += OnChatTabMoved;
        _chatEdgeTab.PointerReleased += OnChatTabReleased;
        _chatEdgeTab.PointerCaptureLost += (_, _) =>
        {
            _chatPressAt = null;
            _chatPressMoved = false;
        };

        _chatBodyColumn = new ColumnDefinition(new GridLength(0));
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                _chatBodyColumn,
                new ColumnDefinition(GridLength.Auto)
            }
        };
        grid.Children.Add(body);
        grid.Children.Add(_chatEdgeTab);
        Grid.SetColumn(_chatEdgeTab, 1);

        _chatEdgeChrome = new Border
        {
            Background = RadialTabChrome.CreateFillBrush(),
            BorderBrush = RadialTabChrome.CreateStrokeBrush(),
            BorderThickness = new Thickness(RadialTabChrome.BorderDip),
            CornerRadius = new CornerRadius(ChatEdgeLayout.CornerRadiusDip, 0, 0, ChatEdgeLayout.CornerRadiusDip),
            Child = grid
        };
        Content = _chatEdgeChrome;
        MinWidth = 0;
        MinHeight = 0;
        CanResize = false;
        Topmost = true;
    }

    private void ApplyClosedTabSize()
    {
        _chatEdgeOwnsPlacement = true;
        _chatOpen = false;
        MinWidth = 0;
        MinHeight = 0;
        CanResize = false;
        Width = ChatEdgeLayout.ClosedTabWidthDip;
        Height = ChatEdgeLayout.ClosedTabHeightDip;
        WindowStartupLocation = WindowStartupLocation.Manual;
        if (_chatBody is not null)
            _chatBody.IsVisible = false;
        if (_chatBodyColumn is not null)
            _chatBodyColumn.Width = new GridLength(0);
    }

    private void SetChatOpen(bool open)
    {
        _chatOpen = open;
        if (_chatBody is not null)
            _chatBody.IsVisible = open;
        if (_chatBodyColumn is not null)
            _chatBodyColumn.Width = open ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        PinChatEdge();
        if (!IsVisible)
            Show();
    }

    private void PinChatEdge()
    {
        var screen = Screens?.ScreenFromWindow(this) ?? Screens?.Primary;
        var area = screen?.WorkingArea;
        if (area is not { Width: > 0, Height: > 0 } work)
        {
            MinWidth = 0;
            MinHeight = 0;
            Width = _chatOpen ? 640 : ChatEdgeLayout.ClosedTabWidthDip;
            Height = _chatOpen ? 480 : ChatEdgeLayout.ClosedTabHeightDip;
            return;
        }

        var scale = RenderScaling <= 0 ? 1 : RenderScaling;
        var place = ChatEdgeLayout.Place(work.X, work.Y, work.Width, work.Height, _chatOpen, scale);
        var widthDip = Math.Max(1, place.WidthPx / scale);
        var heightDip = Math.Max(1, place.HeightPx / scale);

        _applyingLayout = true;
        try
        {
            if (WindowState != WindowState.Normal)
                WindowState = WindowState.Normal;
            CanResize = false;
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
            MinWidth = 0;
            MinHeight = 0;
            Width = widthDip;
            Height = heightDip;
            MinWidth = widthDip;
            MaxWidth = widthDip;
            MinHeight = heightDip;
            MaxHeight = heightDip;
            Position = new PixelPoint(place.X, place.Y);
            if (_chatEdgeTab is not null)
                _chatEdgeTab.Width = Math.Max(1, place.TabWidthPx / scale);
            Topmost = true;
        }
        finally
        {
            _applyingLayout = false;
        }
    }

    private void OnChatTabPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_chatEdgeTab is null || !e.GetCurrentPoint(_chatEdgeTab).Properties.IsLeftButtonPressed)
            return;
        _chatPressAt = e.GetPosition(_chatEdgeTab);
        _chatPressMoved = false;
        e.Pointer.Capture(_chatEdgeTab);
        e.Handled = true;
    }

    private void OnChatTabMoved(object? sender, PointerEventArgs e)
    {
        if (_chatEdgeTab is null || _chatPressAt is not Point start)
            return;
        var dx = e.GetPosition(_chatEdgeTab).X - start.X;
        if (ChatEdgeLayout.CountsAsDrag(dx))
            _chatPressMoved = true;
        if (!_chatOpen && ChatEdgeLayout.DragPullsOpen(dx))
            SetChatOpen(true);
    }

    private void OnChatTabReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_chatEdgeTab is null || _chatPressAt is null)
            return;
        var moved = _chatPressMoved;
        _chatPressAt = null;
        _chatPressMoved = false;
        if (e.Pointer.Captured == _chatEdgeTab)
            e.Pointer.Capture(null);
        if (!moved)
            SetChatOpen(!_chatOpen);
        e.Handled = true;
    }
}
