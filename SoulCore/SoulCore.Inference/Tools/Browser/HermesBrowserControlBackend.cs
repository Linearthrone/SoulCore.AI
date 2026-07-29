namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Optional Hermes MCP stretch backend (OPS-143 / BED-144). Not required for BED-136 Pass.
/// Returns an honest failure until MCP <c>browser_bridge</c> is restored.
/// </summary>
public sealed class HermesBrowserControlBackend : IBrowserControlBackend
{
    public const string UnavailableMessage =
        "Hermes MCP browser backend unavailable (OPS-143 browser_bridge MCP not restored). Set Tools:BrowserBackend=native.";

    public Task<BrowserBackendResult> HealthAsync(CancellationToken ct = default)
        => Task.FromResult(new BrowserBackendResult(
            true,
            "browser backend=hermes; MCP browser_bridge not restored",
            new { backend = "hermes", connected = false, hint = UnavailableMessage }));

    public Task<BrowserBackendResult> CaptureTabAsync(int tab, CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<BrowserBackendResult> ClickAsync(int x, int y, CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<BrowserBackendResult> TypeAsync(string text, CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<BrowserBackendResult> KeyAsync(string key, CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<BrowserBackendResult> ScrollAsync(int dx, int dy, CancellationToken ct = default)
        => Task.FromResult(Fail());

    private static BrowserBackendResult Fail()
        => new(false, UnavailableMessage, null);
}
