using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Host.Voice;

public sealed class WhisperSttClient : ISttClient
{
    private readonly HttpClient _http;
    private readonly VoiceOptions _options;
    private readonly ILogger<WhisperSttClient> _logger;

    public WhisperSttClient(HttpClient http, IOptions<VoiceOptions> options, ILogger<WhisperSttClient> logger)
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

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(wavBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        var name = string.IsNullOrWhiteSpace(fileName) ? "audio.wav" : fileName.Trim();
        form.Add(fileContent, "audio", name);

        var url = "transcribe";
        if (_http.BaseAddress is null)
            url = $"{_options.SttUrl.TrimEnd('/')}/transcribe";
        using var response = await _http.PostAsync(url, form, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("STT failed {Status}: {Body}", (int)response.StatusCode, Truncate(body));
            throw new InvalidOperationException($"STT failed ({(int)response.StatusCode}): {Truncate(body)}");
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("text", out var textProp)
                && textProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return textProp.GetString()?.Trim() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "STT response was not JSON text: {Body}", Truncate(body));
        }

        return body.Trim();
    }

    private static string Truncate(string s) =>
        s.Length <= 200 ? s : s[..197] + "...";
}
