namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Returned when <c>Tools:BrowserBackend</c> is neither <c>native</c> nor <c>hermes</c>.
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
        Content: $"browser backend '{_backend}' is not supported — use Tools.BrowserBackend=native (BrowserCaptureBridge :17891). Hermes browser backend is retired (BED-185).",
        Data: null);
}