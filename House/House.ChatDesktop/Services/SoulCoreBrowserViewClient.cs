using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace House.ChatDesktop.Services;

/// <summary>FED-196: polls Host <c>GET /browser/view</c> for Victoria's Playwright stream.</summary>
public sealed class BrowserViewSnapshot
{
    public bool Reachable { get; init; }
    public bool HasImage { get; init; }
    public string? Url { get; init; }
    public string? Title { get; init; }
    public string? LastAction { get; init; }
    public string? WaitingOnYou { get; init; }
    public string? Backend { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? Detail { get; init; }
    public byte[]? ImageBytes { get; init; }
}

public sealed class SoulCoreBrowserViewClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public SoulCoreBrowserViewClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    public static Uri MetaUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/browser/view");

    public static Uri ImageUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/browser/view/image");

    public async Task<BrowserViewSnapshot> GetAsync(
        bool includeImage = true,
        CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new BrowserViewSnapshot
            {
                Reachable = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var metaResponse = await _http.GetAsync(MetaUri, cancellationToken).ConfigureAwait(false);
            if (!metaResponse.IsSuccessStatusCode)
            {
                return new BrowserViewSnapshot
                {
                    Reachable = true,
                    Detail = $"HTTP {(int)metaResponse.StatusCode}"
                };
            }

            var dto = await metaResponse.Content.ReadFromJsonAsync<MetaDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            byte[]? imageBytes = null;
            if (includeImage && dto is { HasImage: true })
            {
                using var imgResponse = await _http.GetAsync(ImageUri, cancellationToken).ConfigureAwait(false);
                if (imgResponse.IsSuccessStatusCode)
                    imageBytes = await imgResponse.Content.ReadAsByteArrayAsync(cancellationToken)
                        .ConfigureAwait(false);
            }

            return new BrowserViewSnapshot
            {
                Reachable = true,
                HasImage = dto?.HasImage == true && imageBytes is { Length: > 0 },
                Url = dto?.Url,
                Title = dto?.Title,
                LastAction = dto?.LastAction,
                WaitingOnYou = dto?.WaitingOnYou,
                Backend = dto?.Backend,
                UpdatedAt = dto?.UpdatedAt,
                ImageBytes = imageBytes,
                Detail = dto?.Note
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new BrowserViewSnapshot { Reachable = false, Detail = ex.Message };
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed class MetaDto
    {
        [JsonPropertyName("hasImage")]
        public bool HasImage { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("lastAction")]
        public string? LastAction { get; set; }

        [JsonPropertyName("waitingOnYou")]
        public string? WaitingOnYou { get; set; }

        [JsonPropertyName("backend")]
        public string? Backend { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }
}
