using System.Net.Http;
using System.Text.Json;

namespace House.ChatDesktop.Services;

/// <summary>PROP-22.9 — Host log tail. Lines are real or the list is empty; nothing is invented.</summary>
public sealed record SystemLogFetch(bool Available, string? Path, IReadOnlyList<string> Lines, string? Problem);

/// <summary>Host <c>GET /settings/system-log?lines=N</c> (PROP-22 §3.4). Secrets are masked by the Host.</summary>
public sealed class SoulCoreSystemLogClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };

    public async Task<SystemLogFetch> GetAsync(int lines = 200, CancellationToken ct = default)
    {
        if (!OperatorHttp.IsLoopback)
            return new SystemLogFetch(false, null, Array.Empty<string>(), "The log stays on this machine.");

        try
        {
            var take = Math.Clamp(lines, 1, 2000);
            using var request = OperatorHttp.Request(HttpMethod.Get, $"/settings/system-log?lines={take}");
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new SystemLogFetch(
                    false,
                    null,
                    Array.Empty<string>(),
                    await OperatorHttp.ErrorTextAsync(response, ct).ConfigureAwait(false));
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Parse(json);
        }
        catch (Exception ex) when (OperatorHttp.IsExpectedFailure(ex))
        {
            return new SystemLogFetch(false, null, Array.Empty<string>(), $"System log unavailable: {ex.GetType().Name}");
        }
    }

    internal static SystemLogFetch Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new SystemLogFetch(false, null, Array.Empty<string>(), "System log response was empty.");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new SystemLogFetch(false, null, Array.Empty<string>(), "System log response was not an object.");

            var available = OperatorHttp.Bool(root, "available");
            var path = OperatorHttp.StrOrNull(root, "path");
            var lines = new List<string>();
            if (root.TryGetProperty("lines", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String)
                        lines.Add(el.GetString() ?? "");
                }
            }

            return new SystemLogFetch(available, path, lines, null);
        }
        catch (JsonException ex)
        {
            return new SystemLogFetch(false, null, Array.Empty<string>(), $"System log JSON error: {ex.Message}");
        }
    }

    public void Dispose() => _http.Dispose();
}
