namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Session gate for desktop (and later browser) control tools (BED-135).
/// Capture defaults on; control defaults off and requires an explicit session
/// opt-in via <see cref="SetAllowComputerControl"/>.
/// </summary>
public interface IComputerControlGate
{
    /// <summary>Read-only capture / window list/focus allowed.</summary>
    bool AllowDesktopCapture { get; }

    /// <summary>Write/control (click/type/key) allowed for this session.</summary>
    bool AllowComputerControl { get; }

    /// <summary>
    /// Per-session toggle. Does not persist across Host restarts.
    /// Callers (settings UI / chat command) set this after user consent.
    /// </summary>
    void SetAllowComputerControl(bool enabled);
}
