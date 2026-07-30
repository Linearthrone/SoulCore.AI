using SoulCore.Config;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Shared gate messages for browser tools (BED-136). Write/control actions
/// share computer-control opt-in with desktop (BED-135).
/// </summary>
public static class BrowserToolGate
{
    public const string CaptureDenied =
        "browser capture disabled — set Tools.AllowBrowserCapture=true (or enable in Settings → Tools & Access)";

    public const string ControlDenied =
        "browser control requires user authorization — enable AllowComputerControl in Settings → Tools & Access";

    public static bool IsCaptureAllowed(IToolsAccessSettings access)
    {
        ArgumentNullException.ThrowIfNull(access);
        return access.AllowBrowserCapture;
    }

    public static bool IsControlAllowed(IToolsAccessSettings access)
    {
        ArgumentNullException.ThrowIfNull(access);
        return access.AllowComputerControl;
    }

    /// <summary>Config-snapshot overload (tests / cold options).</summary>
    public static bool IsCaptureAllowed(ToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AllowBrowserCapture;
    }

    /// <summary>Config-snapshot overload (tests / cold options).</summary>
    public static bool IsControlAllowed(ToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AllowComputerControl;
    }
}
