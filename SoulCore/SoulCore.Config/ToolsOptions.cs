namespace SoulCore.Config;

/// <summary>
/// Non-secret knobs for filesystem (BED-133) + desktop/browser/trading
/// (BED-135/136/138/144) tools. Filesystem access is gated by whitelisted
/// roots — empty lists disable the filesystem tools entirely (they return
/// <c>Success:false, "filesystem tools disabled"</c>).
/// </summary>
public sealed class ToolsOptions
{
    public const string SectionName = "Tools";

    /// <summary>Canonical backend token for Hermes MCP routing (BED-144).</summary>
    public const string BackendHermes = "hermes";

    /// <summary>Canonical backend token for native C# fallbacks.</summary>
    public const string BackendNative = "native";

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

    // ---- Desktop (BED-135 / BED-144) ----

    /// <summary>Allow read-only desktop capture (<c>desktop_screenshot</c>, window list/focus).</summary>
    public bool AllowDesktopCapture { get; set; } = true;

    /// <summary>
    /// Session opt-in for desktop/browser input injection (click/type/key/scroll).
    /// Default <c>false</c> — never on out of the box.
    /// </summary>
    public bool AllowComputerControl { get; set; } = false;

    /// <summary>
    /// <c>hermes</c> → route via <c>IHermesMcpInvoker</c> / MCP <c>computer_use</c> family;
    /// <c>native</c> → C# fallback (may be unimplemented). No silent cross-fallback when
    /// set to <c>hermes</c> and the gateway is down.
    /// </summary>
    public string DesktopBackend { get; set; } = BackendHermes;

    // ---- Browser (BED-136 / BED-144) ----

    /// <summary>Allow read-only browser capture (<c>browser_health</c>, <c>browser_capture_tab</c>).</summary>
    public bool AllowBrowserCapture { get; set; } = true;

    /// <summary>
    /// <c>hermes</c> → <c>browser_bridge_*</c> MCP; <c>native</c> → C# fallback.
    /// </summary>
    public string BrowserBackend { get; set; } = BackendHermes;

    // ---- MT4 / trading (BED-138 / BED-144) ----

    /// <summary>Allow MT4 read tools. Default <c>false</c> — even reads are opt-in.</summary>
    public bool AllowMt4Read { get; set; } = false;

    /// <summary>
    /// Master gate for MT4 write tools (<c>execute_trade</c>, <c>close_position</c>,
    /// <c>run_backtest</c>). Even with <c>confirmed=true</c>, trade tools refuse when false.
    /// </summary>
    public bool AllowMt4Trade { get; set; } = false;

    /// <summary>
    /// <c>hermes</c> → <c>mt4_*</c> MCP; <c>native</c> → C# fallback.
    /// Per-trade confirmation is enforced on <b>both</b> backends before dispatch.
    /// </summary>
    public string Mt4Backend { get; set; } = BackendHermes;
}
