using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace House.ChatDesktop.Services;

/// <summary>FED-196 / PROP-14: polls Host <c>GET /browser/view</c> (+ optional embed).</summary>
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
    public bool EmbedPane { get; init; }
    public string EmbedSurface { get; init; } = "none";
    public int? CursorX { get; init; }
    public int? CursorY { get; init; }
    public string? CursorState { get; init; }
    public DateTimeOffset? CursorAt { get; init; }
    public int FrameWidth { get; init; }
    public int FrameHeight { get; init; }
}

public sealed class BrowserEmbedSnapshot
{
    public bool Reachable { get; init; }
    public string Mode { get; init; } = "disabled";
    public string Surface { get; init; } = "none";
    public long Hwnd { get; init; }
    public int Pid { get; init; }
    public string? Title { get; init; }
    public string? Detail { get; init; }
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

    public static Uri EmbedUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/browser/embed");

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
            // Skip JPEG download when embed pane is preferred (Presence hosts HWND instead).
            var wantImage = includeImage && dto is { HasImage: true } && dto.EmbedPane != true;
            if (wantImage)
            {
                using var imgResponse = await _http.GetAsync(ImageUri, cancellationToken).ConfigureAwait(false);
                if (imgResponse.IsSuccessStatusCode)
                    imageBytes = await imgResponse.Content.ReadAsByteArrayAsync(cancellationToken)
                        .ConfigureAwait(false);
            }
            else if (includeImage && dto is { HasImage: true, EmbedPane: true })
            {
                // Still fetch a light fallback frame if embed fails later this tick.
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
                Detail = dto?.Note,
                EmbedPane = dto?.EmbedPane == true,
                EmbedSurface = dto?.EmbedSurface ?? "none",
                CursorX = dto?.CursorX,
                CursorY = dto?.CursorY,
                CursorState = dto?.CursorState,
                CursorAt = dto?.CursorAt,
                FrameWidth = dto?.FrameWidth ?? 0,
                FrameHeight = dto?.FrameHeight ?? 0
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new BrowserViewSnapshot { Reachable = false, Detail = ex.Message };
        }
    }

    public async Task<BrowserEmbedSnapshot> GetEmbedAsync(CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new BrowserEmbedSnapshot
            {
                Reachable = false,
                Mode = "disabled",
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var response = await _http.GetAsync(EmbedUri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new BrowserEmbedSnapshot
                {
                    Reachable = true,
                    Mode = "fallback",
                    Detail = $"HTTP {(int)response.StatusCode}"
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<EmbedDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return new BrowserEmbedSnapshot
            {
                Reachable = true,
                Mode = dto?.Mode ?? "fallback",
                Surface = dto?.Surface ?? "none",
                Hwnd = dto?.Hwnd ?? 0,
                Pid = dto?.Pid ?? 0,
                Title = dto?.Title,
                Detail = dto?.Detail
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new BrowserEmbedSnapshot { Reachable = false, Mode = "fallback", Detail = ex.Message };
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

        [JsonPropertyName("embedPane")]
        public bool EmbedPane { get; set; }

        [JsonPropertyName("embedSurface")]
        public string? EmbedSurface { get; set; }

        [JsonPropertyName("cursorX")]
        public int? CursorX { get; set; }

        [JsonPropertyName("cursorY")]
        public int? CursorY { get; set; }

        [JsonPropertyName("cursorState")]
        public string? CursorState { get; set; }

        [JsonPropertyName("cursorAt")]
        public DateTimeOffset? CursorAt { get; set; }

        [JsonPropertyName("frameWidth")]
        public int FrameWidth { get; set; }

        [JsonPropertyName("frameHeight")]
        public int FrameHeight { get; set; }
    }

    private sealed class EmbedDto
    {
        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        [JsonPropertyName("surface")]
        public string? Surface { get; set; }

        [JsonPropertyName("hwnd")]
        public long Hwnd { get; set; }

        [JsonPropertyName("pid")]
        public int Pid { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }
    }
}
