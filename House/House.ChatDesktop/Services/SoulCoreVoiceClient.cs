using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace House.ChatDesktop.Services;

/// <summary>Host voice proxies: /api/stt, /api/voice/health.</summary>
public sealed class SoulCoreVoiceClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(90) };

    private static Uri SttUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/stt");

    private static Uri HealthUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/voice/health");

    public async Task<(bool Ok, string? Text, string? Error)> TranscribeAsync(
        byte[] wavBytes,
        string fileName = "ptt.wav",
        CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return (false, null, $"Non-loopback host blocked: {ConnectionDefaults.Host}");

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(wavBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(content, "audio", fileName);

        try
        {
            using var response = await _http.PostAsync(SttUri, form, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var root = doc.RootElement;
            var text = root.TryGetProperty("text", out var t) ? t.GetString() : null;
            var ok = root.TryGetProperty("ok", out var o) && o.ValueKind == System.Text.Json.JsonValueKind.True;
            if (!response.IsSuccessStatusCode || !ok)
            {
                var err = root.TryGetProperty("error", out var e) ? e.GetString() : body;
                return (false, null, err ?? $"HTTP {(int)response.StatusCode}");
            }

            return (true, text?.Trim() ?? "", null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<VoiceHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var snap = await _http.GetFromJsonAsync<VoiceHealthSnapshot>(HealthUri, cancellationToken)
                .ConfigureAwait(false);
            return snap ?? new VoiceHealthSnapshot();
        }
        catch
        {
            return new VoiceHealthSnapshot { Enabled = false };
        }
    }
}

public sealed class VoiceHealthSnapshot
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("stt")]
    public VoiceEndpointHealth? Stt { get; set; }

    [JsonPropertyName("tts")]
    public VoiceEndpointHealth? Tts { get; set; }

    [JsonPropertyName("playOnHostSpeakers")]
    public bool PlayOnHostSpeakers { get; set; }

    [JsonPropertyName("playInUnreal")]
    public bool PlayInUnreal { get; set; }
}

public sealed class VoiceEndpointHealth
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }
}
