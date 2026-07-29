using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Native C# browser backend (BED-136 Pass path — required; TT-159 Avenue C).
/// Drives Chromium via loopback Chrome DevTools Protocol
/// (<see cref="ToolsOptions.BrowserCdpUrl"/>, default <c>http://127.0.0.1:9222</c>).
/// When CDP is unreachable, returns honest <c>Success:false</c> (except health,
/// which always reports status). Hermes MCP is a separate optional stretch backend.
/// </summary>
public sealed class NativeBrowserControlBackend : IBrowserControlBackend
{
    public const string BridgeUnavailableMessage =
        "browser bridge unavailable — start Chrome/Chromium with --remote-debugging-port=9222 "
        + "(loopback only), or set Tools:BrowserBackend=hermes when OPS-143 browser_bridge MCP is restored";

    private readonly IOptions<ToolsOptions> _options;
    private readonly string _captureDirectory;

    public NativeBrowserControlBackend(IOptions<ToolsOptions> options)
        : this(options, ResolveDefaultCaptureDirectory())
    {
    }

    /// <summary>Test / override ctor — writes captures under <paramref name="captureDirectory"/>.</summary>
    public NativeBrowserControlBackend(IOptions<ToolsOptions> options, string captureDirectory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(captureDirectory);
        _captureDirectory = Path.GetFullPath(captureDirectory);
        Directory.CreateDirectory(_captureDirectory);
    }

    public async Task<BrowserBackendResult> HealthAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!ChromeCdpClient.TryValidateLoopbackBase(_options.Value.BrowserCdpUrl, out var baseUri, out var err))
        {
            return Ok("browser backend=native; CDP config invalid", new
            {
                backend = "native",
                connected = false,
                cdpUrl = _options.Value.BrowserCdpUrl,
                error = err,
            });
        }

        var (ok, msg, targets) = await ChromeCdpClient.ListTargetsAsync(baseUri, ct).ConfigureAwait(false);
        return Ok(
            ok
                ? $"browser backend=native; CDP connected — {msg}"
                : $"browser backend=native; CDP not connected — {msg}",
            new
            {
                backend = "native",
                connected = ok,
                cdpUrl = baseUri.ToString(),
                pageCount = targets.Count,
                pages = targets.Select(t => new { t.Title, t.Url }).ToArray(),
                hint = ok ? null : BridgeUnavailableMessage,
            });
    }

    public async Task<BrowserBackendResult> CaptureTabAsync(int tab, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (tab < 0)
            return Fail("tab must be >= 0");

        if (!TryResolveBase(out var baseUri, out var fail))
            return fail!;

        var (ok, msg, path, dom, title, url) = await ChromeCdpClient
            .CaptureAsync(baseUri, tab, _captureDirectory, ct)
            .ConfigureAwait(false);
        if (!ok)
            return Fail(string.IsNullOrWhiteSpace(msg) ? BridgeUnavailableMessage : msg);

        return Ok(msg, new
        {
            path,
            tab,
            title,
            url,
            dom,
            bytes = path is not null && File.Exists(path) ? new FileInfo(path).Length : 0L,
            platform = "cdp",
            format = "png",
        });
    }

    public async Task<BrowserBackendResult> ClickAsync(int x, int y, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!TryResolveBase(out var baseUri, out var fail))
            return fail!;

        var (ok, msg) = await ChromeCdpClient.ClickAsync(baseUri, tab: 0, x, y, ct).ConfigureAwait(false);
        return ok
            ? Ok(msg, new { x, y, platform = "cdp" })
            : Fail(msg);
    }

    public async Task<BrowserBackendResult> TypeAsync(string text, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
            return Fail("text must be non-empty");

        if (!TryResolveBase(out var baseUri, out var fail))
            return fail!;

        var (ok, msg) = await ChromeCdpClient.TypeAsync(baseUri, tab: 0, text, ct).ConfigureAwait(false);
        return ok
            ? Ok(msg, new { length = text.Length, platform = "cdp" })
            : Fail(msg);
    }

    public async Task<BrowserBackendResult> KeyAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(key))
            return Fail("key must be non-empty");

        if (!TryResolveBase(out var baseUri, out var fail))
            return fail!;

        var (ok, msg) = await ChromeCdpClient.KeyAsync(baseUri, tab: 0, key, ct).ConfigureAwait(false);
        return ok
            ? Ok(msg, new { key = key.Trim(), platform = "cdp" })
            : Fail(msg);
    }

    public async Task<BrowserBackendResult> ScrollAsync(int dx, int dy, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!TryResolveBase(out var baseUri, out var fail))
            return fail!;

        var (ok, msg) = await ChromeCdpClient.ScrollAsync(baseUri, tab: 0, dx, dy, ct).ConfigureAwait(false);
        return ok
            ? Ok(msg, new { dx, dy, platform = "cdp" })
            : Fail(msg);
    }

    private bool TryResolveBase(out Uri baseUri, out BrowserBackendResult? fail)
    {
        if (!ChromeCdpClient.TryValidateLoopbackBase(_options.Value.BrowserCdpUrl, out baseUri!, out var err))
        {
            fail = Fail(err);
            return false;
        }

        fail = null;
        return true;
    }

    private static string ResolveDefaultCaptureDirectory()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
            local = Path.GetTempPath();
        return Path.Combine(local, "SoulCore", "browser-captures");
    }

    private static BrowserBackendResult Ok(string message, object? data = null)
        => new(true, message, data);

    private static BrowserBackendResult Fail(string message)
        => new(false, message, null);
}
