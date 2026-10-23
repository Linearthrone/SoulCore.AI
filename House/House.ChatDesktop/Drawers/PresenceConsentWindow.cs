using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Drawers;

/// <summary>Opaque, topmost consent dialog. It is not a glass or click-through window.</summary>
internal static class PresenceConsentWindow
{
    public static async Task<bool> ShowAsync(
        Window owner,
        bool light,
        string title,
        string message,
        string confirmText,
        string cancelText)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 440,
            Height = 240,
            MinWidth = 360,
            MinHeight = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Topmost = true,
            ShowInTaskbar = !GlassShellState.HotkeysArmed,
            Background = new SolidColorBrush(GlassChrome.OpaqueBackground(light)),
            Foreground = new SolidColorBrush(light
                ? Color.FromRgb(0x14, 0x20, 0x33)
                : Color.FromRgb(0xE6, 0xE7, 0xEA)),
            TransparencyLevelHint = [WindowTransparencyLevel.None],
            Opacity = 1,
            SystemDecorations = SystemDecorations.BorderOnly
        };

        var body = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };
        var cancel = new Button
        {
            Content = cancelText,
            MinWidth = 88,
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancel.Classes.Add("ghost");
        var confirm = new Button
        {
            Content = confirmText,
            MinWidth = 88
        };
        confirm.Classes.Add("primary");
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, confirm }
        };
        dialog.Content = new Border
        {
            Padding = new Thickness(20),
            Background = new SolidColorBrush(GlassChrome.OpaqueBackground(light)),
            Child = new DockPanel
            {
                Children = { buttons, body }
            }
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        var tcs = new TaskCompletionSource<bool>();
        cancel.Click += (_, _) =>
        {
            tcs.TrySetResult(false);
            dialog.Close();
        };
        confirm.Click += (_, _) =>
        {
            tcs.TrySetResult(true);
            dialog.Close();
        };
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        if (!owner.IsVisible)
            owner.Show();
        await dialog.ShowDialog(owner).ConfigureAwait(true);
        return await tcs.Task.ConfigureAwait(true);
    }
}
