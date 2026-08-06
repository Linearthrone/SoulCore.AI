using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace House.ChatDesktop.Services;

/// <summary>
/// Detaches a docked panel into its own window with Always-on-top chrome.
/// </summary>
public sealed class PanelPopOutService
{
    private readonly Dictionary<string, Window> _windows = new(StringComparer.Ordinal);

    public bool IsPopped(string key) => _windows.ContainsKey(key);

    public void Toggle(
        string key,
        string title,
        Border panel,
        Panel? dockHost,
        ToggleButtonState alwaysOnTop,
        Action? onClosed = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(panel);

        if (_windows.TryGetValue(key, out var existing))
        {
            existing.Activate();
            return;
        }

        if (dockHost is null)
            return;

        var placeholder = new Border
        {
            MinHeight = panel.Bounds.Height > 0 ? panel.Bounds.Height : 120,
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = $"{title} — popped out",
                Foreground = Res("MutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12
            }
        };

        var index = dockHost.Children.IndexOf(panel);
        if (index < 0) return;

        dockHost.Children.RemoveAt(index);
        dockHost.Children.Insert(index, placeholder);

        var topToggle = new CheckBox
        {
            Content = "Always on top",
            IsChecked = alwaysOnTop.IsOn,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var host = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(topToggle, Dock.Top);
        host.Children.Add(topToggle);
        host.Children.Add(panel);

        var win = new Window
        {
            Title = title,
            Width = Math.Max(420, panel.Bounds.Width > 0 ? panel.Bounds.Width + 40 : 520),
            Height = Math.Max(320, panel.Bounds.Height > 0 ? panel.Bounds.Height + 80 : 480),
            Background = Res("BgBrush"),
            Foreground = Res("TextBrush"),
            Content = host,
            Topmost = alwaysOnTop.IsOn,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        topToggle.IsCheckedChanged += (_, _) =>
        {
            var on = topToggle.IsChecked == true;
            alwaysOnTop.IsOn = on;
            win.Topmost = on;
        };

        win.Closed += (_, _) =>
        {
            _windows.Remove(key);
            host.Children.Remove(panel);
            var phIndex = dockHost.Children.IndexOf(placeholder);
            if (phIndex >= 0)
            {
                dockHost.Children.RemoveAt(phIndex);
                dockHost.Children.Insert(phIndex, panel);
            }
            else
            {
                dockHost.Children.Add(panel);
            }

            onClosed?.Invoke();
        };

        _windows[key] = win;
        win.Show();
    }

    public void CloseAll()
    {
        foreach (var win in _windows.Values.ToList())
        {
            try { win.Close(); }
            catch { /* ignore */ }
        }

        _windows.Clear();
    }

    private static IBrush Res(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var v) && v is IBrush b
            ? b
            : Brushes.Gray;
}

/// <summary>Mutable always-on-top preference shared between docked chrome and pop-out.</summary>
public sealed class ToggleButtonState
{
    public bool IsOn { get; set; }
}
