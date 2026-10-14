using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Controls;

/// <summary>
/// Owned chrome over the Her-screen surface. The embedded VirtualBox HWND paints
/// above Avalonia siblings, so this window stays in the top-right corner and still takes clicks.
/// </summary>
public sealed class VictoriaVmPowerButtonWindow : Window
{
    private readonly Button _button;
    private bool? _running;
    private bool _busy;

    public VictoriaVmPowerButtonWindow()
    {
        Title = "VM power";
        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = false;
        Width = VictoriaVmPowerButtonPlacement.ButtonWidth;
        Height = VictoriaVmPowerButtonPlacement.ButtonHeight;
        SizeToContent = SizeToContent.Manual;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 2),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        _button.Click += (_, _) =>
        {
            if (!_busy)
                Clicked?.Invoke(this, EventArgs.Empty);
        };
        Content = _button;
        Apply(running: false, busy: false);
    }

    public event EventHandler? Clicked;

    public void Apply(bool? running, bool busy)
    {
        _running = running;
        _busy = busy;
        _button.IsEnabled = !busy;

        if (busy)
        {
            _button.Content = running == true ? "Stopping…" : "Starting…";
            Paint(Color.Parse("#1A1C20"), Color.Parse("#8B8E97"), Color.Parse("#3A404C"));
            ToolTip.SetTip(_button, running == true
                ? "Saving victoria-sandbox state…"
                : "Starting victoria-sandbox…");
            return;
        }

        if (running == true)
        {
            _button.Content = "Stop";
            Paint(Color.Parse("#2A1214"), Color.Parse("#FF8A90"), Color.Parse("#FF3D4A"));
            ToolTip.SetTip(_button, "Stop victoria-sandbox (save state — next Start resumes)");
            return;
        }

        _button.Content = "Start";
        Paint(Color.Parse("#102418"), Color.Parse("#8CFFB0"), Color.Parse("#2EE66A"));
        ToolTip.SetTip(_button, "Start victoria-sandbox");
    }

    public void SyncToSurface(Control surface)
    {
        if (!IsVisible)
            return;

        var bounds = surface.Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1)
            return;

        var buttonWidth = Bounds.Width > 1 ? Bounds.Width : Width;
        var offset = VictoriaVmPowerButtonPlacement.TopRightOffset(bounds.Width, buttonWidth);
        PixelPoint topLeft;
        try
        {
            topLeft = surface.PointToScreen(new Point(offset.X, offset.Y));
        }
        catch
        {
            return;
        }

        if (Position != topLeft)
            Position = topLeft;
    }

    private void Paint(Color background, Color foreground, Color border)
    {
        _button.Background = new SolidColorBrush(background);
        _button.Foreground = new SolidColorBrush(foreground);
        _button.BorderBrush = new SolidColorBrush(border);
    }
}
