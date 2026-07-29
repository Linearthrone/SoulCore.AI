using SoulCore.Config;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Gate messages and checks for BED-135 desktop tools.
/// Control tools refuse when <see cref="ToolsOptions.AllowComputerControl"/> is false;
/// capture/list/focus refuse when <see cref="ToolsOptions.AllowDesktopCapture"/> is false.
/// </summary>
public static class DesktopToolGate
{
    public const string ControlDeniedMessage =
        "desktop control requires user authorization — ask the user to enable AllowComputerControl";

    public const string CaptureDeniedMessage =
        "desktop capture requires AllowDesktopCapture=true";

    public static bool IsCaptureAllowed(ToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AllowDesktopCapture;
    }

    public static bool IsControlAllowed(ToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AllowComputerControl;
    }

    /// <summary>
    /// Normalize <see cref="ToolsOptions.DesktopBackend"/> — unknown values fall back to native.
    /// </summary>
    public static bool IsHermesBackend(ToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return string.Equals(options.DesktopBackend, "hermes", StringComparison.OrdinalIgnoreCase);
    }
}
