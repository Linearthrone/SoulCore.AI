using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Host.Voice;

public sealed class ChatterboxTtsClient : ITtsClient
{
    private readonly HttpClient _http;
    private readonly VoiceOptions _options;
    private readonly ILogger<ChatterboxTtsClient> _logger;

    public ChatterboxTtsClient(HttpClient http, IOptions<VoiceOptions> options, ILogger<ChatterboxTtsClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]?> SynthesizeAsync(
        string text,
        string? voice = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var payload = new ChatterboxRequest
        {
            Text = text.Trim(),
            Voice = string.IsNullOrWhiteSpace(voice) ? _options.DefaultVoice : voice.Trim()
        };

        var url = _http.BaseAddress is null
            ? _options.TtsUrl.TrimEnd('/') + "/"
            : "";
        using var response = await _http.PostAsJsonAsync(url, payload, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("TTS failed {Status}: {Body}", (int)response.StatusCode, Truncate(err));
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length < 44)
        {
            _logger.LogWarning("TTS returned empty/invalid WAV ({Len} bytes)", bytes.Length);
            return null;
        }

        return bytes;
    }

    private static string Truncate(string s) =>
        s.Length <= 200 ? s : s[..197] + "...";

    private sealed class ChatterboxRequest
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("voice")]
        public string Voice { get; set; } = "default";
    }
}
