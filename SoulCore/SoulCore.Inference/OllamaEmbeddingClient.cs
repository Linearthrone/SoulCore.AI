using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference;

/// <summary>
/// Ollama embedding client via <c>POST /api/embeddings</c> (<c>{ model, prompt }</c> → <c>embedding</c>).
/// When Unreal is live and <see cref="InferenceOptions.SkipEmbeddingsWhenUeLive"/> is set,
/// returns an empty vector so callers fall back to recency recall (VRAM policy).
/// </summary>
public sealed class OllamaEmbeddingClient : IEmbeddingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly InferenceOptions _options;
    private readonly ILogger<OllamaEmbeddingClient> _logger;
    private readonly IUeLiveSignal _ueLive;

    public OllamaEmbeddingClient(
        HttpClient http,
        IOptions<InferenceOptions> options,
        ILogger<OllamaEmbeddingClient> logger,
        IUeLiveSignal? ueLive = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ueLive = ueLive ?? new NullUeLiveSignal();
    }

    public bool IsEnabled => true;

    public string Model
    {
        get
        {
            var ueLive = _ueLive.IsUeLive;
            if (InferenceModelRouting.ShouldSkipEmbeddings(_options, ueLive))
                return "(skipped-ue-live)";
            return InferenceModelRouting.ResolveEmbeddingModel(_options, ueLive);
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text to embed must be non-empty.", nameof(text));

        var ueLive = _ueLive.IsUeLive;
        if (InferenceModelRouting.ShouldSkipEmbeddings(_options, ueLive))
        {
            _logger.LogDebug("Skipping embeddings while Unreal is live (SkipEmbeddingsWhenUeLive)");
            return Array.Empty<float>();
        }

        var model = InferenceModelRouting.ResolveEmbeddingModel(_options, ueLive);
        var payload = new OllamaEmbeddingsRequest
        {
            Model = model,
            Prompt = text.Trim()
        };

        using var response = await _http.PostAsJsonAsync(
            "api/embeddings",
            payload,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Ollama embeddings failed: {Status} {Body}",
                (int)response.StatusCode,
                TextUtil.Truncate(body, 400));
            response.EnsureSuccessStatusCode();
        }

        var parsed = JsonSerializer.Deserialize<OllamaEmbeddingsResponse>(body, JsonOptions);
        if (parsed?.Embedding is null || parsed.Embedding.Length == 0)
            throw new InvalidOperationException("Ollama embeddings response missing embedding vector.");

        return parsed.Embedding;
    }

    private sealed class OllamaEmbeddingsRequest
    {
        public string Model { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
    }

    private sealed class OllamaEmbeddingsResponse
    {
        public float[]? Embedding { get; set; }
    }
}
