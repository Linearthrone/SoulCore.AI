namespace SoulCore.Config;

/// <summary>
/// Non-secret knobs for system/filesystem tools (BED-133) and gated desktop /
/// browser / MT4 tool classes (BED-135/136/138). Filesystem access is gated by
/// whitelisted roots — empty lists disable the filesystem tools entirely
/// (they return <c>Success:false, "filesystem tools disabled"</c>).
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
    /// When false (default), all MT4 read tools refuse with an authorization
    /// message. User must opt in — even reads are gated by default (BED-138).
    /// </summary>
    public bool AllowMt4Read { get; set; }

    /// <summary>
    /// Master write gate for MT4 trade tools (<c>execute_trade</c>,
    /// <c>close_position</c>, <c>run_backtest</c>). When false (default), even
    /// calls with <c>confirmed=true</c> refuse. Per-trade confirmation is an
    /// additional gate on top of this (BED-138).
    /// </summary>
    public bool AllowMt4Trade { get; set; }

    /// <summary>
    /// MT4 backend selector. <c>"hermes"</c> (default) routes through the
    /// <c>house_victoria</c> MCP <c>mt4_*</c> tools via the Hermes gateway
    /// (OPS-143 / BED-144). Native C# MT4 client is out of scope for BED-138.
    /// </summary>
    public string Mt4Backend { get; set; } = "hermes";
}
