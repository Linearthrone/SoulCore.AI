namespace SoulCore.Config;

/// <summary>
/// Non-secret knobs for filesystem (BED-133), desktop (BED-135), browser (BED-136),
/// and MT4 (BED-138) tools. Session gates seed from these values and remain mutable
/// via <c>/settings/tools</c> for the Host process lifetime.
/// </summary>
public sealed class ToolsOptions
{
    public const string SectionName = "Tools";

    public const string BackendHermes = "hermes";
    public const string BackendLlmod = "llmod";
    /// <summary>Alias for <see cref="BackendLlmod"/> (BED-169).</summary>
    public const string BackendNative = "native";
    /// <summary>Local cua-driver (LLMOD-style agent cursor overlay + background clicks).</summary>
    public const string BackendCua = "cua";

    /// <summary>Default LLMOD MCP HTTP on shadow (Tailscale MagicDNS).</summary>
    /// <summary>Tailscale MagicDNS for shadow (hyphen). Bare <c>housevictoria</c> does not resolve.</summary>
    public const string DefaultLlmodMcpEndpoint = "http://house-victoria:8080";

    public IReadOnlyList<string> FilesystemRoots { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> FilesystemWriteRoots { get; set; } = Array.Empty<string>();
    public bool UseDefaultRoots { get; set; } = true;

    public bool AllowDesktopCapture { get; set; } = true;
    public bool AllowBrowserCapture { get; set; } = true;
    /// <summary>
    /// Desktop/browser write control (click/type/key/open/scroll). Defaults
    /// <c>true</c> (TASK-177) so fresh Host sessions match Settings → Tools &amp; Access
    /// checkboxes; still toggleable per session. Security tradeoff vs prior opt-in.
    /// </summary>
    public bool AllowComputerControl { get; set; } = true;

    /// <summary>
    /// Prefer non-stealing desktop input. With <c>DesktopBackend=cua</c>: agent overlay +
    /// <c>delivery_mode=background</c> (OS mouse never moves — same as LLMOD).
    /// With <c>native</c>: PostMessage click first, else SetCursorPos+restore.
    /// </summary>
    public bool SoftCursorRestore { get; set; } = true;

    /// <summary>Desktop backend: <c>cua</c> (default when installed), <c>native</c>, or <c>hermes</c>.</summary>
    public string DesktopBackend { get; set; } = BackendCua;

    /// <summary>
    /// When non-empty, desktop tools are hard-scoped to windows whose title contains
    /// this substring (case-insensitive), e.g. <c>victoria-sandbox</c> for
    /// <c>victoria-sandbox [Running] - Oracle VirtualBox</c>. Clicks/drags/scrolls
    /// outside that window are refused; <c>desktop_open_app</c> on the host is blocked;
    /// <c>list_desktop_windows</c> only returns matching windows. Empty = unrestricted.
    /// </summary>
    public string DesktopTargetWindowTitle { get; set; } = "";

    /// <summary>
    /// Browser backend: <c>playwright</c> (BED-195 — Victoria dedicated Chromium),
    /// <c>native</c> (BrowserCaptureBridge :17891), or legacy. When
    /// <c>playwright</c>, Host prefers it even if DesktopTargetWindowTitle is set
    /// (VM stays for desktop_*; web Login uses Playwright).
    /// </summary>
    public string BrowserBackend { get; set; } = BackendNative;

    public const string BackendPlaywright = "playwright";

    /// <summary>
    /// Victoria-only Chromium profile directory. Must NOT be Kurt's Chrome/Edge profile.
    /// Default: %LOCALAPPDATA%\SoulCore\victoria-browser
    /// </summary>
    public string PlaywrightUserDataDir { get; set; } = "";

    /// <summary>When true, launch headed Chromium for debugging (stream still preferred for Kurt).</summary>
    public bool PlaywrightHeaded { get; set; }

    /// <summary>Loopback base URL for native browser capture bridge (default :17891).</summary>
    public string BrowserBridgeUrl { get; set; } = "http://127.0.0.1:17891";

    /// <summary>Alias for bridge code (same value as <see cref="BrowserBridgeUrl"/>).</summary>
    public const string DefaultBrowserBridgeBaseUrl = "http://127.0.0.1:17891/";

    /// <summary>Preferred name in bridge code; mirrors <see cref="BrowserBridgeUrl"/>.</summary>
    public string BrowserBridgeBaseUrl
    {
        get => BrowserBridgeUrl;
        set => BrowserBridgeUrl = value ?? "";
    }

    /// <summary>When false (default), all MT4 read tools refuse.</summary>
    public bool AllowMt4Read { get; set; }

    /// <summary>When false (default), MT4 trade/close/backtest refuse even if confirmed.</summary>
    public bool AllowMt4Trade { get; set; }

    /// <summary>When false (default), email list/read/search/file/mark refuse.</summary>
    public bool AllowEmailRead { get; set; }

    /// <summary>When false (default), email send/reply refuse even if confirmed.</summary>
    public bool AllowEmailSend { get; set; }

    /// <summary>When false (default), email delete refuse even if confirmed.</summary>
    public bool AllowEmailDelete { get; set; }

    /// <summary>
    /// MT4 backend: <c>llmod</c> (default) → LLMOD MCP HTTP on shadow;
    /// <c>hermes</c> → Hermes gateway fallback; <c>native</c> aliases <c>llmod</c>.
    /// </summary>
    public string Mt4Backend { get; set; } = BackendLlmod;

    /// <summary>LLMOD MCP HTTP base URL when <see cref="Mt4Backend"/> is llmod/native.</summary>
    public string LlmodMcpEndpoint { get; set; } = DefaultLlmodMcpEndpoint;
}
