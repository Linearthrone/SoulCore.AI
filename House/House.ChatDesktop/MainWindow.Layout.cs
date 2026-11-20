using System;
using Avalonia;
using Avalonia.Controls;
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
    private Size _lastLockedSize;

    private void InitLayoutChrome()
    {
        ApplyStoredWindowBounds();
        UpdateMaximizeCaption();
        SyncResizeGripState();

        Opened += (_, _) =>
        {
            _layoutReady = true;
            // Frameless transparent window off a disconnected monitor = "starts then nothing".
            EnsureWindowOnScreen();
            FitToWorkingArea();
            _lastLockedSize = new Size(Width, Height);
            PresenceStartupLog.Write(
                $"MainWindow opened at {Position.X},{Position.Y} size {Width}x{Height} state={WindowState}");
        };
        Closing += (_, _) => PersistLayoutNow();
        PropertyChanged += MainWindow_LayoutPropertyChanged;
        // Window.Position is a CLR property (not AvaloniaProperty) - use PositionChanged.
        PositionChanged += (_, _) =>
        {
            ScheduleLayoutSave();
            RepositionVmDrawer();
            RepositionRadial();
        };
        Resized += MainWindow_Resized;
    }

    private void MainWindow_LayoutPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_layoutReady || _applyingLayout)
            return;
        if (e.Property == WindowStateProperty)
        {
            UpdateMaximizeCaption();
            SyncResizeGripState();
            RepositionVmDrawer();
            ScheduleLayoutSave();
            return;
        }

        if (e.Property == WidthProperty || e.Property == HeightProperty)
            ScheduleLayoutSave();
    }

    /// <summary>
    /// PROP-22.4 aspect lock: the shell is a phone slab. Width drives height (least fighty); when
    /// only the height moved (top/bottom edge drag) the width follows it instead.
    /// </summary>
    private void MainWindow_Resized(object? sender, WindowResizedEventArgs e)
    {
        if (!_layoutReady || _applyingLayout || WindowState != WindowState.Normal)
            return;

        var w = e.ClientSize.Width;
        var h = e.ClientSize.Height;
        if (w <= 0 || h <= 0)
            return;

        var expectedHeight = LocalUiSettings.HeightForWidth(w);
        if (Math.Abs(h - expectedHeight) <= 1.5)
        {
            _lastLockedSize = e.ClientSize;
            RepositionVmDrawer();
            return;
        }

        var widthMoved = _lastLockedSize.Width <= 0 || Math.Abs(w - _lastLockedSize.Width) > 0.5;
        _applyingLayout = true;
        try
        {
            if (widthMoved)
            {
                Height = Math.Max(MinHeight, expectedHeight);
            }
            else
            {
                Width = Math.Max(MinWidth, LocalUiSettings.WidthForHeight(h));
            }
        }
        finally
        {
            _applyingLayout = false;
        }

        _lastLockedSize = new Size(Width, Height);
        RepositionVmDrawer();
        ScheduleLayoutSave();
    }

    private void ApplyStoredWindowBounds()
    {
        _applyingLayout = true;
        try
        {
            var width = Math.Max(LocalUiSettings.MinWindowWidth, _uiSettings.ResolvedWindowWidth());
            Width = width;
            Height = LocalUiSettings.HeightForWidth(width);
            MinWidth = LocalUiSettings.MinWindowWidth;
            // 360 wide at 19.5:9 is 780 tall — that, not the 640 floor, is the effective minimum.
            MinHeight = Math.Max(
                LocalUiSettings.MinWindowHeight,
                LocalUiSettings.HeightForWidth(LocalUiSettings.MinWindowWidth));

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

    /// <summary>Shrink the slab (keeping 19.5:9) when the default 996 tall does not fit this screen.</summary>
    private void FitToWorkingArea()
    {
        if (WindowState != WindowState.Normal)
            return;

        var areas = CollectScreenWorkingAreas();
        if (areas.Count == 0)
            return;

        var scaling = DesktopScaling <= 0 ? 1.0 : DesktopScaling;
        var area = Screens.ScreenFromWindow(this)?.WorkingArea ?? areas[0];
        var maxHeight = area.Height / scaling - 16;
        if (Height <= maxHeight)
            return;

        var height = Math.Max(MinHeight, maxHeight);
        var width = Math.Max(MinWidth, LocalUiSettings.WidthForHeight(height));
        _applyingLayout = true;
        try
        {
            Width = width;
            Height = Math.Max(height, LocalUiSettings.HeightForWidth(width));
            var pos = Position;
            var y = Math.Max(area.Y, Math.Min(pos.Y, area.Bottom - (int)Math.Round(Height * scaling)));
            Position = new PixelPoint(pos.X, y);
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
