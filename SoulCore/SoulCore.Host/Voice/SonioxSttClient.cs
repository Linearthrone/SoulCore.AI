using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Host.Voice;

/// <summary>PROP-21: Soniox async file STT (upload → transcription → transcript).</summary>
public sealed class SonioxSttClient : ISttClient
{
    private readonly HttpClient _http;
    private readonly VoiceOptions _options;
    private readonly ILogger<SonioxSttClient> _logger;

    public SonioxSttClient(HttpClient http, IOptions<VoiceOptions> options, ILogger<SonioxSttClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> TranscribeAsync(
        byte[] wavBytes,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wavBytes);
        if (wavBytes.Length == 0)
            return string.Empty;
        EnsureKey();

        var name = string.IsNullOrWhiteSpace(fileName) ? "audio.wav" : fileName.Trim();
        var fileId = await UploadFileAsync(wavBytes, name, cancellationToken).ConfigureAwait(false);
        var transcriptionId = await CreateTranscriptionAsync(fileId, cancellationToken).ConfigureAwait(false);
        await WaitCompletedAsync(transcriptionId, cancellationToken).ConfigureAwait(false);
        return await FetchTranscriptAsync(transcriptionId, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureKey()
    {
        if (!_options.HasApiKey)
            throw new InvalidOperationException("Voice:ApiKey / SONIOX_API_KEY is not configured");
    }

    private async Task<string> UploadFileAsync(byte[] bytes, string fileName, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);

        using var response = await _http.PostAsync("v1/files", form, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Soniox file upload failed {Status}: {Body}", (int)response.StatusCode, Truncate(body));
            throw new InvalidOperationException($"Soniox upload failed ({(int)response.StatusCode})");
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("id", out var idProp)
            || idProp.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(idProp.GetString()))
            throw new InvalidOperationException("Soniox upload response missing id");

        return idProp.GetString()!;
    }

    private async Task<string> CreateTranscriptionAsync(string fileId, CancellationToken ct)
    {
        var payload = new
        {
            model = _options.SttAsyncModel,
            file_id = fileId
        };
        using var response = await _http.PostAsJsonAsync("v1/transcriptions", payload, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Soniox transcription create failed {Status}: {Body}", (int)response.StatusCode, Truncate(body));
            throw new InvalidOperationException($"Soniox transcription create failed ({(int)response.StatusCode})");
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("id", out var idProp)
            || idProp.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(idProp.GetString()))
            throw new InvalidOperationException("Soniox transcription response missing id");

        return idProp.GetString()!;
    }

    private async Task WaitCompletedAsync(string transcriptionId, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(10, _options.TimeoutSeconds));
        while (DateTimeOffset.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            using var response = await _http.GetAsync($"v1/transcriptions/{transcriptionId}", ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Soniox transcription poll failed {Status}: {Body}", (int)response.StatusCode, Truncate(body));
                throw new InvalidOperationException($"Soniox transcription poll failed ({(int)response.StatusCode})");
            }

            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.TryGetProperty("status", out var st)
                ? st.GetString() ?? ""
                : "";
            if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
                return;
            if (string.Equals(status, "error", StringComparison.OrdinalIgnoreCase))
            {
                var err = doc.RootElement.TryGetProperty("error_message", out var em) ? em.GetString() : body;
                throw new InvalidOperationException($"Soniox transcription error: {Truncate(err ?? "")}");
            }

            await Task.Delay(400, ct).ConfigureAwait(false);
        }

        throw new TimeoutException("Soniox transcription timed out");
    }

    private async Task<string> FetchTranscriptAsync(string transcriptionId, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"v1/transcriptions/{transcriptionId}/transcript", ct)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Soniox transcript fetch failed {Status}: {Body}", (int)response.StatusCode, Truncate(body));
            throw new InvalidOperationException($"Soniox transcript fetch failed ({(int)response.StatusCode})");
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("text", out var textProp)
                && textProp.ValueKind == JsonValueKind.String)
                return textProp.GetString()?.Trim() ?? string.Empty;

            if (doc.RootElement.TryGetProperty("tokens", out var tokens)
                && tokens.ValueKind == JsonValueKind.Array)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var t in tokens.EnumerateArray())
                {
                    if (t.TryGetProperty("text", out var tt) && tt.ValueKind == JsonValueKind.String)
                        sb.Append(tt.GetString());
                }

                return sb.ToString().Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Soniox transcript parse failed: {Body}", Truncate(body));
        }

        return body.Trim();
    }

    private static string Truncate(string s) =>
        s.Length <= 200 ? s : s[..197] + "...";
}
