using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace House.ChatDesktop.Services;

public sealed class EmailAccountSnapshot
{
    public string Id { get; init; } = "";
    public string Role { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Address { get; init; } = "";
    public string ImapHost { get; init; } = "";
    public int ImapPort { get; init; } = 993;
    public bool ImapUseSsl { get; init; } = true;
    public string SmtpHost { get; init; } = "";
    public int SmtpPort { get; init; } = 587;
    public bool SmtpUseSsl { get; init; }
    public string Username { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public bool HasPassword { get; init; }
    public bool IsConfigured { get; init; }
}

public sealed class EmailSettingsSnapshot
{
    public IReadOnlyList<EmailAccountSnapshot> Accounts { get; init; } = Array.Empty<EmailAccountSnapshot>();
    public string? Note { get; init; }
    public bool Reachable { get; init; }
    public string? Detail { get; init; }
}

public sealed class SoulCoreEmailSettingsClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public SoulCoreEmailSettingsClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    public static Uri SettingsUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/settings/email");

    public async Task<EmailSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new EmailSettingsSnapshot
            {
                Reachable = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, SettingsUri);
            AttachAuth(request);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new EmailSettingsSnapshot
                {
                    Reachable = true,
                    Detail = $"HTTP {(int)response.StatusCode}"
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<EmailListDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return FromListDto(dto, reachable: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new EmailSettingsSnapshot { Reachable = false, Detail = ex.Message };
        }
    }

    public async Task<EmailSettingsSnapshot> UpsertAsync(
        EmailAccountWriteDto account,
        CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new EmailSettingsSnapshot
            {
                Reachable = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            var json = JsonSerializer.Serialize(account, JsonOptions);
            using var request = new HttpRequestMessage(HttpMethod.Post, SettingsUri)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            AttachAuth(request);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new EmailSettingsSnapshot
                {
                    Reachable = true,
                    Detail = $"HTTP {(int)response.StatusCode}" +
                             (string.IsNullOrWhiteSpace(body) ? "" : $": {TrimDetail(body)}")
                };
            }

            // Re-fetch full list so the editor stays in sync across slots.
            return await GetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new EmailSettingsSnapshot { Reachable = false, Detail = ex.Message };
        }
    }

    private static void AttachAuth(HttpRequestMessage request)
    {
        var token = CompanionToken.Resolve();
        if (string.IsNullOrEmpty(token))
            return;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("X-Api-Key", token);
    }

    private static EmailSettingsSnapshot FromListDto(EmailListDto? dto, bool reachable) => new()
    {
        Reachable = reachable,
        Note = dto?.Note,
        Accounts = (dto?.Accounts ?? Array.Empty<EmailAccountDto>())
            .Select(a => new EmailAccountSnapshot
            {
                Id = a.Id ?? "",
                Role = a.Role ?? "",
                DisplayName = a.DisplayName ?? "",
                Address = a.Address ?? "",
                ImapHost = a.ImapHost ?? "",
                ImapPort = a.ImapPort > 0 ? a.ImapPort : 993,
                ImapUseSsl = a.ImapUseSsl,
                SmtpHost = a.SmtpHost ?? "",
                SmtpPort = a.SmtpPort > 0 ? a.SmtpPort : 587,
                SmtpUseSsl = a.SmtpUseSsl,
                Username = a.Username ?? "",
                Enabled = a.Enabled,
                HasPassword = a.HasPassword,
                IsConfigured = a.IsConfigured
            })
            .ToList()
    };

    private static string TrimDetail(string body)
    {
        var t = body.Trim();
        return t.Length <= 180 ? t : t[..180] + "…";
    }

    public void Dispose() => _http.Dispose();

    public sealed class EmailAccountWriteDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("imapHost")]
        public string? ImapHost { get; set; }

        [JsonPropertyName("imapPort")]
        public int? ImapPort { get; set; }

        [JsonPropertyName("imapUseSsl")]
        public bool? ImapUseSsl { get; set; }

        [JsonPropertyName("smtpHost")]
        public string? SmtpHost { get; set; }

        [JsonPropertyName("smtpPort")]
        public int? SmtpPort { get; set; }

        [JsonPropertyName("smtpUseSsl")]
        public bool? SmtpUseSsl { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("password")]
        public string? Password { get; set; }

        [JsonPropertyName("enabled")]
        public bool? Enabled { get; set; }
    }

    private sealed class EmailListDto
    {
        [JsonPropertyName("accounts")]
        public EmailAccountDto[]? Accounts { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    private sealed class EmailAccountDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("imapHost")]
        public string? ImapHost { get; set; }

        [JsonPropertyName("imapPort")]
        public int ImapPort { get; set; }

        [JsonPropertyName("imapUseSsl")]
        public bool ImapUseSsl { get; set; }

        [JsonPropertyName("smtpHost")]
        public string? SmtpHost { get; set; }

        [JsonPropertyName("smtpPort")]
        public int SmtpPort { get; set; }

        [JsonPropertyName("smtpUseSsl")]
        public bool SmtpUseSsl { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("hasPassword")]
        public bool HasPassword { get; set; }

        [JsonPropertyName("isConfigured")]
        public bool IsConfigured { get; set; }
    }
}
