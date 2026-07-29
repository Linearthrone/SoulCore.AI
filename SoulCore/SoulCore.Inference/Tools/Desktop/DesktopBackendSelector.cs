using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Selects the desktop backend from <see cref="ToolsOptions.DesktopBackend"/>.
/// Default / unknown → <see cref="NativeDesktopControlBackend"/> (Pass path).
/// <c>hermes</c> → <see cref="HermesDesktopControlBackend"/> (optional stretch).
/// </summary>
public sealed class DesktopBackendSelector : IDesktopControlBackend
{
    private readonly IOptions<ToolsOptions> _options;
    private readonly NativeDesktopControlBackend _native;
    private readonly HermesDesktopControlBackend _hermes;

    public DesktopBackendSelector(
        IOptions<ToolsOptions> options,
        NativeDesktopControlBackend native,
        HermesDesktopControlBackend hermes)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _native = native ?? throw new ArgumentNullException(nameof(native));
        _hermes = hermes ?? throw new ArgumentNullException(nameof(hermes));
    }

    private IDesktopControlBackend Current
        => DesktopToolGate.IsHermesBackend(_options.Value) ? _hermes : _native;

    public Task<DesktopBackendResult> ScreenshotAsync(int monitor, CancellationToken ct = default)
        => Current.ScreenshotAsync(monitor, ct);

    public Task<DesktopBackendResult> ClickAsync(int x, int y, string button, CancellationToken ct = default)
        => Current.ClickAsync(x, y, button, ct);

    public Task<DesktopBackendResult> TypeAsync(string text, CancellationToken ct = default)
        => Current.TypeAsync(text, ct);

    public Task<DesktopBackendResult> KeyAsync(string key, CancellationToken ct = default)
        => Current.KeyAsync(key, ct);

    public Task<DesktopBackendResult> ListWindowsAsync(CancellationToken ct = default)
        => Current.ListWindowsAsync(ct);

    public Task<DesktopBackendResult> FocusWindowAsync(string title, CancellationToken ct = default)
        => Current.FocusWindowAsync(title, ct);
}
