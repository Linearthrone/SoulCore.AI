namespace SoulCore.Config;

/// <summary>
/// Non-secret knobs for system/filesystem tools (BED-133) and computer-use
/// gates (BED-135/136). Filesystem access is gated by whitelisted roots —
/// empty lists disable the filesystem tools entirely (they return
/// <c>Success:false, "filesystem tools disabled"</c>). Desktop/browser write
/// actions require <see cref="AllowComputerControl"/> session opt-in (default
/// false). Native browser backend is out of scope for BED-136 — use
/// <see cref="BrowserBackend"/> = <c>hermes</c>.
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
    /// When true (default), read-only browser tools (<c>browser_health</c>,
    /// <c>browser_capture_tab</c>) may run. Does not authorize click/type/key/scroll.
    /// </summary>
    public bool AllowBrowserCapture { get; set; } = true;

    /// <summary>
    /// Session opt-in for write/control tools shared by desktop (BED-135) and
    /// browser (BED-136): click/type/key/scroll. Default <c>false</c> — never
    /// inject input until the user enables this for the session.
    /// </summary>
    public bool AllowComputerControl { get; set; } = false;

    /// <summary>
    /// Browser tool backend. <c>hermes</c> (default) routes through Hermes MCP
    /// <c>browser_bridge_*</c> (OPS-143 / BED-144). Native C# fallback is not
    /// implemented in BED-136 — unsupported values return a clear error.
    /// </summary>
    public string BrowserBackend { get; set; } = "hermes";
}
