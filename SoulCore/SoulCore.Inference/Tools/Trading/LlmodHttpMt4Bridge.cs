using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Trading;

/// <summary>
/// Direct HTTP bridge to LLMOD MCP on shadow (<c>POST /command</c>, BED-169).
/// Targets Tailscale host <c>house-victoria:8080</c> by default — MT4 terminal +
/// <c>HouseVictoriaBridge.mq4</c> stay on shadow; SoulCore Host stays on main.
/// </summary>
public sealed class LlmodHttpMt4Bridge : IMt4Bridge
{
    public const string UnavailableMessage =
        "llmod mcp unavailable — check house-victoria MCP HTTP :8080 and Tailscale";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ToolsOptions _options;
    private readonly ILogger<LlmodHttpMt4Bridge> _logger;

    public LlmodHttpMt4Bridge(
        HttpClient http,
        IOptions<ToolsOptions> options,
        ILogger<LlmodHttpMt4Bridge> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ToolResult> InvokeAsync(
        string mcpToolName,
        JsonElement args,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mcpToolName))
            return new ToolResult(false, "error: mt4 mcp tool name required", null);

        var tool = mcpToolName.Trim();
        var endpoint = ResolveEndpoint(_options);
        var commandUrl = $"{endpoint}/command";
        var parameters = Mt4LlmodArgsMapper.ToLlmodParameters(tool, args);

        var payload = new Dictionary<string, object?>
        {
            ["command"] = tool,
            ["parameters"] = parameters
        };

        _logger.LogInformation(
            "LLMOD MCP invoke start: tool={Tool} endpoint={Endpoint}",
            tool,
            endpoint);

        string body;
        try
        {
            using var response = await _http
                .PostAsJsonAsync(commandUrl, payload, JsonOptions, ct)
                .ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "LLMOD MCP HTTP {Status} for tool={Tool}: {Body}",
                    (int)response.StatusCode,
                    tool,
                    Truncate(body, 400));
                return new ToolResult(
                    false,
                    UnavailableMessage,
                    new { mcpToolName = tool, reason = "http_error", status = (int)response.StatusCode });
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLMOD MCP transport failure for tool={Tool}", tool);
            return new ToolResult(
                false,
                UnavailableMessage,
                new { mcpToolName = tool, reason = "transport", error = ex.GetType().Name });
        }

        return TranslateResponse(tool, body);
    }

    public static string ResolveEndpoint(ToolsOptions options)
    {
        var raw = string.IsNullOrWhiteSpace(options.LlmodMcpEndpoint)
            ? ToolsOptions.DefaultLlmodMcpEndpoint
            : options.LlmodMcpEndpoint.Trim();
        return raw.TrimEnd('/');
    }

    public static ToolResult TranslateResponse(string mcpToolName, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new ToolResult(
                false,
                UnavailableMessage,
                new { mcpToolName, reason = "empty_body" });
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return new ToolResult(false, body.Trim(), new { mcpToolName, reason = "non_json" });
        }

        using (doc)
        {
            var root = doc.RootElement;
            var outerSuccess = !root.TryGetProperty("success", out var outerOk)
                || outerOk.ValueKind != JsonValueKind.False;

            if (!outerSuccess)
            {
                var msg = root.TryGetProperty("message", out var om) && om.ValueKind == JsonValueKind.String
                    ? om.GetString()
                    : UnavailableMessage;
                return new ToolResult(false, msg ?? UnavailableMessage, root.Clone());
            }

            if (root.TryGetProperty("data", out var dataEl))
            {
                var inner = ParseInnerData(dataEl);
                if (inner is not null)
                {
                    var innerSuccess = !inner.Value.TryGetProperty("success", out var isOk)
                        || isOk.ValueKind != JsonValueKind.False;

                    var content = FormatInnerContent(inner.Value);
                    return new ToolResult(innerSuccess, content, inner.Value.Clone());
                }
            }

            var message = root.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String
                ? msgEl.GetString()
                : "ok";
            return new ToolResult(true, message ?? "ok", root.Clone());
        }
    }

    private static JsonElement? ParseInnerData(JsonElement dataEl)
    {
        if (dataEl.ValueKind == JsonValueKind.Object)
            return dataEl.Clone();

        if (dataEl.ValueKind != JsonValueKind.String)
            return null;

        var dataStr = dataEl.GetString();
        if (string.IsNullOrWhiteSpace(dataStr))
            return null;

        try
        {
            using var innerDoc = JsonDocument.Parse(dataStr);
            return innerDoc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string FormatInnerContent(JsonElement inner)
    {
        if (inner.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
        {
            var text = msg.GetString();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return inner.GetRawText();
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
