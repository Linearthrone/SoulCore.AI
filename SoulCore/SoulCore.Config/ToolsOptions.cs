namespace SoulCore.Config;

/// <summary>
/// Non-secret knobs for filesystem (BED-133), desktop (BED-135), browser (BED-136),
/// and MT4 (BED-138) tools. Write/control actions require session opt-in gates.
/// </summary>
public sealed class ToolsOptions
{
    public const string SectionName = "Tools";

    public const string BackendHermes = "hermes";
    public const string BackendNative = "native";

    public IReadOnlyList<string> FilesystemRoots { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> FilesystemWriteRoots { get; set; } = Array.Empty<string>();
    public bool UseDefaultRoots { get; set; } = true;

    public bool AllowDesktopCapture { get; set; } = true;
    public bool AllowBrowserCapture { get; set; } = true;
    public bool AllowComputerControl { get; set; }

    /// <summary>Desktop backend: <c>native</c> (default) or <c>hermes</c>.</summary>
    public string DesktopBackend { get; set; } = BackendNative;

    /// <summary>Browser backend: <c>hermes</c> (default). Native not implemented.</summary>
    public string BrowserBackend { get; set; } = BackendHermes;

    /// <summary>When false (default), all MT4 read tools refuse.</summary>
    public bool AllowMt4Read { get; set; }

    /// <summary>When false (default), MT4 trade/close/backtest refuse even if confirmed.</summary>
    public bool AllowMt4Trade { get; set; }

    /// <summary>MT4 backend: <c>hermes</c> (default). Native out of scope.</summary>
    public string Mt4Backend { get; set; } = BackendHermes;
}
