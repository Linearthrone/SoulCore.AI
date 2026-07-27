using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Trading;

/// <summary>
/// Hermes MCP path for MT4 tools (BED-138). Prefers a direct tool-call endpoint
/// on the Hermes gateway (<c>POST /v1/tools/call</c> with
/// <c>{ "name":"mt4_*", "arguments":{...} }</c>); falls back to reporting
/// <c>hermes gateway unavailable</c> when <c>GET /health</c> fails.
/// </summary>
/// <remarks>
/// <para>
/// OPS-143 restores the <c>house_victoria</c> MCP server that exposes the
/// <c>mt4_*</c> tools. BED-144 may refine the invoke path (e.g. force
/// <c>tool_choice</c> via <c>/v1/chat/completions</c>); this bridge keeps a
/// thin HTTP contract so SoulCore tools stay backend-agnostic.
/// </para>
/// <para>
/// Auth uses the same <c>SOULCORE_HERMES_API_KEY</c> / user-secrets key as
/// <c>HermesHttpClient</c> when present; health probes do not require a key.
/// </para>
/// </remarks>
public sealed class HermesMt4Bridge : IMt4Bridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ILogger<HermesMt4Bridge> _logger;

    public HermesMt4Bridge(
        HttpClient http,
        IOptions<HermesOptions> hermesOptions,
        ILogger<HermesMt4Bridge> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _ = hermesOptions ?? throw new ArgumentNullException(nameof(hermesOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var key = ResolveApiKey(hermesOptions.Value);
        if (!string.IsNullOrWhiteSpace(key)
            && _http.DefaultRequestHeaders.Authorization is null)
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", key);
        }
    }

    public async Task<ToolResult> InvokeAsync(
        string mcpToolName,
        JsonElement args,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mcpToolName))
        {
            return new ToolResult(false, "error: mt4 mcp tool name required", null);
        }

        if (!await IsHealthyAsync(ct).ConfigureAwait(false))
        {
            return new ToolResult(false, "hermes gateway unavailable", null);
        }

        var payload = new Dictionary<string, object?>
        {
            ["name"] = mcpToolName.Trim(),
            ["arguments"] = args.ValueKind == JsonValueKind.Undefined
                || args.ValueKind == JsonValueKind.Null
                ? new Dictionary<string, object?>()
                : JsonSerializer.Deserialize<object>(args.GetRawText())
        };

        try
        {
            using var response = await _http.PostAsJsonAsync(
                "v1/tools/call",
                payload,
                JsonOptions,
                ct).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Hermes MT4 call {Tool} failed: {Status} {Body}",
                    mcpToolName,
                    (int)response.StatusCode,
                    Truncate(body, 400));

                // 404 on the invoke path → gateway up but MCP wiring incomplete
                // (OPS-143 / BED-144). Surface a clear message, not a raw HTML body.
                if ((int)response.StatusCode == 404)
                {
                    return new ToolResult(
                        false,
                        $"hermes mt4 tool '{mcpToolName}' invoke path unavailable (POST /v1/tools/call returned 404) — confirm OPS-143 MCP wiring / BED-144 route",
                        null);
                }

                return new ToolResult(
                    false,
                    $"hermes mt4 call failed: {(int)response.StatusCode} {Truncate(body, 200)}",
                    null);
            }

            return new ToolResult(true, string.IsNullOrWhiteSpace(body) ? "(empty)" : body, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hermes MT4 call {Tool} threw", mcpToolName);
            return new ToolResult(false, $"hermes gateway unavailable: {ex.Message}", null);
        }
    }

    private async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync("health", ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Hermes MT4 health probe failed");
            return false;
        }
    }

    private static string? ResolveApiKey(HermesOptions options)
    {
        var env = Environment.GetEnvironmentVariable(SecretNames.HermesApiKey);
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();
        return string.IsNullOrWhiteSpace(options.ApiKey) ? null : options.ApiKey.Trim();
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value ?? string.Empty;
        return value.Substring(0, max) + "…";
    }
}
