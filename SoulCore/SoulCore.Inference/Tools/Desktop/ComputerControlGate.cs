using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Default <see cref="IComputerControlGate"/>. Seeds from
/// <see cref="ToolsOptions"/> at construction; control may be toggled per
/// session without mutating the bound options object.
/// </summary>
public sealed class ComputerControlGate : IComputerControlGate
{
    private readonly bool _allowCapture;
    private int _allowControl; // 0/1 for Interlocked

    public ComputerControlGate(IOptions<ToolsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var opts = options.Value ?? new ToolsOptions();
        _allowCapture = opts.AllowDesktopCapture;
        _allowControl = opts.AllowComputerControl ? 1 : 0;
    }

    /// <summary>Test ctor — bypasses options binding.</summary>
    public ComputerControlGate(bool allowDesktopCapture, bool allowComputerControl)
    {
        _allowCapture = allowDesktopCapture;
        _allowControl = allowComputerControl ? 1 : 0;
    }

    public bool AllowDesktopCapture => _allowCapture;

    public bool AllowComputerControl =>
        Interlocked.CompareExchange(ref _allowControl, 0, 0) == 1;

    public void SetAllowComputerControl(bool enabled)
        => Interlocked.Exchange(ref _allowControl, enabled ? 1 : 0);
}
