using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// VM-scoped browser backend. All actions run inside Ubuntu Firefox via Guest
/// Additions. Never talks to Kurt's Windows Chrome extension on :17891.
/// </summary>
public sealed class GuestVmBrowserBridge : IBrowserBridge
{
    public const string HostBlocked =
        "VM scope active: Kurt's Windows Chrome/bridge is blocked. " +
        "browser_* tools drive Firefox inside the Ubuntu guest (victoria-sandbox). " +
        "The VirtualBox window can stay minimized.";

    private readonly IVmGuestDesktop _desktop;
    private readonly IVmGuestBrowser _browser;

    public GuestVmBrowserBridge(IVmGuestDesktop desktop, IVmGuestBrowser browser)
    {
        _desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
    }

    public string BackendName => "vbox-guest";

    public async Task<BrowserBridgeResult> HealthAsync(CancellationToken ct = default)
    {
        var listed = await _desktop.ListWindowsAsync(ct).ConfigureAwait(false);
        if (!listed.Success)
            return new BrowserBridgeResult(false, HostBlocked + " " + listed.Content, null);

        var firefox = (listed.Content ?? "").Contains("Firefox", StringComparison.OrdinalIgnoreCase);
        var content = firefox
            ? "guest browser ok: Firefox is open in the Ubuntu VM (not Windows Chrome). " + HostBlocked
            : "guest browser ok: Ubuntu VM reachable; Firefox not listed yet — call browser_navigate or desktop_open_app. " +
              HostBlocked;
        return new BrowserBridgeResult(true, content, null);
    }

    public async Task<BrowserBridgeResult> CaptureTabAsync(int tab, CancellationToken ct = default)
    {
        _ = tab;
        var shot = await _desktop.ScreenshotAsync(ct).ConfigureAwait(false);
        var snap = await _browser.BrowserSnapshotAsync(null, ct).ConfigureAwait(false);
        var content = (shot.Success ? shot.Content : "screenshot failed: " + shot.Content)
                      + "\n\n" + (snap.Success ? snap.Content : snap.Content);
        return new BrowserBridgeResult(shot.Success, content, shot.Data);
    }

    public async Task<BrowserBridgeResult> ClickAsync(int x, int y, CancellationToken ct = default)
    {
        var result = await _desktop.ClickAsync(x, y, "left", 1, ct).ConfigureAwait(false);
        return ToBridge(result);
    }

    public async Task<BrowserBridgeResult> TypeAsync(string text, CancellationToken ct = default)
    {
        var result = await _desktop.TypeAsync(text ?? "", ct).ConfigureAwait(false);
        return ToBridge(result);
    }

    public async Task<BrowserBridgeResult> KeyAsync(string key, CancellationToken ct = default)
    {
        var result = await _desktop.KeyAsync(key ?? "", ct).ConfigureAwait(false);
        return ToBridge(result);
    }

    public async Task<BrowserBridgeResult> ScrollAsync(int dx, int dy, CancellationToken ct = default)
    {
        var result = await _desktop.ScrollAsync(640, 400, dy, dx, ct).ConfigureAwait(false);
        return ToBridge(result);
    }

    public async Task<BrowserBridgeResult> NavigateAsync(string url, CancellationToken ct = default)
    {
        var result = await _browser.BrowserNavigateAsync(url, ct).ConfigureAwait(false);
        return ToBridge(result);
    }

    public async Task<BrowserBridgeResult> SnapshotAsync(string? query = null, CancellationToken ct = default)
    {
        var result = await _browser.BrowserSnapshotAsync(query, ct).ConfigureAwait(false);
        if (result.Success)
            return ToBridge(result);

        // AT-SPI often breaks after guestcontrol/session churn. Fall back to a
        // framebuffer PNG so the model can still see Login and desktop_click.
        var shot = await _desktop.ScreenshotAsync(ct).ConfigureAwait(false);
        if (shot.Success)
        {
            return new BrowserBridgeResult(
                true,
                "browser_snapshot (AT-SPI) failed — fell back to desktop_screenshot. " +
                "Use desktop_click with coords from the PNG (not window-center). " +
                "AT-SPI error: " + result.Content + "\n\n" + shot.Content,
                shot.Data);
        }

        return new BrowserBridgeResult(
            false,
            result.Content + " | desktop_screenshot also failed: " + shot.Content
            + ". Set SOULCORE_VBOX_GUEST_PASS / ensure VM is running, then retry desktop_screenshot.",
            null);
    }

    public async Task<BrowserBridgeResult> ClickTextAsync(string text, int nth = 1, CancellationToken ct = default)
    {
        var result = await _browser.BrowserClickTextAsync(text, nth, ct).ConfigureAwait(false);
        return ToBridge(result);
    }

    public async Task<BrowserBridgeResult> FillAsync(string field, string value, CancellationToken ct = default)
    {
        var result = await _browser.BrowserFillAsync(field, value, ct).ConfigureAwait(false);
        return ToBridge(result);
    }

    public async Task<BrowserBridgeResult> BackAsync(CancellationToken ct = default)
    {
        var result = await _browser.BrowserBackAsync(ct).ConfigureAwait(false);
        return ToBridge(result);
    }

    public async Task<BrowserBridgeResult> TabsAsync(CancellationToken ct = default)
    {
        var result = await _browser.BrowserTabsAsync(ct).ConfigureAwait(false);
        return ToBridge(result);
    }

    private static BrowserBridgeResult ToBridge(DesktopOpResult result) =>
        new(result.Success, result.Content, result.Data);
}
