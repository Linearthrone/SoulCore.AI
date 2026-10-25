using Avalonia.Controls;
using Avalonia.Interactivity;

namespace House.ChatDesktop;

public partial class MainWindow
{
    private void PanelPopOut_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        switch (tag)
        {
            case "hud":
                _popOuts.Toggle("hud", "Victoria — HUD", HudPanel, _hudDock, _hudTop);
                break;
            case "chat":
                _popOuts.Toggle("chat", "Messages", ChatPanel, _chatDock, _chatTop);
                break;
            case "services":
                _popOuts.Toggle("services", "Services", ServicesPanel, _servicesDock, _servicesTop);
                break;
        }
    }

    private void HudAlwaysOnTop_Changed(object? sender, RoutedEventArgs e)
    {
        _hudTop.IsOn = HudAlwaysOnTop?.IsChecked == true;
        if (_hudTop.IsOn) Topmost = true;
    }

    private void ChatAlwaysOnTop_Changed(object? sender, RoutedEventArgs e)
    {
        _chatTop.IsOn = ChatAlwaysOnTop?.IsChecked == true;
        if (_chatTop.IsOn) Topmost = true;
    }

    private void ServicesAlwaysOnTop_Changed(object? sender, RoutedEventArgs e)
    {
        _servicesTop.IsOn = ServicesAlwaysOnTop?.IsChecked == true;
        if (_servicesTop.IsOn) Topmost = true;
    }
}
