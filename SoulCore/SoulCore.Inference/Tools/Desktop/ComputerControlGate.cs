using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Default <see cref="IComputerControlGate"/> / <see cref="IToolsAccessSettings"/>.
/// Seeds from <see cref="ToolsOptions"/> at construction; gates may be toggled per
/// Host process (session) without mutating the bound options object.
/// </summary>
public sealed class ComputerControlGate : IComputerControlGate, IToolsAccessSettings
{
    private int _allowDesktopCapture;
    private int _allowBrowserCapture;
    private int _allowControl;
    private int _allowMt4Read;
    private int _allowMt4Trade;
    private int _softCursorRestore;

    public ComputerControlGate(IOptions<ToolsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var opts = options.Value ?? new ToolsOptions();
        _allowDesktopCapture = opts.AllowDesktopCapture ? 1 : 0;
        _allowBrowserCapture = opts.AllowBrowserCapture ? 1 : 0;
        _allowControl = opts.AllowComputerControl ? 1 : 0;
        _allowMt4Read = opts.AllowMt4Read ? 1 : 0;
        _allowMt4Trade = opts.AllowMt4Trade ? 1 : 0;
        _softCursorRestore = opts.SoftCursorRestore ? 1 : 0;

        var desktop = string.IsNullOrWhiteSpace(opts.DesktopBackend)
            ? ToolsOptions.BackendCua
            : opts.DesktopBackend.Trim();
        var browser = string.IsNullOrWhiteSpace(opts.BrowserBackend)
            ? "none"
            : opts.BrowserBackend.Trim();
        var mt4 = string.IsNullOrWhiteSpace(opts.Mt4Backend)
            ? ToolsOptions.BackendLlmod
            : opts.Mt4Backend.Trim();

        // BED-185: never keep hermes backends live.
        if (string.Equals(desktop, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase))
            desktop = ToolsOptions.BackendCua;
        if (string.Equals(browser, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase))
            browser = "none";
        if (string.Equals(mt4, ToolsOptions.BackendHermes, StringComparison.OrdinalIgnoreCase))
            mt4 = ToolsOptions.BackendLlmod;

        DesktopBackend = desktop;
        BrowserBackend = browser;
        Mt4Backend = mt4;
    }

    /// <summary>Test ctor — bypasses options binding.</summary>
    public ComputerControlGate(bool allowDesktopCapture, bool allowComputerControl)
        : this(
            allowDesktopCapture,
            allowBrowserCapture: true,
            allowComputerControl,
            allowMt4Read: false,
            allowMt4Trade: false)
    {
    }

    /// <summary>Test ctor — full gate set.</summary>
    public ComputerControlGate(
        bool allowDesktopCapture,
        bool allowBrowserCapture,
        bool allowComputerControl,
        bool allowMt4Read,
        bool allowMt4Trade,
        string desktopBackend = ToolsOptions.BackendNative,
        string browserBackend = "none",
        string mt4Backend = ToolsOptions.BackendLlmod,
        bool softCursorRestore = true)
    {
        _allowDesktopCapture = allowDesktopCapture ? 1 : 0;
        _allowBrowserCapture = allowBrowserCapture ? 1 : 0;
        _allowControl = allowComputerControl ? 1 : 0;
        _allowMt4Read = allowMt4Read ? 1 : 0;
        _allowMt4Trade = allowMt4Trade ? 1 : 0;
        _softCursorRestore = softCursorRestore ? 1 : 0;
        DesktopBackend = desktopBackend;
        BrowserBackend = browserBackend;
        Mt4Backend = mt4Backend;
    }

    public bool AllowDesktopCapture => Read(ref _allowDesktopCapture);
    public bool AllowBrowserCapture => Read(ref _allowBrowserCapture);
    public bool AllowComputerControl => Read(ref _allowControl);
    public bool AllowMt4Read => Read(ref _allowMt4Read);
    public bool AllowMt4Trade => Read(ref _allowMt4Trade);
    public bool SoftCursorRestore => Read(ref _softCursorRestore);

    public string DesktopBackend { get; }
    public string BrowserBackend { get; }
    public string Mt4Backend { get; }

    public void SetAllowDesktopCapture(bool enabled) => Write(ref _allowDesktopCapture, enabled);
    public void SetAllowBrowserCapture(bool enabled) => Write(ref _allowBrowserCapture, enabled);
    public void SetAllowComputerControl(bool enabled) => Write(ref _allowControl, enabled);
    public void SetAllowMt4Read(bool enabled) => Write(ref _allowMt4Read, enabled);
    public void SetAllowMt4Trade(bool enabled) => Write(ref _allowMt4Trade, enabled);
    public void SetSoftCursorRestore(bool enabled) => Write(ref _softCursorRestore, enabled);

    private static bool Read(ref int flag) =>
        Interlocked.CompareExchange(ref flag, 0, 0) == 1;

    private static void Write(ref int flag, bool enabled) =>
        Interlocked.Exchange(ref flag, enabled ? 1 : 0);
}
