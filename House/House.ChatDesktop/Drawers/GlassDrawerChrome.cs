using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Drawers;

internal static class GlassDrawerChrome
{
    public static Window Host(string title, Control body, double width, double height)
    {
        var window = new Window();
        Attach(window, title, body, width, height);
        return window;
    }

    public static void Attach(Window window, string title, Control body, double width, double height)
    {
        var drag = new Border
        {
            Height = 36,
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = title,
                Margin = new Thickness(12, 8, 8, 0),
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            }
        };
        var hide = new Button
        {
            Content = "Hide",
            Margin = new Thickness(0, 6, 8, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        hide.Classes.Add("ghost");

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        header.Children.Add(drag);
        header.Children.Add(hide);
        Grid.SetColumn(hide, 1);

        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);
        dock.Children.Add(body);

        var shell = new Border { Child = dock };
        shell.Classes.Add("shell");
        var bevel = new Border
        {
            Margin = new Thickness(4),
            Child = shell
        };
        bevel.Classes.Add("shell-bevel");

        window.Title = title;
        window.Width = width;
        window.Height = height;
        window.MinWidth = Math.Min(420, width);
        window.MinHeight = Math.Min(180, height);
        window.Content = bevel;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.CanResize = true;
        window.Background = Brushes.Transparent;
        window.TransparencyLevelHint =
        [
            WindowTransparencyLevel.Transparent,
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.None
        ];
        window.SystemDecorations = SystemDecorations.None;
        window.ExtendClientAreaToDecorationsHint = true;
        window.ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
        window.ShowInTaskbar = false;

        hide.Click += (_, _) => window.Hide();
        drag.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
                return;
            window.BeginMoveDrag(e);
            if (DrawerDismiss.IsMostlyOffScreen(window))
                window.Hide();
        };
        window.Closing += (_, e) =>
        {
            if (GlassShellState.IsExiting)
                return;
            e.Cancel = true;
            window.Hide();
        };
    }

    public static bool TryDetach(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                return true;
            case ContentControl content when ReferenceEquals(content.Content, control):
                content.Content = null;
                return true;
            case Decorator decorator when ReferenceEquals(decorator.Child, control):
                decorator.Child = null;
                return true;
            default:
                return control.Parent is null;
        }
    }
}
