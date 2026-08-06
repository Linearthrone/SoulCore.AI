using System.Net.Http;
using System.Net.Http.Headers;

namespace House.ChatDesktop.Services;

/// <summary>
/// Fetches companion MMS assets from Host <c>/api/companion/v1/media/{id}/file</c>.
/// </summary>
public sealed class CompanionMediaClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static Uri MediaFileUri(string mediaId) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/companion/v1/media/{Uri.EscapeDataString(mediaId)}/file");

    public async Task<(bool Ok, byte[]? Bytes, string? ContentType, string? Error)> TryGetAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
            return (false, null, null, "mediaId required");

        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return (false, null, null, $"Non-loopback host blocked: {ConnectionDefaults.Host}");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, MediaFileUri(mediaId));
            var token = CompanionToken.Resolve();
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return (false, null, null, $"HTTP {(int)response.StatusCode}");

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var contentType = response.Content.Headers.ContentType?.MediaType;
            return (true, bytes, contentType, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return (false, null, null, ex.Message);
        }
    }

    public void Dispose() => _http.Dispose();
}
