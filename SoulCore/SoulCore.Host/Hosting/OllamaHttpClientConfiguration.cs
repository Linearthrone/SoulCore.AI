namespace SoulCore.Host.Hosting;

internal static class OllamaHttpClientConfiguration
{
    internal static Uri NormalizeBaseUri(string baseUrl)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/') + "/";
        return new Uri(trimmed, UriKind.Absolute);
    }

    internal static void Configure(
        HttpClient client,
        string baseUrl,
        int timeoutSeconds,
        string? apiKey)
    {
        client.BaseAddress = NormalizeBaseUri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds));
        client.DefaultRequestHeaders.Remove("Authorization");
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
    }
}
