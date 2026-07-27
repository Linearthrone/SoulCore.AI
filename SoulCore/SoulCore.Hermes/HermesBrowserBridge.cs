using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Tools.Browser;

namespace SoulCore.Hermes;

/// <summary>
/// Hermes MCP <c>browser_bridge_*</c> backend for BED-136.
/// <para>
/// Flow: probe <see cref="IHermesClient.GetHealthAsync"/> → POST
/// <c>/v1/tools/call</c> with MCP tool name + arguments. BED-144 may refine
/// this to a shared MCP-direct helper; until then this is the hermes path for
/// browser tools. When Hermes is down / disabled, returns
/// <c>hermes gateway unavailable</c> without injecting input.
/// </para>
/// </summary>
public sealed class HermesBrowserBridge : IBrowserBridge
{
    public const string UnavailableContent = "hermes gateway unavailable";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHermesClient _hermes;
    private readonly HttpClient _http;
    private readonly HermesOptions _options;
    private readonly ILogger<HermesBrowserBridge> _logger;

    public HermesBrowserBridge(
        IHermesClient hermes,
        HttpClient http,
        IOptions<HermesOptions> options,
        ILogger<HermesBrowserBridge> logger)
    {
        _hermes = hermes ?? throw new ArgumentNullException(nameof(hermes));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ApplyApiKey(_http, ResolveApiKey(_options));
    }

    public string BackendName => "hermes";

    public Task<BrowserBridgeResult> HealthAsync(CancellationToken ct = default)
        => InvokeAsync("browser_bridge_health", new Dictionary<string, object?>(), ct);

    public Task<BrowserBridgeResult> CaptureTabAsync(int tab, CancellationToken ct = default)
        => InvokeAsync("browser_bridge_capture_tab", new Dictionary<string, object?> { ["tab"] = tab }, ct);

    public Task<BrowserBridgeResult> ClickAsync(int x, int y, CancellationToken ct = default)
        => InvokeAsync("browser_bridge_click", new Dictionary<string, object?> { ["x"] = x, ["y"] = y }, ct);

    public Task<BrowserBridgeResult> TypeAsync(string text, CancellationToken ct = default)
        => InvokeAsync("browser_bridge_type", new Dictionary<string, object?> { ["text"] = text }, ct);

    public Task<BrowserBridgeResult> KeyAsync(string key, CancellationToken ct = default)
        => InvokeAsync("browser_bridge_key", new Dictionary<string, object?> { ["key"] = key }, ct);

    public Task<BrowserBridgeResult> ScrollAsync(int dx, int dy, CancellationToken ct = default)
        => InvokeAsync("browser_bridge_scroll", new Dictionary<string, object?> { ["dx"] = dx, ["dy"] = dy }, ct);

    private async Task<BrowserBridgeResult> InvokeAsync(
        string mcpToolName,
        Dictionary<string, object?> arguments,
        CancellationToken ct)
    {
        if (!await IsGatewayUpAsync(ct).ConfigureAwait(false))
            return new BrowserBridgeResult(false, UnavailableContent, null);

        try
        {
            var payload = new ToolsCallRequest
            {
                Name = mcpToolName,
                Arguments = arguments
            };

            using var response = await _http.PostAsJsonAsync(
                "v1/tools/call",
                payload,
                JsonOptions,
                ct).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Hermes MCP tool {Tool} failed: {Status} {Body}",
                    mcpToolName,
                    (int)response.StatusCode,
                    Truncate(body));
                return new BrowserBridgeResult(
                    false,
                    $"hermes MCP {mcpToolName} failed: HTTP {(int)response.StatusCode}",
                    null);
            }

            return ParseToolsCallResponse(mcpToolName, body);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hermes MCP tool {Tool} threw", mcpToolName);
            return new BrowserBridgeResult(false, UnavailableContent, null);
        }
    }

    private async Task<bool> IsGatewayUpAsync(CancellationToken ct)
    {
        try
        {
            var health = await _hermes.GetHealthAsync(ct).ConfigureAwait(false);
            return !string.IsNullOrWhiteSpace(health);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Hermes health probe failed");
            return false;
        }
    }

    /// <summary>
    /// Parse Hermes <c>/v1/tools/call</c> JSON. Accepts common shapes:
    /// <c>{content}</c>, <c>{result}</c>, <c>{screenshot_path,dom}</c>, or raw text.
    /// </summary>
    public static BrowserBridgeResult ParseToolsCallResponse(string mcpToolName, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return new BrowserBridgeResult(false, $"hermes MCP {mcpToolName} returned empty body", null);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            string? content = null;
            string? screenshotPath = null;
            string? dom = null;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("content", out var contentProp)
                    && contentProp.ValueKind == JsonValueKind.String)
                {
                    content = contentProp.GetString();
                }
                else if (root.TryGetProperty("result", out var resultProp)
                         && resultProp.ValueKind == JsonValueKind.String)
                {
                    content = resultProp.GetString();
                }

                if (root.TryGetProperty("screenshot_path", out var pathProp)
                    && pathProp.ValueKind == JsonValueKind.String)
                {
                    screenshotPath = pathProp.GetString();
                }
                else if (root.TryGetProperty("path", out var path2)
                         && path2.ValueKind == JsonValueKind.String)
                {
                    screenshotPath = path2.GetString();
                }

                if (root.TryGetProperty("dom", out var domProp)
                    && domProp.ValueKind == JsonValueKind.String)
                {
                    dom = domProp.GetString();
                }

                if (root.TryGetProperty("success", out var successProp)
                    && (successProp.ValueKind == JsonValueKind.False
                        || (successProp.ValueKind == JsonValueKind.String
                            && string.Equals(successProp.GetString(), "false", StringComparison.OrdinalIgnoreCase))))
                {
                    return new BrowserBridgeResult(
                        false,
                        content ?? $"hermes MCP {mcpToolName} reported failure",
                        null);
                }
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                if (!string.IsNullOrWhiteSpace(screenshotPath))
                {
                    content = string.IsNullOrWhiteSpace(dom)
                        ? $"captured tab screenshot: {screenshotPath}"
                        : $"captured tab screenshot: {screenshotPath}\n\nDOM:\n{dom}";
                }
                else
                {
                    content = body.Trim();
                }
            }
            else if (!string.IsNullOrWhiteSpace(screenshotPath)
                     && content.IndexOf(screenshotPath, StringComparison.Ordinal) < 0)
            {
                content = string.IsNullOrWhiteSpace(dom)
                    ? $"{content}\nscreenshot: {screenshotPath}"
                    : $"{content}\nscreenshot: {screenshotPath}\n\nDOM:\n{dom}";
            }

            object? data = screenshotPath is null && dom is null
                ? null
                : new { path = screenshotPath, dom };

            return new BrowserBridgeResult(true, content!, data);
        }
        catch (JsonException)
        {
            // Non-JSON body — treat as plain-text success content from MCP.
            return new BrowserBridgeResult(true, body.Trim(), null);
        }
    }

    private static string? ResolveApiKey(HermesOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            return options.ApiKey.Trim();

        var fromEnv = Environment.GetEnvironmentVariable(SecretNames.HermesApiKey);
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv.Trim();
    }

    private static void ApplyApiKey(HttpClient http, string? apiKey)
    {
        http.DefaultRequestHeaders.Remove("Authorization");
        if (!string.IsNullOrWhiteSpace(apiKey))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    private static string Truncate(string? text, int max = 200)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return text.Length <= max ? text : text[..max] + "…";
    }

    private sealed class ToolsCallRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public Dictionary<string, object?> Arguments { get; set; } = new();
    }
}
