using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace House.ChatDesktop.Services;

/// <summary>Shared helpers for the PROP-22 operator REST clients (loopback only, companion auth).</summary>
internal static class OperatorHttp
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Uri Uri(string pathAndQuery) =>
        new(ConnectionDefaults.HttpBase + pathAndQuery);

    public static bool IsLoopback => ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host);

    public static HttpRequestMessage Request(HttpMethod method, string pathAndQuery, object? body = null)
    {
        var request = new HttpRequestMessage(method, Uri(pathAndQuery));
        var token = CompanionToken.Resolve();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("X-Api-Key", token);
        }

        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        return request;
    }

    public static async Task<string> ErrorTextAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("error", out var err)
                    && err.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(err.GetString()))
                {
                    return err.GetString()!;
                }
            }
        }
        catch
        {
            // fall through to the status code
        }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.NotFound => "Host does not have this route yet — restart Host.",
            System.Net.HttpStatusCode.Unauthorized => "HTTP 401 — save the companion token in Settings.",
            _ => $"HTTP {(int)response.StatusCode}"
        };
    }

    public static bool IsExpectedFailure(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or OperationCanceledException
            or JsonException or NotSupportedException or InvalidOperationException;

    // ── tolerant JSON readers ─────────────────────────────────────────────────

    public static string Str(JsonElement el, string name)
    {
        if (el.ValueKind == JsonValueKind.Object
            && el.TryGetProperty(name, out var p)
            && p.ValueKind == JsonValueKind.String)
        {
            return p.GetString() ?? "";
        }

        return "";
    }

    public static string? StrOrNull(JsonElement el, string name)
    {
        var s = Str(el, name);
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    public static long Long(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var p))
            return 0;
        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetInt64(out var n) => n,
            JsonValueKind.String when long.TryParse(p.GetString(), out var n) => n,
            _ => 0
        };
    }

    public static int Int(JsonElement el, string name, int fallback = 0)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var p))
            return fallback;
        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(p.GetString(), out var n) => n,
            _ => fallback
        };
    }

    public static bool Bool(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var p))
            return false;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when p.TryGetInt32(out var n) => n != 0,
            JsonValueKind.String => p.GetString() is "1" or "true" or "True",
            _ => false
        };
    }
}
