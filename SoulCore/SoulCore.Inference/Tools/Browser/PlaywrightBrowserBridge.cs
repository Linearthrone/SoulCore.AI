using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// BED-195 Avenue A1: Host Playwright Chromium with Victoria-only user-data-dir.
/// Never attaches to the operator's daily Chrome profile.
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
                $"playwright ok: Victoria dedicated Chromium (not the operator's Chrome). url={url} title={title}",
                new { backend = BackendId, url, title, profile = ResolveUserDataDir(_opts.Value) });
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Playwright health failed");
            return new BrowserBridgeResult(false, FormatPlaywrightError("health", ex), null);
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
            await ClickWithVisibleCursorAsync(page, x, y, ct).ConfigureAwait(false);
            await PublishFrameAsync(page, $"click ({x},{y})", ct, clickX: x, clickY: y).ConfigureAwait(false);
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
            // Prefer AriaSnapshot (string) - Accessibility.SnapshotAsync is deprecated.
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
            var label = text.Trim();
            var n = Math.Max(1, nth);
            var resolved = await ResolveClickableAsync(page, label, n).ConfigureAwait(false);
            if (resolved is null)
            {
                return new BrowserBridgeResult(
                    false,
                    $"no clickable control matching '{label}' (nth={n}). Call browser_snapshot to list buttons/links.",
                    new { text = label, nth = n, action_ok = false, goal_complete = false, backend = BackendId });
            }

            var (target, how) = resolved.Value;
            var urlBefore = page.Url;
            var titleBefore = await page.TitleAsync().ConfigureAwait(false);

            await target.ScrollIntoViewIfNeededAsync(new() { Timeout = 5_000 }).ConfigureAwait(false);
            var box = await target.BoundingBoxAsync().ConfigureAwait(false);
            int? clickX = null;
            int? clickY = null;
            if (box is not null)
            {
                clickX = (int)Math.Round(box.X + box.Width / 2);
                clickY = (int)Math.Round(box.Y + box.Height / 2);
                await ClickWithVisibleCursorAsync(page, clickX.Value, clickY.Value, ct).ConfigureAwait(false);
            }
            else
            {
                await EnsureClickCursorAsync(page).ConfigureAwait(false);
                await target.ClickAsync(new LocatorClickOptions { Timeout = 15_000 }).ConfigureAwait(false);
            }

            // SPA/nav may not fire; settle briefly then compare URL/title so we don't pretend the next screen arrived.
            try
            {
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 5_000 })
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Click may open a dialog without navigation — still re-snapshot.
            }

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 3_000 })
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Long-polling pages never idle — ignore.
            }

            await page.WaitForTimeoutAsync(400).ConfigureAwait(false);

            var urlAfter = page.Url;
            var titleAfter = await page.TitleAsync().ConfigureAwait(false);
            var pageChanged = !string.Equals(urlBefore, urlAfter, StringComparison.Ordinal)
                || !string.Equals(titleBefore ?? "", titleAfter ?? "", StringComparison.Ordinal);

            var action = clickX is int cx && clickY is int cy
                ? $"click_text '{label}' via {how} @({cx},{cy})"
                : $"click_text '{label}' via {how}";
            await PublishFrameAsync(page, action, ct, clickX, clickY).ConfigureAwait(false);
            return new BrowserBridgeResult(
                true,
                FormatClickTextResult(label, n, how, urlBefore, urlAfter, pageChanged),
                new
                {
                    text = label,
                    nth = n,
                    matched_as = how,
                    x = clickX,
                    y = clickY,
                    url_before = urlBefore,
                    url = urlAfter,
                    title = titleAfter,
                    page_changed = pageChanged,
                    backend = BackendId,
                    action_ok = true,
                    goal_complete = false
                });
        }
        catch (Exception ex)
        {
            return Fail("click_text", ex);
        }
    }

    /// <summary>
    /// Prefer real controls (button/link) over raw text nodes so clicks actually activate UI.
    /// </summary>
    internal static async Task<(ILocator Target, string How)?> ResolveClickableAsync(IPage page, string label, int nth)
    {
        var n = Math.Max(1, nth);
        var roles = new[]
        {
            (AriaRole.Button, "button"),
            (AriaRole.Link, "link"),
            (AriaRole.Tab, "tab"),
            (AriaRole.Menuitem, "menuitem"),
        };

        foreach (var (role, how) in roles)
        {
            var loc = page.GetByRole(role, new() { Name = label, Exact = false });
            var count = await loc.CountAsync().ConfigureAwait(false);
            if (count >= n)
                return (loc.Nth(n - 1), how);
        }

        // Clickable elements only — bare GetByText used to hit headings/labels and "succeed" with no UI change.
        var clickable = page.Locator("button, a, [role='button'], [role='link'], input[type='submit'], input[type='button'], summary")
            .Filter(new() { HasTextString = label });
        var clickableCount = await clickable.CountAsync().ConfigureAwait(false);
        if (clickableCount >= n)
            return (clickable.Nth(n - 1), "clickable");

        return null;
    }

    public static string FormatClickTextResult(
        string label,
        int nth,
        string how,
        string urlBefore,
        string urlAfter,
        bool pageChanged)
    {
        if (pageChanged)
        {
            return
                $"playwright clicked '{label}' (nth={nth}, as {how}). page changed {urlBefore} -> {urlAfter}. " +
                "goal_complete=false. Call browser_snapshot now to confirm the next screen before telling Kurt you are waiting.";
        }

        return
            $"playwright clicked '{label}' (nth={nth}, as {how}) but URL/title did NOT change ({urlAfter}). " +
            "Do NOT claim the next screen appeared or that you are waiting on a popup — call browser_snapshot now. " +
            "If the control is still there, try a different label, nth=2, or browser_fill first. goal_complete=false.";
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

    private async Task PublishFrameAsync(IPage page, string action, CancellationToken ct, int? clickX = null, int? clickY = null)
    {
        if (_view is null)
            return;
        try
        {
            var bytes = await page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Jpeg, Quality = 55 })
                .ConfigureAwait(false);
            if (clickX is int x && clickY is int y)
                bytes = PlaywrightClickCursor.BurnInMarker(bytes, x, y);
            var title = await page.TitleAsync().ConfigureAwait(false);
            _view.Publish(bytes, page.Url, title, action);
        }
        catch (Exception ex)
        {
            _log?.LogDebug(ex, "Playwright frame publish failed");
        }
    }

    private async Task EnsureClickCursorAsync(IPage page)
    {
        try
        {
            await page.EvaluateAsync(PlaywrightClickCursor.InitScript).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.LogDebug(ex, "Playwright click cursor inject failed");
        }
    }

    private async Task ClickWithVisibleCursorAsync(IPage page, int x, int y, CancellationToken ct)
    {
        _ = ct;
        await EnsureClickCursorAsync(page).ConfigureAwait(false);
        await page.Mouse.MoveAsync(x, y).ConfigureAwait(false);
        try
        {
            await page.EvaluateAsync(
                @"([x, y]) => { if (window.__scShowClick) window.__scShowClick(x, y); }",
                new[] { x, y }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.LogDebug(ex, "Playwright click cursor move failed");
        }

        // Brief pause so Presence / headed window can show the aim point before the click.
        await page.WaitForTimeoutAsync(140).ConfigureAwait(false);
        await page.Mouse.ClickAsync(x, y).ConfigureAwait(false);
        await page.WaitForTimeoutAsync(80).ConfigureAwait(false);
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
            // Refuse obvious operator Chrome profile paths.
            if (userData.Contains("Google" + Path.DirectorySeparatorChar + "Chrome", StringComparison.OrdinalIgnoreCase)
                || userData.Contains("Microsoft" + Path.DirectorySeparatorChar + "Edge", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "PlaywrightUserDataDir must not be the operator's Chrome/Edge profile. Use SoulCore/victoria-browser.");
            }

            _playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);
            var headed = _opts.Value.PlaywrightHeaded;
            // Pin ExecutablePath to chromium-1148 chrome.exe (same path install-playwright verifies).
            // Playwright 1.49 headless otherwise prefers chromium-headless-shell, which made the
            // terminal say OK while Victoria still failed — and she then asked for VirtualBox.
            var chromeExe = TryResolveChromiumExecutable();
            if (chromeExe is null)
            {
                var expected = ExpectedChromiumExecutablePaths().FirstOrDefault() ?? "chromium-1148/chrome-win/chrome.exe";
                throw new InvalidOperationException(
                    $"Executable doesn't exist at {expected}. " +
                    "Please run SoulCore/scripts/install-playwright.ps1 (VirtualBox is not required for websites).");
            }

            _context = await _playwright.Chromium.LaunchPersistentContextAsync(userData, new BrowserTypeLaunchPersistentContextOptions
            {
                ExecutablePath = chromeExe,
                Headless = !headed,
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                Args = new[] { "--disable-blink-features=AutomationControlled" }
            }).ConfigureAwait(false);

            await _context.AddInitScriptAsync(PlaywrightClickCursor.InitScript).ConfigureAwait(false);

            _page = _context.Pages.Count > 0 ? _context.Pages[0] : await _context.NewPageAsync().ConfigureAwait(false);
            await EnsureClickCursorAsync(_page).ConfigureAwait(false);
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

    /// <summary>Microsoft.Playwright 1.49.0 browsers.json chromium revision.</summary>
    public const string ChromiumRevision = "1148";

    /// <summary>
    /// Paths Host will accept for Victoria's Chromium (must match install-playwright.ps1).
    /// </summary>
    public static IEnumerable<string> ExpectedChromiumExecutablePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
            yield break;

        yield return Path.Combine(local, "ms-playwright", $"chromium-{ChromiumRevision}", "chrome-win", "chrome.exe");
        yield return Path.Combine(local, "ms-playwright", $"chromium-{ChromiumRevision}", "chrome-win64", "chrome.exe");
    }

    public static string? TryResolveChromiumExecutable()
    {
        foreach (var path in ExpectedChromiumExecutablePaths())
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static BrowserBridgeResult Fail(string op, Exception ex) =>
        new(false, FormatPlaywrightError(op, ex), new { action_ok = false, goal_complete = false, backend = BackendId, setup_needed = LooksLikeMissingBrowser(ex) });

    /// <summary>
    /// People-friendly error Victoria can relay to Kurt. Always includes the install
    /// recipe when Chromium is missing (navigate/click used to omit it).
    /// </summary>
    public static string FormatPlaywrightError(string op, Exception ex)
    {
        var detail = (ex.Message ?? "").Trim();
        if (LooksLikeMissingBrowser(ex))
        {
            return
                $"Victoria's browser is not set up yet ({op}). " +
                "VirtualBox is NOT required for websites — do not ask Kurt to start the VM for this. " +
                "Kurt: from the Soul_Core repo root run " +
                "`powershell -NoProfile -ExecutionPolicy Bypass -File .\\SoulCore\\scripts\\install-playwright.ps1` " +
                "and wait until it prints FOUND chrome.exe under chromium-1148 (first download can take 5-10 minutes - do not cancel), " +
                "then `.\\ALLSTART.ps1 -RestartHost`. " +
                "Need: %LOCALAPPDATA%\\ms-playwright\\chromium-1148\\chrome-win\\chrome.exe " +
                "(Victoria's profile is separate: %LOCALAPPDATA%\\SoulCore\\victoria-browser). " +
                (string.IsNullOrWhiteSpace(detail) ? "" : $"Detail: {detail}");
        }

        return $"playwright {op} failed: {detail}";
    }

    public static bool LooksLikeMissingBrowser(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            var m = cur.Message ?? "";
            if (m.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
                || m.Contains("Please run the following command to download new browsers", StringComparison.OrdinalIgnoreCase)
                || m.Contains("browserType.launch", StringComparison.OrdinalIgnoreCase)
                    && m.Contains("chromium", StringComparison.OrdinalIgnoreCase)
                || m.Contains("playwright install", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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
