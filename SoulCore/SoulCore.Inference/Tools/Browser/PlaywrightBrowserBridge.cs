using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// BED-195 Avenue A1: Host Playwright Chromium with Victoria-only user-data-dir.
/// Never attaches to Kurt's daily Chrome profile.
/// </summary>
public sealed class PlaywrightBrowserBridge : IBrowserBridge, IAsyncDisposable
{
    public const string BackendId = "playwright";

    private readonly IOptions<ToolsOptions> _opts;
    private readonly ILogger<PlaywrightBrowserBridge>? _log;
    private readonly IVictoriaBrowserViewHub? _view;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool _disposed;

    public PlaywrightBrowserBridge(
        IOptions<ToolsOptions> opts,
        ILogger<PlaywrightBrowserBridge>? log = null,
        IVictoriaBrowserViewHub? view = null)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _log = log;
        _view = view;
    }

    public string BackendName => BackendId;

    public static string ResolveUserDataDir(ToolsOptions opts)
    {
        var configured = (opts.PlaywrightUserDataDir ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
            local = Path.GetTempPath();
        return Path.Combine(local, "SoulCore", "victoria-browser");
    }

    public async Task<BrowserBridgeResult> HealthAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsurePageAsync(ct).ConfigureAwait(false);
            var url = _page?.Url ?? "";
            var title = _page is null ? "" : await _page.TitleAsync().ConfigureAwait(false);
            return new BrowserBridgeResult(
                true,
                $"playwright ok: Victoria dedicated Chromium (not Kurt's Chrome). url={url} title={title}",
                new { backend = BackendId, url, title, profile = ResolveUserDataDir(_opts.Value) });
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Playwright health failed");
            return new BrowserBridgeResult(
                false,
                "playwright not ready: " + ex.Message +
                ". Run: pwsh SoulCore/scripts/install-playwright.ps1 (or `playwright install chromium`).",
                null);
        }
    }

    public async Task<BrowserBridgeResult> CaptureTabAsync(int tab, CancellationToken ct = default)
    {
        _ = tab;
        return await SnapshotVisualAsync("capture_tab", ct).ConfigureAwait(false);
    }

    public async Task<BrowserBridgeResult> ClickAsync(int x, int y, CancellationToken ct = default)
    {
        try
        {
            var page = await EnsurePageAsync(ct).ConfigureAwait(false);
            await page.Mouse.ClickAsync(x, y).ConfigureAwait(false);
            await PublishFrameAsync(page, $"click ({x},{y})", ct).ConfigureAwait(false);
            return new BrowserBridgeResult(
                true,
                $"playwright click at ({x},{y}). url={page.Url}",
                new { x, y, url = page.Url, backend = BackendId, action_ok = true, goal_complete = false });
        }
        catch (Exception ex)
        {
            return Fail("click", ex);
        }
    }

    public async Task<BrowserBridgeResult> TypeAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var page = await EnsurePageAsync(ct).ConfigureAwait(false);
            await page.Keyboard.TypeAsync(text ?? "").ConfigureAwait(false);
            await PublishFrameAsync(page, $"type {text?.Length ?? 0} chars", ct).ConfigureAwait(false);
            return new BrowserBridgeResult(
                true,
                $"playwright typed {text?.Length ?? 0} chars (value redacted in logs).",
                BrowserResultHonesty.FillRedacted("(focused)", text?.Length ?? 0, BackendId));
        }
        catch (Exception ex)
        {
            return Fail("type", ex);
        }
    }

    public async Task<BrowserBridgeResult> KeyAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var page = await EnsurePageAsync(ct).ConfigureAwait(false);
            await page.Keyboard.PressAsync(key ?? "Enter").ConfigureAwait(false);
            await PublishFrameAsync(page, $"key {key}", ct).ConfigureAwait(false);
            return new BrowserBridgeResult(true, $"playwright key '{key}'. url={page.Url}",
                new { key, url = page.Url, backend = BackendId, action_ok = true, goal_complete = false });
        }
        catch (Exception ex)
        {
            return Fail("key", ex);
        }
    }

    public async Task<BrowserBridgeResult> ScrollAsync(int dx, int dy, CancellationToken ct = default)
    {
        try
        {
            var page = await EnsurePageAsync(ct).ConfigureAwait(false);
            await page.Mouse.WheelAsync(dx, dy).ConfigureAwait(false);
            await PublishFrameAsync(page, $"scroll dx={dx} dy={dy}", ct).ConfigureAwait(false);
            return new BrowserBridgeResult(true, $"playwright scroll dx={dx} dy={dy}",
                new { dx, dy, backend = BackendId, action_ok = true, goal_complete = false });
        }
        catch (Exception ex)
        {
            return Fail("scroll", ex);
        }
    }

    public async Task<BrowserBridgeResult> NavigateAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new BrowserBridgeResult(false, "browser_navigate needs an http(s) URL.", null);
        }

        try
        {
            var page = await EnsurePageAsync(ct).ConfigureAwait(false);
            var resp = await page.GotoAsync(uri.AbsoluteUri, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30_000
            }).ConfigureAwait(false);

            if (resp is null)
            {
                return new BrowserBridgeResult(
                    false,
                    $"playwright navigate to {uri} returned no response (load not verified).",
                    BrowserResultHonesty.LaunchOnly(uri.AbsoluteUri, BackendId));
            }

            if (!resp.Ok && resp.Status is >= 400)
            {
                return new BrowserBridgeResult(
                    false,
                    $"playwright navigate HTTP {resp.Status} for {uri}.",
                    new { url = uri.AbsoluteUri, status = resp.Status, action_ok = false, goal_complete = false, backend = BackendId });
            }

            var title = await page.TitleAsync().ConfigureAwait(false);
            await PublishFrameAsync(page, $"navigate {uri.Host}", ct).ConfigureAwait(false);
            return new BrowserBridgeResult(
                true,
                $"playwright loaded {page.Url} title='{title}'. goal_complete=false (login/forms still need click/fill).",
                BrowserResultHonesty.Navigated(page.Url, title, BackendId, goalComplete: false));
        }
        catch (Exception ex)
        {
            return Fail("navigate", ex);
        }
    }

    public async Task<BrowserBridgeResult> SnapshotAsync(string? query = null, CancellationToken ct = default)
    {
        try
        {
            var page = await EnsurePageAsync(ct).ConfigureAwait(false);
            // Prefer AriaSnapshot (string) — Accessibility.SnapshotAsync is deprecated.
            var raw = await page.Locator("body").AriaSnapshotAsync().ConfigureAwait(false);
            var text = FormatA11yText(raw, query);
            await PublishFrameAsync(page, "snapshot", ct).ConfigureAwait(false);
            return new BrowserBridgeResult(
                true,
                $"playwright a11y snapshot url={page.Url}\n{text}",
                new
                {
                    url = page.Url,
                    title = await page.TitleAsync().ConfigureAwait(false),
                    backend = BackendId,
                    action_ok = true,
                    goal_complete = false,
                    degraded = false,
                    locator = "a11y"
                });
        }
        catch (Exception ex)
        {
            return Fail("snapshot", ex);
        }
    }

    public async Task<BrowserBridgeResult> ClickTextAsync(string text, int nth = 1, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new BrowserBridgeResult(false, "browser_click_text needs visible text (e.g. Login).", null);

        try
        {
            var page = await EnsurePageAsync(ct).ConfigureAwait(false);
            var n = Math.Max(1, nth);
            var role = page.GetByRole(AriaRole.Button, new() { Name = text.Trim() });
            var count = await role.CountAsync().ConfigureAwait(false);
            ILocator target;
            if (count >= n)
                target = role.Nth(n - 1);
            else
            {
                var byText = page.GetByText(text.Trim(), new() { Exact = false });
                if (await byText.CountAsync().ConfigureAwait(false) < n)
                    return new BrowserBridgeResult(false, $"no control matching '{text}' (nth={n}).", null);
                target = byText.Nth(n - 1);
            }

            await target.ClickAsync(new LocatorClickOptions { Timeout = 15_000 }).ConfigureAwait(false);
            await PublishFrameAsync(page, $"click_text '{text}'", ct).ConfigureAwait(false);
            return new BrowserBridgeResult(
                true,
                $"playwright clicked '{text}' (nth={n}). url={page.Url}. goal_complete=false until page postcondition.",
                new { text, nth = n, url = page.Url, backend = BackendId, action_ok = true, goal_complete = false });
        }
        catch (Exception ex)
        {
            return Fail("click_text", ex);
        }
    }

    public async Task<BrowserBridgeResult> FillAsync(string field, string value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(field))
            return new BrowserBridgeResult(false, "browser_fill needs a field name.", null);

        try
        {
            var page = await EnsurePageAsync(ct).ConfigureAwait(false);
            var label = page.GetByLabel(field.Trim(), new() { Exact = false });
            ILocator target;
            if (await label.CountAsync().ConfigureAwait(false) > 0)
                target = label.First;
            else
            {
                var ph = page.GetByPlaceholder(field.Trim(), new() { Exact = false });
                if (await ph.CountAsync().ConfigureAwait(false) == 0)
                    return new BrowserBridgeResult(false, $"no field matching '{field}'.", null);
                target = ph.First;
            }

            await target.FillAsync(value ?? "").ConfigureAwait(false);
            _log?.LogInformation(
                "playwright fill field={Field} valueChars={Chars} (value redacted)",
                field.Trim(),
                value?.Length ?? 0);
            await PublishFrameAsync(page, $"fill '{field}'", ct).ConfigureAwait(false);
            return new BrowserBridgeResult(
                true,
                $"playwright filled '{field}' ({value?.Length ?? 0} chars) [value redacted]. url={page.Url}",
                BrowserResultHonesty.FillRedacted(field.Trim(), value?.Length ?? 0, BackendId));
        }
        catch (Exception ex)
        {
            return Fail("fill", ex);
        }
    }

    public async Task<BrowserBridgeResult> BackAsync(CancellationToken ct = default)
    {
        try
        {
            var page = await EnsurePageAsync(ct).ConfigureAwait(false);
            await page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.DOMContentLoaded })
                .ConfigureAwait(false);
            await PublishFrameAsync(page, "back", ct).ConfigureAwait(false);
            return new BrowserBridgeResult(true, $"playwright back → {page.Url}",
                BrowserResultHonesty.Navigated(page.Url, await page.TitleAsync().ConfigureAwait(false), BackendId));
        }
        catch (Exception ex)
        {
            return Fail("back", ex);
        }
    }

    public async Task<BrowserBridgeResult> TabsAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsurePageAsync(ct).ConfigureAwait(false);
            var pages = _context?.Pages ?? Array.Empty<IPage>();
            var lines = new List<string>();
            for (var i = 0; i < pages.Count; i++)
            {
                var p = pages[i];
                var title = await p.TitleAsync().ConfigureAwait(false);
                lines.Add($"[{i}] {title} | {p.Url}");
            }

            return new BrowserBridgeResult(true, "playwright tabs:\n" + string.Join("\n", lines),
                new { count = pages.Count, backend = BackendId, action_ok = true, goal_complete = false });
        }
        catch (Exception ex)
        {
            return Fail("tabs", ex);
        }
    }

    private async Task<BrowserBridgeResult> SnapshotVisualAsync(string reason, CancellationToken ct)
    {
        try
        {
            var page = await EnsurePageAsync(ct).ConfigureAwait(false);
            var bytes = await page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Jpeg, Quality = 70 })
                .ConfigureAwait(false);
            _view?.Publish(bytes, page.Url, await page.TitleAsync().ConfigureAwait(false), reason);
            return new BrowserBridgeResult(
                true,
                $"playwright screenshot ({reason}) url={page.Url}",
                new
                {
                    bytes,
                    format = "jpeg",
                    url = page.Url,
                    backend = BackendId,
                    action_ok = true,
                    goal_complete = false
                });
        }
        catch (Exception ex)
        {
            return Fail("capture", ex);
        }
    }

    private async Task PublishFrameAsync(IPage page, string action, CancellationToken ct)
    {
        if (_view is null)
            return;
        try
        {
            var bytes = await page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Jpeg, Quality = 55 })
                .ConfigureAwait(false);
            var title = await page.TitleAsync().ConfigureAwait(false);
            _view.Publish(bytes, page.Url, title, action);
        }
        catch (Exception ex)
        {
            _log?.LogDebug(ex, "Playwright frame publish failed");
        }
    }

    private async Task<IPage> EnsurePageAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_page is not null && !_page.IsClosed)
                return _page;

            var userData = ResolveUserDataDir(_opts.Value);
            Directory.CreateDirectory(userData);
            // Refuse obvious Kurt Chrome profile paths.
            if (userData.Contains("Google" + Path.DirectorySeparatorChar + "Chrome", StringComparison.OrdinalIgnoreCase)
                || userData.Contains("Microsoft" + Path.DirectorySeparatorChar + "Edge", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "PlaywrightUserDataDir must not be Kurt's Chrome/Edge profile. Use SoulCore/victoria-browser.");
            }

            _playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);
            var headed = _opts.Value.PlaywrightHeaded;
            _context = await _playwright.Chromium.LaunchPersistentContextAsync(userData, new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = !headed,
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                Args = new[] { "--disable-blink-features=AutomationControlled" }
            }).ConfigureAwait(false);

            _page = _context.Pages.Count > 0 ? _context.Pages[0] : await _context.NewPageAsync().ConfigureAwait(false);
            return _page;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string FormatA11yText(string? raw, string? query)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "(empty a11y tree)";
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            var lines = raw.Split('\n').Where(l => l.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(40);
            var filtered = string.Join("\n", lines);
            if (!string.IsNullOrWhiteSpace(filtered))
                return filtered;
        }

        return raw.Length <= 6000 ? raw : raw[..6000] + "\n…(truncated)";
    }

    private static BrowserBridgeResult Fail(string op, Exception ex) =>
        new(false, $"playwright {op} failed: {ex.Message}", new { action_ok = false, goal_complete = false, backend = BackendId });

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_context is not null)
                await _context.CloseAsync().ConfigureAwait(false);
        }
        catch { /* ignore */ }
        _playwright?.Dispose();
        _gate.Dispose();
    }
}
