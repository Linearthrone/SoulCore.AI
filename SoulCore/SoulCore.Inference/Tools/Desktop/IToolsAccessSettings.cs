namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Session-scoped tool access gates (seeded from <c>Tools</c> config).
/// Mutable at runtime via Settings UI / <c>POST /settings/tools</c> without rewriting appsettings.
/// </summary>
public interface IToolsAccessSettings
{
    bool AllowDesktopCapture { get; }
    bool AllowBrowserCapture { get; }
    bool AllowComputerControl { get; }
    bool AllowMt4Read { get; }
    bool AllowMt4Trade { get; }

    /// <summary>Restore user cursor after Victoria's native click (soft cursor mode).</summary>
    bool SoftCursorRestore { get; }

    /// <summary>Read-only backend label from config (<c>llmod</c> / <c>native</c> / <c>hermes</c>).</summary>
    string DesktopBackend { get; }

    string BrowserBackend { get; }
    string Mt4Backend { get; }

    void SetAllowDesktopCapture(bool enabled);
    void SetAllowBrowserCapture(bool enabled);
    void SetAllowComputerControl(bool enabled);
    void SetAllowMt4Read(bool enabled);
    void SetAllowMt4Trade(bool enabled);
    void SetSoftCursorRestore(bool enabled);
}
