using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Native House Victoria browser bridge (BED-182) — HTTP to
/// <c>BrowserCaptureBridge/bridge_server.py</c> on loopback :17891.
/// No Hermes. Requires the unpacked <c>BrowserCaptureExtension</c> in Chrome/Edge.
/// </summary>
public sealed class NativeBrowserBridge : IBrowserBridge
{
    public const string UnavailableContent =
        "browser capture bridge unavailable — start BrowserCaptureBridge " +
        "(ALLSTART or SoulCore/scripts/start-browser-bridge.ps1) and load " +
        "BrowserCaptureExtension (chrome://extensions → Load unpacked)";

    public const string DefaultBaseUrl = "http://127.0.0.1:17891";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ILogger<NativeBrowserBridge> _logger;

    public NativeBrowserBridge(
        HttpClient http,
        IOptions<ToolsOptions>? toolsOptions,
        ILogger<NativeBrowserBridge>? logger = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? NullLogger<NativeBrowserBridge>.Instance;

        var configured = toolsOptions?.Value?.BrowserBridgeUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(configured)
            && (_http.BaseAddress is null || _http.BaseAddress.AbsoluteUri == "http://localhost/"))
        {
            _http.BaseAddress = new Uri(configured.TrimEnd('/') + "/");
        }
        else if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(DefaultBaseUrl.TrimEnd('/') + "/");
        }
    }

    public string BackendName => "native";

    public async Task<BrowserBridgeResult> HealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync("health", ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return new BrowserBridgeResult(
                    false,
                    $"{UnavailableContent} (HTTP {(int)resp.StatusCode})",
                    null);
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var ok = doc.RootElement.TryGetProperty("ok", out var okEl)
                     && okEl.ValueKind == JsonValueKind.True;
            var service = doc.RootElement.TryGetProperty("service", out var s)
                ? s.GetString()
                : "hv-browser-capture-bridge";
            var pending = doc.RootElement.TryGetProperty("pending_jobs", out var p)
                          && p.TryGetInt32(out var n)
                ? n
                : 0;
            var content = ok
                ? $"browser bridge ok: {service} pending_jobs={pending}"
                : UnavailableContent;
            return new BrowserBridgeResult(ok, content, doc.RootElement.Clone());
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Native browser bridge health failed");
            return new BrowserBridgeResult(false, UnavailableContent, null);
        }
    }

    public async Task<BrowserBridgeResult> CaptureTabAsync(int tab, CancellationToken ct = default)
    {
        // Bridge captures the active tab; SoulCore's tab index is advisory only.
        _ = tab;
        var payload = new
        {
            include_screenshot = true,
            include_page_map = true,
            timeout_seconds = 35
        };
        return await PostAsync("capture", payload, formatCapture: true, ct).ConfigureAwait(false);
    }

    public Task<BrowserBridgeResult> ClickAsync(int x, int y, CancellationToken ct = default) =>
        PostAsync("action", new
        {
            action = "click",
            x,
            y,
            button = "left",
            timeout_seconds = 35
        }, formatCapture: false, ct);

    public Task<BrowserBridgeResult> TypeAsync(string text, CancellationToken ct = default) =>
        PostAsync("action", new
        {
            action = "type",
            text = text ?? "",
            clear = false,
            timeout_seconds = 35
        }, formatCapture: false, ct);

    public Task<BrowserBridgeResult> KeyAsync(string key, CancellationToken ct = default) =>
        PostAsync("action", new
        {
            action = "key",
            key = key ?? "",
            modifiers = Array.Empty<string>(),
            timeout_seconds = 35
        }, formatCapture: false, ct);

    public Task<BrowserBridgeResult> ScrollAsync(int dx, int dy, CancellationToken ct = default) =>
        PostAsync("action", new
        {
            action = "scroll",
            delta_x = dx,
            delta_y = dy,
            timeout_seconds = 35
        }, formatCapture: false, ct);

    private async Task<BrowserBridgeResult> PostAsync(
        string path,
        object body,
        bool formatCapture,
        CancellationToken ct)
    {
        try
        {
            using var resp = await _http.PostAsJsonAsync(path, body, ct).ConfigureAwait(false);
            var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new BrowserBridgeResult(
                    false,
                    $"browser bridge empty response from /{path} (HTTP {(int)resp.StatusCode})",
                    null);
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;

            if (!ok)
            {
                var err = root.TryGetProperty("error", out var e) ? e.GetString() : "failed";
                var hint = root.TryGetProperty("hint", out var h) ? h.GetString() : null;
                var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;
                var msg = new StringBuilder();
                msg.Append("browser bridge error: ").Append(err);
                if (!string.IsNullOrWhiteSpace(detail))
                    msg.Append(" — ").Append(detail);
                if (!string.IsNullOrWhiteSpace(hint))
                    msg.Append(" (").Append(hint).Append(')');
                if (string.Equals(err, "extension_timeout", StringComparison.OrdinalIgnoreCase))
                    msg.Append(" — ").Append(UnavailableContent);
                return new BrowserBridgeResult(false, msg.ToString(), root.Clone());
            }

            if (formatCapture)
                return FormatCaptureSuccess(root);

            var detailOk = root.TryGetProperty("detail", out var det) ? det.GetString() : "ok";
            var url = root.TryGetProperty("url", out var u) ? u.GetString() : null;
            var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            var content = $"browser {path} ok: {detailOk}"
                          + (string.IsNullOrWhiteSpace(title) ? "" : $" title={title}")
                          + (string.IsNullOrWhiteSpace(url) ? "" : $" url={url}");
            return new BrowserBridgeResult(true, content, root.Clone());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Native browser bridge /{Path} failed", path);
            return new BrowserBridgeResult(false, UnavailableContent, null);
        }
    }

    private static BrowserBridgeResult FormatCaptureSuccess(JsonElement root)
    {
        var path = root.TryGetProperty("screenshot_path", out var p) ? p.GetString() : null;
        var url = root.TryGetProperty("url", out var u) ? u.GetString() : null;
        var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
        var pageMap = root.TryGetProperty("page_map", out var pm) && pm.ValueKind == JsonValueKind.Object
            ? pm
            : (JsonElement?)null;

        var sb = new StringBuilder();
        sb.Append("captured tab screenshot: ");
        sb.Append(string.IsNullOrWhiteSpace(path) ? "(no path)" : path);
        if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(url))
        {
            sb.AppendLine();
            sb.Append("title: ").Append(title ?? "").AppendLine();
            sb.Append("url: ").Append(url ?? "");
        }

        if (pageMap is { } map)
        {
            sb.AppendLine().AppendLine().Append("page_map:");
            sb.AppendLine(map.GetRawText());
        }

        byte[]? bytes = null;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try { bytes = File.ReadAllBytes(path); }
            catch { /* host-side only */ }
        }

        object data = bytes is { Length: > 0 }
            ? new { path, url, title, page_map = pageMap?.Clone(), bytes }
            : new { path, url, title, page_map = pageMap?.Clone() };

        return new BrowserBridgeResult(true, sb.ToString(), data);
    }
}
