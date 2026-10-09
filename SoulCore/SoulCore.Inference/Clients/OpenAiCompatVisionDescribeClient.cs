using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Clients;

/// <summary>
/// OpenAI-compat <c>/chat/completions</c> vision describe
/// (Synthetic.new: <c>https://api.synthetic.new/openai/v1</c> + <c>syn:large:vision</c>).
/// </summary>
public sealed class OpenAiCompatVisionDescribeClient : IVisionDescribeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public const string DefaultUiPrompt =
        "You are helping an agent operate an Ubuntu guest browser/desktop. " +
        "Describe this screenshot concretely: visible text (headings, buttons, links, " +
        "labels, input placeholders, errors), layout, and what to do next. " +
        "Guest coordinates origin is top-left (0,0). Prefer quoting exact button/link labels. " +
        "If the image is blank, corrupted, or unreadable, say so explicitly.";

    private readonly HttpClient _http;
    private readonly InferenceOptions _options;
    private readonly ILogger<OpenAiCompatVisionDescribeClient> _logger;

    public OpenAiCompatVisionDescribeClient(
        HttpClient http,
        IOptions<InferenceOptions> options,
        ILogger<OpenAiCompatVisionDescribeClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsConfigured =>
        _options.IsVisionSidecarConfigured && !string.IsNullOrWhiteSpace(_options.ResolveVisionApiKey());

    public async Task<string?> DescribeScreenshotAsync(
        string toolName,
        IReadOnlyList<string> base64Images,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || base64Images is not { Count: > 0 })
            return null;

        var model = _options.VisionModel.Trim();
        var contentParts = new List<object>
        {
            new { type = "text", text = DefaultUiPrompt + $"\nTool source: {toolName}." }
        };
        foreach (var img in base64Images)
        {
            if (string.IsNullOrWhiteSpace(img))
                continue;
            var mime = ToolImagePayload.LooksLikeJpegBase64(img) ? "image/jpeg" : "image/png";
            contentParts.Add(new
            {
                type = "image_url",
                imageUrl = new { url = $"data:{mime};base64,{img}" }
            });
        }

        if (contentParts.Count < 2)
            return null;

        var payload = new
        {
            model,
            messages = new object[]
            {
                new { role = "user", content = contentParts }
            },
            maxTokens = 1024,
            temperature = 0.1
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            // Prefer per-request auth in case the factory client was built without a key yet.
            var key = _options.ResolveVisionApiKey();
            if (!string.IsNullOrWhiteSpace(key))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

            using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Vision sidecar HTTP {Status} model={Model}: {Body}",
                    (int)resp.StatusCode,
                    model,
                    Truncate(body, 400));
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                _logger.LogWarning("Vision sidecar: no choices in response");
                return null;
            }

            var msg = choices[0].TryGetProperty("message", out var m) ? m : default;
            var text = msg.ValueKind == JsonValueKind.Object && msg.TryGetProperty("content", out var c)
                ? c.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Vision sidecar: empty content");
                return null;
            }

            _logger.LogInformation(
                "Vision sidecar ok: model={Model} tool={Tool} chars={Chars}",
                model,
                toolName,
                text.Length);
            return text.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Vision sidecar failed model={Model}", model);
            return null;
        }
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s[..max] + "…";
    }
}
