namespace SoulCore.Config;

/// <summary>
/// Non-secret knobs for system / filesystem / desktop tools.
/// Filesystem access is gated by whitelisted roots — empty lists disable the
/// filesystem tools entirely (they return <c>Success:false, "filesystem tools disabled"</c>).
/// Desktop write tools (<c>desktop_click</c>/<c>type</c>/<c>key</c>) require
/// <see cref="AllowComputerControl"/> (default false — session opt-in).
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
    /// When true (default), read-only desktop tools may run:
    /// <c>desktop_screenshot</c>, <c>list_desktop_windows</c>,
    /// <c>focus_desktop_window</c>. Does not authorize input injection.
    /// </summary>
    public bool AllowDesktopCapture { get; set; } = true;

    /// <summary>
    /// Session opt-in for desktop <b>write/control</b> tools
    /// (<c>desktop_click</c>, <c>desktop_type</c>, <c>desktop_key</c>).
    /// Default <c>false</c> — must be enabled explicitly (config/env) before
    /// any input injection. Never enable by default.
    /// </summary>
    public bool AllowComputerControl { get; set; }

    /// <summary>
    /// Desktop tool backend: <c>native</c> (C# Win32 / Linux helpers — BED-135
    /// Pass path) or <c>hermes</c> (optional MCP stretch; OPS-143). Default
    /// <c>native</c>.
    /// </summary>
    public string DesktopBackend { get; set; } = "native";
}
