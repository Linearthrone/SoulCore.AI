using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace House.ChatDesktop.Services;

public sealed class DesktopViewGalleryItem
{
    public string FileName { get; init; } = "";
    public string? Path { get; init; }
    public string? Source { get; init; }
    public string? Format { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public DateTimeOffset? CapturedAt { get; init; }
    public string? Action { get; init; }
    public string? ImageUrl { get; init; }
}

public sealed class DesktopViewSnapshot
{
    public bool HasImage { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int? CursorX { get; init; }
    public int? CursorY { get; init; }
    public string? LastAction { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public bool SoftCursorRestore { get; init; }
    public string? Format { get; init; }
    /// <summary>desktop | eyes | browser — which capture path produced the frame.</summary>
    public string? Source { get; init; }
    /// <summary>Filesystem path of the latest gallery (or source) file on the Host machine.</summary>
    public string? DiskPath { get; init; }
    public string? GalleryDir { get; init; }
    public IReadOnlyList<DesktopViewGalleryItem> Recent { get; init; } = Array.Empty<DesktopViewGalleryItem>();
    public bool Reachable { get; init; }
    public string? Detail { get; init; }
    public byte[]? ImageBytes { get; init; }
}

/// <summary>Polls Host <c>GET /desktop/view</c> + image for Presence “Victoria's screen”.</summary>
public sealed class SoulCoreDesktopViewClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public SoulCoreDesktopViewClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    public static Uri MetaUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/desktop/view");

    public static Uri ImageUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/desktop/view/image");

    public static Uri GalleryImageUri(string fileName) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/desktop/view/gallery/{Uri.EscapeDataString(fileName)}");

    public async Task<DesktopViewSnapshot> GetAsync(
        bool includeImage = true,
        CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new DesktopViewSnapshot
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
                return new DesktopViewSnapshot
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

            var recent = (dto?.Recent ?? Array.Empty<GalleryDto>())
                .Select(g => new DesktopViewGalleryItem
                {
                    FileName = g.FileName ?? "",
                    Path = g.Path,
                    Source = g.Source,
                    Format = g.Format,
                    Width = g.Width,
                    Height = g.Height,
                    CapturedAt = g.CapturedAt,
                    Action = g.Action,
                    ImageUrl = g.ImageUrl
                })
                .ToArray();

            return new DesktopViewSnapshot
            {
                Reachable = true,
                HasImage = dto?.HasImage == true && imageBytes is { Length: > 0 },
                Width = dto?.Width ?? 0,
                Height = dto?.Height ?? 0,
                CursorX = dto?.CursorX,
                CursorY = dto?.CursorY,
                LastAction = dto?.LastAction,
                UpdatedAt = dto?.UpdatedAt,
                SoftCursorRestore = dto?.SoftCursorRestore ?? true,
                Format = dto?.Format,
                Source = dto?.Source,
                DiskPath = dto?.DiskPath,
                GalleryDir = dto?.GalleryDir,
                Recent = recent,
                ImageBytes = imageBytes,
                Detail = dto?.Note
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new DesktopViewSnapshot { Reachable = false, Detail = ex.Message };
        }
    }

    public async Task<byte[]?> GetGalleryImageAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return null;

        try
        {
            using var response = await _http.GetAsync(GalleryImageUri(fileName), cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed class MetaDto
    {
        [JsonPropertyName("hasImage")]
        public bool HasImage { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("cursorX")]
        public int? CursorX { get; set; }

        [JsonPropertyName("cursorY")]
        public int? CursorY { get; set; }

        [JsonPropertyName("lastAction")]
        public string? LastAction { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonPropertyName("softCursorRestore")]
        public bool SoftCursorRestore { get; set; }

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("diskPath")]
        public string? DiskPath { get; set; }

        [JsonPropertyName("galleryDir")]
        public string? GalleryDir { get; set; }

        [JsonPropertyName("recent")]
        public GalleryDto[]? Recent { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    private sealed class GalleryDto
    {
        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("capturedAt")]
        public DateTimeOffset? CapturedAt { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }
    }
}
