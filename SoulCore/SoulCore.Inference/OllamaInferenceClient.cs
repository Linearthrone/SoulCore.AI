using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference;

/// <summary>
/// Ollama HTTP client (quarry default <c>http://127.0.0.1:11434</c>).
/// </summary>
public sealed class OllamaInferenceClient : IInferenceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly InferenceOptions _options;
    private readonly ILogger<OllamaInferenceClient> _logger;

    public OllamaInferenceClient(
        HttpClient http,
        IOptions<InferenceOptions> options,
        ILogger<OllamaInferenceClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> CompleteAsync(
        string prompt,
        string? systemPreamble = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt must be non-empty.", nameof(prompt));

        var payload = new OllamaGenerateRequest
        {
            Model = _options.Model,
            Prompt = prompt,
            System = string.IsNullOrWhiteSpace(systemPreamble) ? null : systemPreamble.Trim(),
            Stream = false,
            Options = new OllamaGenerateOptions { NumPredict = _options.MaxTokens }
        };

        using var response = await _http.PostAsJsonAsync(
            "api/generate",
            payload,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Ollama generate failed: {Status} {Body}",
                (int)response.StatusCode,
                TextUtil.Truncate(body, 400));
            response.EnsureSuccessStatusCode();
        }

        var parsed = JsonSerializer.Deserialize<OllamaGenerateResponse>(body, JsonOptions);
        return parsed?.Response ?? string.Empty;
    }

    private sealed class OllamaGenerateRequest
    {
        public string Model { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string? System { get; set; }
        public bool Stream { get; set; }
        public OllamaGenerateOptions? Options { get; set; }
    }

    private sealed class OllamaGenerateOptions
    {
        public int NumPredict { get; set; }
    }

    private sealed class OllamaGenerateResponse
    {
        public string? Response { get; set; }
    }
}
