using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Selects the browser backend from <see cref="ToolsOptions.BrowserBackend"/>.
/// Default / unknown → <see cref="NativeBrowserControlBackend"/> (Pass path).
/// <c>hermes</c> → <see cref="HermesBrowserControlBackend"/> (optional stretch).
/// </summary>
public sealed class BrowserBackendSelector : IBrowserControlBackend
{
    private readonly IOptions<ToolsOptions> _options;
    private readonly NativeBrowserControlBackend _native;
    private readonly HermesBrowserControlBackend _hermes;

    public BrowserBackendSelector(
        IOptions<ToolsOptions> options,
        NativeBrowserControlBackend native,
        HermesBrowserControlBackend hermes)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _native = native ?? throw new ArgumentNullException(nameof(native));
        _hermes = hermes ?? throw new ArgumentNullException(nameof(hermes));
    }

    private IBrowserControlBackend Current
        => BrowserToolGate.IsHermesBackend(_options.Value) ? _hermes : _native;

    public Task<BrowserBackendResult> HealthAsync(CancellationToken ct = default)
        => Current.HealthAsync(ct);

    public Task<BrowserBackendResult> CaptureTabAsync(int tab, CancellationToken ct = default)
        => Current.CaptureTabAsync(tab, ct);

    public Task<BrowserBackendResult> ClickAsync(int x, int y, CancellationToken ct = default)
        => Current.ClickAsync(x, y, ct);

    public Task<BrowserBackendResult> TypeAsync(string text, CancellationToken ct = default)
        => Current.TypeAsync(text, ct);

    public Task<BrowserBackendResult> KeyAsync(string key, CancellationToken ct = default)
        => Current.KeyAsync(key, ct);

    public Task<BrowserBackendResult> ScrollAsync(int dx, int dy, CancellationToken ct = default)
        => Current.ScrollAsync(dx, dy, ct);
}
