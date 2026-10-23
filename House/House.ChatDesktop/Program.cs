using Avalonia;
using House.ChatDesktop.Services;
using Velopack;

namespace House.ChatDesktop;

internal static class Program
{
    // Cross-platform Avalonia entry point (Linux, Windows, macOS).
    [STAThread]
    public static void Main(string[] args)
    {
        PresenceStartupLog.Write($"Main enter args={args.Length} base={AppContext.BaseDirectory}");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            CuaLampClear.TryClearNow();
            if (e.ExceptionObject is Exception ex)
                PresenceStartupLog.WriteException("UnhandledException", ex);
            else
                PresenceStartupLog.Write($"UnhandledException: {e.ExceptionObject}");
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) => CuaLampClear.TryClearNow();

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            PresenceStartupLog.WriteException("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        try
        {
            // PROP-4.2: Velopack hooks must run before any other startup work.
            VelopackApp.Build()
                .SetArgs(args)
                .Run();
            PresenceStartupLog.Write("Velopack Run completed");

            // Host /ws requires Bearer when SOULCORE_COMPANION_API_TOKEN is set (phone + desktop).
            // Best-effort early load for repo launches; Velopack installs reload again after
            // LocalUiSettings resolves SoulCoreRepoRoot (see EnsureLocalStackOnOpenAsync).
            var settings = LocalUiSettings.Load();
            CompanionToken.ApplyAllSources(settings.SoulCoreRepoRoot);
            PresenceStartupLog.Write("Starting Avalonia lifetime");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            PresenceStartupLog.Write("Avalonia lifetime ended normally");
        }
        catch (Exception ex)
        {
            PresenceStartupLog.WriteException("Main fatal", ex);
            PresenceStartupLog.ShowFatal(
                "SoulCore CreatorEdition Presence failed to start",
                ex.Message);
            Environment.ExitCode = 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
