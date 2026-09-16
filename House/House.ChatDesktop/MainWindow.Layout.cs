using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using House.ChatDesktop.Services;

namespace House.ChatDesktop;

public partial class MainWindow
{
    private DispatcherTimer? _layoutSaveTimer;
    private bool _layoutReady;
    private bool _applyingLayout;

    private void InitLayoutChrome()
    {
        ApplyStoredWindowBounds();
        ApplyStoredPaneSizes();
        UpdateMaximizeCaption();
        SyncResizeGripState();
        WirePaneSplitterPersistence();

        Opened += (_, _) =>
        {
            _layoutReady = true;
            // Re-apply after first measure so ActualWidth/Height mins stick.
            ApplyStoredPaneSizes();
        };
        Closing += (_, _) => PersistLayoutNow();
        PropertyChanged += MainWindow_LayoutPropertyChanged;
        // Window.Position is a CLR property (not AvaloniaProperty) — use PositionChanged.
        PositionChanged += (_, _) => ScheduleLayoutSave();
    }

    private void WirePaneSplitterPersistence()
    {
        // GridSplitter inherits Thumb drag events; window size does not change when panes move.
        if (PresenceColumnSplitter is not null)
            PresenceColumnSplitter.AddHandler(
                Thumb.DragCompletedEvent,
                (_, _) => ScheduleLayoutSave());
        if (PresenceRowSplitter is not null)
            PresenceRowSplitter.AddHandler(
                Thumb.DragCompletedEvent,
                (_, _) => ScheduleLayoutSave());
    }

    private void MainWindow_LayoutPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_layoutReady || _applyingLayout)
            return;
        if (e.Property == WindowStateProperty)
        {
            UpdateMaximizeCaption();
            SyncResizeGripState();
            ScheduleLayoutSave();
            return;
        }

        if (e.Property == WidthProperty || e.Property == HeightProperty)
        {
            ScheduleLayoutSave();
        }
    }

    private void ApplyStoredWindowBounds()
    {
        _applyingLayout = true;
        try
        {
            Width = _uiSettings.ResolvedWindowWidth();
            Height = _uiSettings.ResolvedWindowHeight();
            MinWidth = LocalUiSettings.MinWindowWidth;
            MinHeight = LocalUiSettings.MinWindowHeight;

            if (_uiSettings.WindowX is double x && _uiSettings.WindowY is double y
                && !double.IsNaN(x) && !double.IsNaN(y))
            {
                // Position after first layout pass is safer; set immediately for cold start.
                Position = new PixelPoint((int)Math.Round(x), (int)Math.Round(y));
                WindowStartupLocation = WindowStartupLocation.Manual;
            }

            if (_uiSettings.WindowMaximized)
                WindowState = WindowState.Maximized;
        }
        finally
        {
            _applyingLayout = false;
        }
    }

    private void ApplyStoredPaneSizes()
    {
        if (PresenceMainSplit?.ColumnDefinitions is { Count: >= 3 } cols)
        {
            var side = _uiSettings.ResolvedSideColumnWidth();
            cols[2].Width = new GridLength(side);
            cols[2].MinWidth = LocalUiSettings.MinSideColumnWidth;
            cols[0].MinWidth = LocalUiSettings.MinChatWidth;
        }

        if (PresenceSideSplit?.RowDefinitions is { Count: >= 3 } rows)
        {
            var sight = _uiSettings.ResolvedSightRowHeight();
            rows[2].Height = new GridLength(sight);
            rows[2].MinHeight = LocalUiSettings.MinSightRowHeight;
            rows[0].MinHeight = LocalUiSettings.MinBrowserRowHeight;
        }
    }

    private void CapturePaneSizesIntoSettings()
    {
        if (PresenceMainSplit?.ColumnDefinitions is { Count: >= 3 } cols)
        {
            var side = cols[2].ActualWidth;
            if (side >= LocalUiSettings.MinSideColumnWidth)
                _uiSettings.SideColumnWidth = side;
        }

        if (PresenceSideSplit?.RowDefinitions is { Count: >= 3 } rows)
        {
            var sight = rows[2].ActualHeight;
            if (sight >= LocalUiSettings.MinSightRowHeight)
                _uiSettings.SightRowHeight = sight;
        }
    }

    private void CaptureWindowBoundsIntoSettings()
    {
        _uiSettings.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            _uiSettings.WindowWidth = Width;
            _uiSettings.WindowHeight = Height;
            _uiSettings.WindowX = Position.X;
            _uiSettings.WindowY = Position.Y;
        }
    }

    private void ScheduleLayoutSave()
    {
        if (!_layoutReady || _applyingLayout)
            return;

        _layoutSaveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _layoutSaveTimer.Tick -= LayoutSaveTimer_Tick;
        _layoutSaveTimer.Tick += LayoutSaveTimer_Tick;
        _layoutSaveTimer.Stop();
        _layoutSaveTimer.Start();
    }

    private void LayoutSaveTimer_Tick(object? sender, EventArgs e)
    {
        _layoutSaveTimer?.Stop();
        PersistLayoutNow();
    }

    private void PersistLayoutNow()
    {
        if (_applyingLayout)
            return;
        try
        {
            CaptureWindowBoundsIntoSettings();
            CapturePaneSizesIntoSettings();
            _uiSettings.Save();
        }
        catch
        {
            // Local prefs must never crash the shell.
        }
    }

    private void UpdateMaximizeCaption()
    {
        if (MaximizeButton is null)
            return;
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        ToolTip.SetTip(MaximizeButton, WindowState == WindowState.Maximized ? "Restore" : "Maximize");
    }

    private void SyncResizeGripState()
    {
        var enabled = WindowState != WindowState.Maximized;
        foreach (var grip in new[]
                 {
                     ResizeWest, ResizeEast, ResizeNorth, ResizeSouth,
                     ResizeNorthWest, ResizeNorthEast, ResizeSouthWest, ResizeSouthEast
                 })
        {
            if (grip is null) continue;
            grip.IsHitTestVisible = enabled;
            grip.IsVisible = enabled;
        }
    }

    private void Maximize_Click(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void TitleDragRegion_DoubleTapped(object? sender, TappedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void ResizeEdge_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (sender is not Control { Tag: string tag })
            return;

        var edge = tag switch
        {
            "West" => WindowEdge.West,
            "East" => WindowEdge.East,
            "North" => WindowEdge.North,
            "South" => WindowEdge.South,
            "NorthWest" => WindowEdge.NorthWest,
            "NorthEast" => WindowEdge.NorthEast,
            "SouthWest" => WindowEdge.SouthWest,
            "SouthEast" => WindowEdge.SouthEast,
            _ => (WindowEdge?)null
        };
        if (edge is null)
            return;

        BeginResizeDrag(edge.Value, e);
    }
}
