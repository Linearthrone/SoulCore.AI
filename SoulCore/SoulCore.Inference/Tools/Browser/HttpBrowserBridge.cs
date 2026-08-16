using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Talks straight to the House Victoria Browser Capture bridge on
/// <c>http://127.0.0.1:17891</c> (extension producer + HTTP job poll).
/// Used when <c>Tools:BrowserBackend</c> is <c>bridge</c> or <c>native</c> so
/// Victoria can capture tabs without enabling the Hermes gateway.
/// </summary>
public sealed class HttpBrowserBridge : IBrowserBridge
{
    public const string UnavailableContent =
        "browser capture bridge unavailable — start BrowserCaptureBridge on :17891 and load the Chrome/Edge extension";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ILogger<HttpBrowserBridge> _logger;

    public HttpBrowserBridge(
        HttpClient http,
        IOptions<ToolsOptions> options,
        ILogger<HttpBrowserBridge> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(options);

        var baseUrl = string.IsNullOrWhiteSpace(options.Value.BrowserBridgeBaseUrl)
            ? ToolsOptions.DefaultBrowserBridgeBaseUrl
            : options.Value.BrowserBridgeBaseUrl.Trim().TrimEnd('/') + "/";

        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(baseUrl);
    }

    public string BackendName => "bridge";

    public async Task<BrowserBridgeResult> HealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync("health", ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new BrowserBridgeResult(
                    false,
                    $"bridge health HTTP {(int)response.StatusCode}: {Truncate(body, 200)}");
            }

            return new BrowserBridgeResult(true, Truncate(body, 500), body);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Browser bridge health failed");
            return new BrowserBridgeResult(false, UnavailableContent);
        }
    }

    public Task<BrowserBridgeResult> CaptureTabAsync(int tab, CancellationToken ct = default)
    {
        // Bridge captures the active tab; tab index is accepted for API parity.
        _ = tab;
        return PostAsync(
            "capture",
            new Dictionary<string, object?>
            {
                ["include_screenshot"] = true,
                ["include_page_map"] = true,
                ["timeout_seconds"] = 35
            },
            ct);
    }

    public Task<BrowserBridgeResult> ClickAsync(int x, int y, CancellationToken ct = default) =>
        PostAsync(
            "action",
            new Dictionary<string, object?>
            {
                ["action"] = "click",
                ["x"] = x,
                ["y"] = y,
                ["timeout_seconds"] = 35
            },
            ct);

    public Task<BrowserBridgeResult> TypeAsync(string text, CancellationToken ct = default) =>
        PostAsync(
            "action",
            new Dictionary<string, object?>
            {
                ["action"] = "type",
                ["text"] = text ?? string.Empty,
                ["timeout_seconds"] = 35
            },
            ct);

    public Task<BrowserBridgeResult> KeyAsync(string key, CancellationToken ct = default) =>
        PostAsync(
            "action",
            new Dictionary<string, object?>
            {
                ["action"] = "key",
                ["key"] = key ?? string.Empty,
                ["timeout_seconds"] = 35
            },
            ct);

    public Task<BrowserBridgeResult> ScrollAsync(int dx, int dy, CancellationToken ct = default) =>
        PostAsync(
            "action",
            new Dictionary<string, object?>
            {
                ["action"] = "scroll",
                ["delta_x"] = dx,
                ["delta_y"] = dy,
                ["timeout_seconds"] = 35
            },
            ct);

    private async Task<BrowserBridgeResult> PostAsync(
        string relativePath,
        Dictionary<string, object?> payload,
        CancellationToken ct)
    {
        try
        {
            using var response = await _http
                .PostAsJsonAsync(relativePath, payload, JsonOptions, ct)
                .ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new BrowserBridgeResult(
                    false,
                    $"bridge {relativePath} HTTP {(int)response.StatusCode}: {Truncate(body, 300)}");
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var root = doc.RootElement;
            var ok = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("ok", out var okEl)
                && okEl.ValueKind == JsonValueKind.True;

            var content = BuildContent(root, body);
            return new BrowserBridgeResult(ok, content, body);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Browser bridge {Path} timed out", relativePath);
            return new BrowserBridgeResult(
                false,
                "browser capture bridge timed out — is the Chrome/Edge extension loaded and the tab open?");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Browser bridge {Path} failed", relativePath);
            return new BrowserBridgeResult(false, UnavailableContent);
        }
    }

    private static string BuildContent(JsonElement root, string rawBody)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return Truncate(rawBody, 800);

        if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
        {
            var err = root.TryGetProperty("error", out var e) ? e.GetString() : null;
            var hint = root.TryGetProperty("hint", out var h) ? h.GetString() : null;
            var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;
            var sb = new StringBuilder();
            sb.Append(err ?? "bridge_error");
            if (!string.IsNullOrWhiteSpace(detail))
                sb.Append(": ").Append(detail);
            if (!string.IsNullOrWhiteSpace(hint))
                sb.Append(" (").Append(hint).Append(')');
            return sb.ToString();
        }

        // Prefer a compact model-facing summary (path + url) over megabyte base64.
        if (root.TryGetProperty("screenshot_path", out var pathEl)
            && pathEl.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(pathEl.GetString()))
        {
            var url = root.TryGetProperty("url", out var u) ? u.GetString() : null;
            var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            return $"captured tab url={url ?? "?"} title={title ?? "?"} path={pathEl.GetString()}";
        }

        return Truncate(rawBody, 1200);
    }

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return text.Length <= max ? text : text[..max] + "…";
    }
}
