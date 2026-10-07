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
            // Frameless transparent window off a disconnected monitor = "starts then nothing".
            EnsureWindowOnScreen();
            PresenceStartupLog.Write(
                $"MainWindow opened at {Position.X},{Position.Y} size {Width}x{Height} state={WindowState}");
        };
        Closing += (_, _) => PersistLayoutNow();
        PropertyChanged += MainWindow_LayoutPropertyChanged;
        // Window.Position is a CLR property (not AvaloniaProperty) - use PositionChanged.
        PositionChanged += (_, _) => ScheduleLayoutSave();
    }

    private void WirePaneSplitterPersistence()
    {
        // GridSplitter inherits Thumb drag events; window size does not change when panes move.
        if (PresenceColumnSplitter is not null)
            PresenceColumnSplitter.AddHandler(
                Thumb.DragCompletedEvent,
                (_, _) =>
                {
                    _victoriaBrowserEmbedHost?.SyncSizeToSlot();
                    ScheduleLayoutSave();
                });
        // PresenceRowSplitter removed — What she saw is a tab beside Her screen.
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

            // Screens may be empty before the window handle exists; Opened re-checks.
            var areas = CollectScreenWorkingAreas();
            var (pos, relocated) = PresenceWindowPlacement.ResolveStartPosition(
                _uiSettings.WindowX,
                _uiSettings.WindowY,
                Width,
                Height,
                areas);

            if (pos is PixelPoint safe)
            {
                Position = safe;
                WindowStartupLocation = WindowStartupLocation.Manual;
                if (relocated)
                {
                    PresenceStartupLog.Write(
                        $"Saved window position off-screen; moved to {safe.X},{safe.Y}");
                    _uiSettings.WindowX = safe.X;
                    _uiSettings.WindowY = safe.Y;
                }
            }

            if (_uiSettings.WindowMaximized)
                WindowState = WindowState.Maximized;
        }
        finally
        {
            _applyingLayout = false;
        }
    }

    private void EnsureWindowOnScreen()
    {
        if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen)
            return;

        var areas = CollectScreenWorkingAreas();
        if (areas.Count == 0)
            return;

        var size = PixelSize.FromSize(ClientSize, DesktopScaling);
        if (size.Width <= 0 || size.Height <= 0)
            size = new PixelSize((int)Math.Round(Width), (int)Math.Round(Height));

        var bounds = new PixelRect(Position, size);
        if (PresenceWindowPlacement.IntersectsAnyScreen(bounds, areas)
            && PresenceWindowPlacement.IsPointOnAnyScreen(Position.X, Position.Y, areas))
            return;

        _applyingLayout = true;
        try
        {
            var pos = PresenceWindowPlacement.CenterOnPrimary(size, areas);
            Position = pos;
            _uiSettings.WindowX = pos.X;
            _uiSettings.WindowY = pos.Y;
            PresenceStartupLog.Write($"EnsureWindowOnScreen relocated to {pos.X},{pos.Y}");
            try { _uiSettings.Save(); }
            catch { /* ignore */ }
        }
        finally
        {
            _applyingLayout = false;
        }
    }

    private IReadOnlyList<PixelRect> CollectScreenWorkingAreas()
    {
        try
        {
            var screens = Screens?.All;
            if (screens is null || screens.Count == 0)
                return Array.Empty<PixelRect>();

            var list = new List<PixelRect>(screens.Count);
            foreach (var screen in screens)
                list.Add(screen.WorkingArea);
            return list;
        }
        catch (Exception ex)
        {
            PresenceStartupLog.WriteException("CollectScreenWorkingAreas", ex);
            return Array.Empty<PixelRect>();
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

        // Sight row height no longer applies — Her screen / What she saw share a tabbed column.
    }

    private void CapturePaneSizesIntoSettings()
    {
        if (PresenceMainSplit?.ColumnDefinitions is { Count: >= 3 } cols)
        {
            var side = cols[2].ActualWidth;
            if (side >= LocalUiSettings.MinSideColumnWidth)
                _uiSettings.SideColumnWidth = side;
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
