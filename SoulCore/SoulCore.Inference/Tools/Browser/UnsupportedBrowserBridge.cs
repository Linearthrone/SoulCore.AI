namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Returned when <c>Tools:BrowserBackend</c> is not <c>hermes</c>. Native C#
/// browser automation is out of scope for BED-136.
/// </summary>
public sealed class UnsupportedBrowserBridge : IBrowserBridge
{
    private readonly string _backend;

    public UnsupportedBrowserBridge(string backend)
    {
        _backend = string.IsNullOrWhiteSpace(backend) ? "(empty)" : backend.Trim();
    }

    public string BackendName => _backend;

    public Task<BrowserBridgeResult> HealthAsync(CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<BrowserBridgeResult> CaptureTabAsync(int tab, CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<BrowserBridgeResult> ClickAsync(int x, int y, CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<BrowserBridgeResult> TypeAsync(string text, CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<BrowserBridgeResult> KeyAsync(string key, CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<BrowserBridgeResult> ScrollAsync(int dx, int dy, CancellationToken ct = default)
        => Task.FromResult(Fail());

    private BrowserBridgeResult Fail() => new(
        Success: false,
        Content: $"browser backend '{_backend}' is unavailable — Hermes is retired (BED-185). Open sites with desktop_open_app (chrome/edge + URL args).",
        Data: null);
}