using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Gate messages and checks for BED-136 browser tools.
/// Control tools refuse when <see cref="ToolsOptions.AllowComputerControl"/> is false;
/// health/capture refuse when <see cref="ToolsOptions.AllowBrowserCapture"/> is false.
/// </summary>
public static class BrowserToolGate
{
    public const string ControlDeniedMessage =
        "browser control requires user authorization — ask the user to enable AllowComputerControl";

    public const string CaptureDeniedMessage =
        "browser capture requires AllowBrowserCapture=true";

    public static bool IsCaptureAllowed(ToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AllowBrowserCapture;
    }

    public static bool IsControlAllowed(ToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AllowComputerControl;
    }

    /// <summary>
    /// Normalize <see cref="ToolsOptions.BrowserBackend"/> — unknown values fall back to native.
    /// </summary>
    public static bool IsHermesBackend(ToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return string.Equals(options.BrowserBackend, "hermes", StringComparison.OrdinalIgnoreCase);
    }
}
