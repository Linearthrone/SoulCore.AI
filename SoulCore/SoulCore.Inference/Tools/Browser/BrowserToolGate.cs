using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Shared gate messages for browser tools (BED-136). Write/control actions
/// share <see cref="ToolsOptions.AllowComputerControl"/> with desktop (BED-135).
/// </summary>
public static class BrowserToolGate
{
    public const string CaptureDenied =
        "browser capture disabled — set Tools.AllowBrowserCapture=true";

    public const string ControlDenied =
        "browser control requires user authorization — ask the user to enable AllowComputerControl";

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
}
