using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace House.ChatDesktop.Services;

public sealed class ToolsAccessSnapshot
{
    public bool AllowDesktopCapture { get; init; }
    public bool AllowBrowserCapture { get; init; }
    public bool AllowComputerControl { get; init; }
    public bool SoftCursorRestore { get; init; } = true;
    public bool AllowMt4Read { get; init; }
    public bool AllowMt4Trade { get; init; }
    public bool AllowEmailRead { get; init; }
    public bool AllowEmailSend { get; init; }
    public bool AllowEmailDelete { get; init; }
    public string? DesktopBackend { get; init; }
    public string? BrowserBackend { get; init; }
    public string? Mt4Backend { get; init; }
    public bool CuaDriverAvailable { get; init; }
    public string? CuaDriverPath { get; init; }
    public string? Scope { get; init; }
    public string? Note { get; init; }
    public bool Reachable { get; init; }
    public string? Detail { get; init; }
}

public sealed class SoulCoreToolsSettingsClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public SoulCoreToolsSettingsClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public static Uri SettingsUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/settings/tools");

    public async Task<ToolsAccessSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new ToolsAccessSnapshot
            {
                Reachable = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var response = await _http.GetAsync(SettingsUri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ToolsAccessSnapshot
                {
                    Reachable = true,
                    Detail = $"HTTP {(int)response.StatusCode}"
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<ToolsDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return FromDto(dto, reachable: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new ToolsAccessSnapshot { Reachable = false, Detail = ex.Message };
        }
    }

    public async Task<ToolsAccessSnapshot> PatchAsync(
        bool? allowDesktopCapture = null,
        bool? allowBrowserCapture = null,
        bool? allowComputerControl = null,
        bool? softCursorRestore = null,
        bool? allowMt4Read = null,
        bool? allowMt4Trade = null,
        bool? allowEmailRead = null,
        bool? allowEmailSend = null,
        bool? allowEmailDelete = null,
        CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new ToolsAccessSnapshot
            {
                Reachable = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            var doc = new Dictionary<string, bool>();
            if (allowDesktopCapture is { } d) doc["allowDesktopCapture"] = d;
            if (allowBrowserCapture is { } b) doc["allowBrowserCapture"] = b;
            if (allowComputerControl is { } c) doc["allowComputerControl"] = c;
            if (softCursorRestore is { } soft) doc["softCursorRestore"] = soft;
            if (allowMt4Read is { } r) doc["allowMt4Read"] = r;
            if (allowMt4Trade is { } t) doc["allowMt4Trade"] = t;
            if (allowEmailRead is { } er) doc["allowEmailRead"] = er;
            if (allowEmailSend is { } es) doc["allowEmailSend"] = es;
            if (allowEmailDelete is { } ed) doc["allowEmailDelete"] = ed;

            var json = JsonSerializer.Serialize(doc);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(SettingsUri, content, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ToolsAccessSnapshot
                {
                    Reachable = true,
                    Detail = $"HTTP {(int)response.StatusCode}"
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<ToolsDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return FromDto(dto, reachable: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new ToolsAccessSnapshot { Reachable = false, Detail = ex.Message };
        }
    }

    private static ToolsAccessSnapshot FromDto(ToolsDto? dto, bool reachable) => new()
    {
        Reachable = reachable,
        AllowDesktopCapture = dto?.AllowDesktopCapture ?? false,
        AllowBrowserCapture = dto?.AllowBrowserCapture ?? false,
        AllowComputerControl = dto?.AllowComputerControl ?? false,
        SoftCursorRestore = dto?.SoftCursorRestore ?? true,
        AllowMt4Read = dto?.AllowMt4Read ?? false,
        AllowMt4Trade = dto?.AllowMt4Trade ?? false,
        AllowEmailRead = dto?.AllowEmailRead ?? false,
        AllowEmailSend = dto?.AllowEmailSend ?? false,
        AllowEmailDelete = dto?.AllowEmailDelete ?? false,
        DesktopBackend = dto?.DesktopBackend,
        BrowserBackend = dto?.BrowserBackend,
        Mt4Backend = dto?.Mt4Backend,
        CuaDriverAvailable = dto?.CuaDriverAvailable ?? false,
        CuaDriverPath = dto?.CuaDriverPath,
        Scope = dto?.Scope,
        Note = dto?.Note
    };

    public void Dispose() => _http.Dispose();

    private sealed class ToolsDto
    {
        [JsonPropertyName("allowDesktopCapture")]
        public bool AllowDesktopCapture { get; set; }

        [JsonPropertyName("allowBrowserCapture")]
        public bool AllowBrowserCapture { get; set; }

        [JsonPropertyName("allowComputerControl")]
        public bool AllowComputerControl { get; set; }

        [JsonPropertyName("softCursorRestore")]
        public bool SoftCursorRestore { get; set; }

        [JsonPropertyName("allowMt4Read")]
        public bool AllowMt4Read { get; set; }

        [JsonPropertyName("allowMt4Trade")]
        public bool AllowMt4Trade { get; set; }

        [JsonPropertyName("allowEmailRead")]
        public bool AllowEmailRead { get; set; }

        [JsonPropertyName("allowEmailSend")]
        public bool AllowEmailSend { get; set; }

        [JsonPropertyName("allowEmailDelete")]
        public bool AllowEmailDelete { get; set; }

        [JsonPropertyName("desktopBackend")]
        public string? DesktopBackend { get; set; }

        [JsonPropertyName("browserBackend")]
        public string? BrowserBackend { get; set; }

        [JsonPropertyName("mt4Backend")]
        public string? Mt4Backend { get; set; }

        [JsonPropertyName("cuaDriverAvailable")]
        public bool CuaDriverAvailable { get; set; }

        [JsonPropertyName("cuaDriverPath")]
        public string? CuaDriverPath { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }
}
