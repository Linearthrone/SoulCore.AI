using Avalonia;

namespace House.ChatDesktop;

internal static class Program
{
    // Cross-platform Avalonia entry point (Linux, Windows, macOS).
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
