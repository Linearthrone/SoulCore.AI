namespace SoulCore.Config;

/// <summary>
/// Non-secret knobs for filesystem tools (BED-133) and desktop tools (BED-135).
/// Filesystem access is gated by whitelisted roots — empty lists disable the
/// filesystem tools entirely (they return <c>Success:false, "filesystem tools disabled"</c>).
/// Desktop control tools require a session opt-in gate
/// (<see cref="AllowComputerControl"/> defaults false — never on out of the box).
/// </summary>
public sealed class ToolsOptions
{
    public const string SectionName = "Tools";

    /// <summary>
    /// Roots the filesystem tools (<c>read_file</c>, <c>list_dir</c>) may read from.
    /// Environment variables (e.g. <c>%LOCALAPPDATA%</c>) are expanded at resolution
    /// time. <see cref="FilesystemWriteRoots"/> must be a subset of these. Relative
    /// entries are resolved against the current <c>AppContext.BaseDirectory</c>.
    /// Empty array (default) disables <c>read_file</c> + <c>list_dir</c>.
    /// </summary>
    public IReadOnlyList<string> FilesystemRoots { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Roots <c>write_file</c> may write to — must be a subset of
    /// <see cref="FilesystemRoots"/> (an entry not under a read root is silently
    /// rejected at runtime). Empty array (default) disables <c>write_file</c>.
    /// </summary>
    public IReadOnlyList<string> FilesystemWriteRoots { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Default read roots applied when <see cref="FilesystemRoots"/> is empty
    /// AND <see cref="UseDefaultRoots"/> is true. Defaults to true so a stock
    /// Host exposes the QA scratch space; set false to require explicit config.
    /// The default roots are:
    /// <list type="bullet">
    /// <item><c>%LOCALAPPDATA%/SoulCore/memory/</c> (read-only)</item>
    /// <item><c>SoulCore/scripts/qa-*/</c> (read/write — QA artifacts)</item>
    /// <item><c>SoulCore/scratch/</c> (read/write)</item>
    /// </list>
    /// </summary>
    public bool UseDefaultRoots { get; set; } = true;

    /// <summary>
    /// When true (default), read-only desktop tools
    /// (<c>desktop_screenshot</c>, <c>list_desktop_windows</c>,
    /// <c>focus_desktop_window</c>) may run. Capture is considered safe.
    /// </summary>
    public bool AllowDesktopCapture { get; set; } = true;

    /// <summary>
    /// Session opt-in for write/control desktop tools
    /// (<c>desktop_click</c>, <c>desktop_type</c>, <c>desktop_key</c>).
    /// <b>Defaults false</b> — never on out of the box. Runtime session
    /// overrides go through <c>IComputerControlGate.SetAllowComputerControl</c>
    /// (chat command / settings); config here is the boot default only.
    /// </summary>
    public bool AllowComputerControl { get; set; }

    /// <summary>
    /// Desktop tool backend: <c>native</c> (C# Win32 / GDI fallback) or
    /// <c>hermes</c> (route via Hermes MCP <c>computer_use</c> — full polish in BED-144).
    /// Default <c>native</c> so Host works without the Hermes gateway.
    /// </summary>
    public string DesktopBackend { get; set; } = "native";
}
