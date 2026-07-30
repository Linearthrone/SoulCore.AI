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

    public ComputerControlGate(IOptions<ToolsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var opts = options.Value ?? new ToolsOptions();
        _allowDesktopCapture = opts.AllowDesktopCapture ? 1 : 0;
        _allowBrowserCapture = opts.AllowBrowserCapture ? 1 : 0;
        _allowControl = opts.AllowComputerControl ? 1 : 0;
        _allowMt4Read = opts.AllowMt4Read ? 1 : 0;
        _allowMt4Trade = opts.AllowMt4Trade ? 1 : 0;
        DesktopBackend = string.IsNullOrWhiteSpace(opts.DesktopBackend)
            ? ToolsOptions.BackendNative
            : opts.DesktopBackend.Trim();
        BrowserBackend = string.IsNullOrWhiteSpace(opts.BrowserBackend)
            ? ToolsOptions.BackendHermes
            : opts.BrowserBackend.Trim();
        Mt4Backend = string.IsNullOrWhiteSpace(opts.Mt4Backend)
            ? ToolsOptions.BackendHermes
            : opts.Mt4Backend.Trim();
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
        string browserBackend = ToolsOptions.BackendHermes,
        string mt4Backend = ToolsOptions.BackendHermes)
    {
        _allowDesktopCapture = allowDesktopCapture ? 1 : 0;
        _allowBrowserCapture = allowBrowserCapture ? 1 : 0;
        _allowControl = allowComputerControl ? 1 : 0;
        _allowMt4Read = allowMt4Read ? 1 : 0;
        _allowMt4Trade = allowMt4Trade ? 1 : 0;
        DesktopBackend = desktopBackend;
        BrowserBackend = browserBackend;
        Mt4Backend = mt4Backend;
    }

    public bool AllowDesktopCapture => Read(ref _allowDesktopCapture);
    public bool AllowBrowserCapture => Read(ref _allowBrowserCapture);
    public bool AllowComputerControl => Read(ref _allowControl);
    public bool AllowMt4Read => Read(ref _allowMt4Read);
    public bool AllowMt4Trade => Read(ref _allowMt4Trade);

    public string DesktopBackend { get; }
    public string BrowserBackend { get; }
    public string Mt4Backend { get; }

    public void SetAllowDesktopCapture(bool enabled) => Write(ref _allowDesktopCapture, enabled);
    public void SetAllowBrowserCapture(bool enabled) => Write(ref _allowBrowserCapture, enabled);
    public void SetAllowComputerControl(bool enabled) => Write(ref _allowControl, enabled);
    public void SetAllowMt4Read(bool enabled) => Write(ref _allowMt4Read, enabled);
    public void SetAllowMt4Trade(bool enabled) => Write(ref _allowMt4Trade, enabled);

    private static bool Read(ref int flag) =>
        Interlocked.CompareExchange(ref flag, 0, 0) == 1;

    private static void Write(ref int flag, bool enabled) =>
        Interlocked.Exchange(ref flag, enabled ? 1 : 0);
}
