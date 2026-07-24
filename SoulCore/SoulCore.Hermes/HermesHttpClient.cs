using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Hermes;

/// <summary>
/// Hermes OpenAI-compatible HTTP client (quarry default <c>http://127.0.0.1:8642</c>).
/// Auth via <c>SOULCORE_HERMES_API_KEY</c> / user-secrets — never committed.
/// </summary>
public sealed class HermesHttpClient : IHermesClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly HermesOptions _options;
    private readonly ILogger<HermesHttpClient> _logger;

    public HermesHttpClient(
        HttpClient http,
        IOptions<HermesOptions> options,
        ILogger<HermesHttpClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ApplyApiKey(_http, ResolveApiKey(_options));
    }

    public async Task<string> ChatAsync(
        string message,
        string? systemPreamble = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message must be non-empty.", nameof(message));

        if (string.IsNullOrWhiteSpace(ResolveApiKey(_options)))
        {
            throw new InvalidOperationException(
                $"Hermes chat requires API key via env {SecretNames.HermesApiKey} or user-secrets. " +
                "Health checks do not require a key.");
        }

        var messages = string.IsNullOrWhiteSpace(systemPreamble)
            ? new[] { new ChatMessage { Role = "user", Content = message } }
            : new[]
            {
                new ChatMessage { Role = "system", Content = systemPreamble.Trim() },
                new ChatMessage { Role = "user", Content = message }
            };

        var payload = new ChatCompletionRequest
        {
            Model = _options.Model,
            Messages = messages,
            MaxTokens = _options.MaxTokens
        };

        using var response = await _http.PostAsJsonAsync(
            "v1/chat/completions",
            payload,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Hermes chat failed: {Status} {Body}",
                (int)response.StatusCode,
                TextUtil.Truncate(body, 400));
            response.EnsureSuccessStatusCode();
        }

        var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions);
        return parsed?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    /// <summary>GET /health — no API key required on quarry Hermes.</summary>
    public async Task<string> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("health", cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return body;
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

    private sealed class ChatCompletionRequest
    {
        public string Model { get; set; } = string.Empty;
        public ChatMessage[] Messages { get; set; } = Array.Empty<ChatMessage>();
        public int? MaxTokens { get; set; }
    }

    private sealed class ChatMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    private sealed class ChatCompletionResponse
    {
        public ChatChoice[]? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        public ChatMessage? Message { get; set; }
    }
}
