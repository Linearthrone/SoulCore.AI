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
    private int _allowEmailRead;
    private int _allowEmailSend;
    private int _allowEmailDelete;
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
        _allowEmailRead = opts.AllowEmailRead ? 1 : 0;
        _allowEmailSend = opts.AllowEmailSend ? 1 : 0;
        _allowEmailDelete = opts.AllowEmailDelete ? 1 : 0;
        _softCursorRestore = opts.SoftCursorRestore ? 1 : 0;
            DesktopBackend = string.IsNullOrWhiteSpace(opts.DesktopBackend)
            ? ToolsOptions.BackendCua
            : opts.DesktopBackend.Trim();
        BrowserBackend = string.IsNullOrWhiteSpace(opts.BrowserBackend)
            ? ToolsOptions.BackendNative
            : opts.BrowserBackend.Trim();
        Mt4Backend = string.IsNullOrWhiteSpace(opts.Mt4Backend)
            ? ToolsOptions.BackendLlmod
            : opts.Mt4Backend.Trim();
        DesktopTargetWindowTitle = (opts.DesktopTargetWindowTitle ?? string.Empty).Trim();
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
        string browserBackend = ToolsOptions.BackendNative,
        string mt4Backend = ToolsOptions.BackendLlmod,
        bool softCursorRestore = true,
        string desktopTargetWindowTitle = "",
        bool allowEmailRead = false,
        bool allowEmailSend = false,
        bool allowEmailDelete = false)
    {
        _allowDesktopCapture = allowDesktopCapture ? 1 : 0;
        _allowBrowserCapture = allowBrowserCapture ? 1 : 0;
        _allowControl = allowComputerControl ? 1 : 0;
        _allowMt4Read = allowMt4Read ? 1 : 0;
        _allowMt4Trade = allowMt4Trade ? 1 : 0;
        _allowEmailRead = allowEmailRead ? 1 : 0;
        _allowEmailSend = allowEmailSend ? 1 : 0;
        _allowEmailDelete = allowEmailDelete ? 1 : 0;
        _softCursorRestore = softCursorRestore ? 1 : 0;
        DesktopBackend = desktopBackend;
        BrowserBackend = browserBackend;
        Mt4Backend = mt4Backend;
        DesktopTargetWindowTitle = (desktopTargetWindowTitle ?? string.Empty).Trim();
    }

    public bool AllowDesktopCapture => Read(ref _allowDesktopCapture);
    public bool AllowBrowserCapture => Read(ref _allowBrowserCapture);
    public bool AllowComputerControl => Read(ref _allowControl);
    public bool AllowMt4Read => Read(ref _allowMt4Read);
    public bool AllowMt4Trade => Read(ref _allowMt4Trade);
    public bool AllowEmailRead => Read(ref _allowEmailRead);
    public bool AllowEmailSend => Read(ref _allowEmailSend);
    public bool AllowEmailDelete => Read(ref _allowEmailDelete);
    public bool SoftCursorRestore => Read(ref _softCursorRestore);

    public string DesktopBackend { get; }
    public string BrowserBackend { get; }
    public string Mt4Backend { get; }

    /// <summary>Substring match for VM/window scope; empty = unrestricted.</summary>
    public string DesktopTargetWindowTitle { get; }

    public void SetAllowDesktopCapture(bool enabled) => Write(ref _allowDesktopCapture, enabled);
    public void SetAllowBrowserCapture(bool enabled) => Write(ref _allowBrowserCapture, enabled);
    public void SetAllowComputerControl(bool enabled) => Write(ref _allowControl, enabled);
    public void SetAllowMt4Read(bool enabled) => Write(ref _allowMt4Read, enabled);
    public void SetAllowMt4Trade(bool enabled) => Write(ref _allowMt4Trade, enabled);
    public void SetAllowEmailRead(bool enabled) => Write(ref _allowEmailRead, enabled);
    public void SetAllowEmailSend(bool enabled) => Write(ref _allowEmailSend, enabled);
    public void SetAllowEmailDelete(bool enabled) => Write(ref _allowEmailDelete, enabled);
    public void SetSoftCursorRestore(bool enabled) => Write(ref _softCursorRestore, enabled);

    private static bool Read(ref int flag) =>
        Interlocked.CompareExchange(ref flag, 0, 0) == 1;

    private static void Write(ref int flag, bool enabled) =>
        Interlocked.Exchange(ref flag, enabled ? 1 : 0);
}
