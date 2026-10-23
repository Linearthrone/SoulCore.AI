using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SoulCore.Core.Persona;

namespace House.ChatDesktop.Services;

/// <summary>Host persona list / CRUD / set-active (PROP-15.3 wizard + PROP-15.4 switcher).</summary>
public sealed class SoulCorePersonaClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;

    public SoulCorePersonaClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    public static Uri ListUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/personas");

    public static Uri ActiveUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/personas/active");

    public static Uri ModeUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/personas/mode");

    /// <summary>POST body <c>{"mode":"one"}</c> or <c>{"mode":"many"}</c>.</summary>
    public static string ModeRequestJson(string? mode) =>
        PersonaChatMode.IsMany(mode)
            ? "{\"mode\":\"many\"}"
            : "{\"mode\":\"one\"}";

    public static Uri PersonaUri(string personaId) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/personas/{Uri.EscapeDataString(personaId)}");

    public static Uri ActivateUri(string personaId) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/personas/{Uri.EscapeDataString(personaId)}/activate");

    public static Uri ArchiveUri(string personaId) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/personas/{Uri.EscapeDataString(personaId)}/archive");

    /// <summary>POST body for description-draft. Empty object: no notes, and not a pack write.</summary>
    public const string DescriptionDraftRequestJson = "{}";

    public static Uri DescriptionDraftUri(string personaId) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/personas/{Uri.EscapeDataString(personaId)}/description-draft");

    /// <summary>JSON object <c>{"summary":"..."}</c>. The caller already built the text.</summary>
    public static string ArchiveRequestJson(string summary) =>
        "{\"summary\":" + JsonSerializer.Serialize(summary) + "}";

    public static Uri InferenceModelsUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/inference/models");

    public async Task<InferenceModelsSnapshot> ListInferenceModelsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new InferenceModelsSnapshot
            {
                Reachable = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var response = await _http.GetAsync(InferenceModelsUri, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new InferenceModelsSnapshot
                {
                    Reachable = true,
                    Available = false,
                    Detail = $"HTTP {(int)response.StatusCode}"
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<InferenceModelsDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var models = (dto?.Models ?? Array.Empty<string>())
                .Select(PersonaWizardLogic.NormalizeInferenceModel)
                .Where(m => m is not null)
                .Select(m => m!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new InferenceModelsSnapshot
            {
                Reachable = true,
                Available = dto?.Available ?? models.Length > 0,
                Models = models,
                HostModel = PersonaWizardLogic.NormalizeInferenceModel(dto?.HostModel),
                ResolvedInferenceModel = PersonaWizardLogic.NormalizeInferenceModel(dto?.ResolvedInferenceModel),
                Detail = dto?.Detail
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new InferenceModelsSnapshot { Reachable = false, Detail = ex.Message };
        }
    }

    public async Task<PersonaListSnapshot> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new PersonaListSnapshot
            {
                Reachable = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var response = await _http.GetAsync(ListUri, cancellationToken).ConfigureAwait(false);
            var code = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                var missing = code == 404;
                return new PersonaListSnapshot
                {
                    Reachable = true,
                    HttpStatus = code,
                    PersonaApiMissing = missing,
                    Detail = missing
                        ? BuildCurrencyStatus.HostNoPersonaApiMessage
                        : $"HTTP {code}"
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<ListDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var personas = (dto?.Personas ?? Array.Empty<PersonaDto>())
                .Select(FromDto)
                .Where(p => !string.IsNullOrWhiteSpace(p.PersonaId))
                .ToArray();

            return new PersonaListSnapshot
            {
                Reachable = true,
                HttpStatus = code,
                PersonaApiMissing = false,
                ActivePersonaId = dto?.ActivePersonaId,
                Personas = personas,
                Detail = null
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new PersonaListSnapshot { Reachable = false, Detail = ex.Message };
        }
    }

    public async Task<PersonaGetSnapshot> GetAsync(
        string personaId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(personaId))
        {
            return new PersonaGetSnapshot { Reachable = true, Ok = false, Detail = "personaId required" };
        }

        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new PersonaGetSnapshot
            {
                Reachable = false,
                Ok = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var response = await _http.GetAsync(PersonaUri(personaId.Trim()), cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new PersonaGetSnapshot
                {
                    Reachable = true,
                    Ok = false,
                    Detail = string.IsNullOrWhiteSpace(body)
                        ? $"HTTP {(int)response.StatusCode}"
                        : TrimDetail(body)
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<PersonaDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (dto is null)
                return new PersonaGetSnapshot { Reachable = true, Ok = false, Detail = "Empty response" };

            return new PersonaGetSnapshot
            {
                Reachable = true,
                Ok = true,
                Persona = FromDto(dto)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new PersonaGetSnapshot { Reachable = false, Ok = false, Detail = ex.Message };
        }
    }

    public async Task<PersonaWriteSnapshot> CreateAsync(
        PersonaPackWrite pack,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return await WriteAsync(HttpMethod.Post, ListUri, pack, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PersonaWriteSnapshot> UpdateAsync(
        string personaId,
        PersonaPackWrite pack,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (string.IsNullOrWhiteSpace(personaId))
        {
            return new PersonaWriteSnapshot
            {
                Reachable = true,
                Ok = false,
                Detail = "personaId required"
            };
        }

        pack.PersonaId = personaId.Trim();
        return await WriteAsync(HttpMethod.Put, PersonaUri(personaId.Trim()), pack, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PersonaActivateSnapshot> ActivateAsync(
        string personaId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(personaId))
        {
            return new PersonaActivateSnapshot
            {
                Reachable = true,
                Ok = false,
                Detail = "personaId required"
            };
        }

        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new PersonaActivateSnapshot
            {
                Reachable = false,
                Ok = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var response = await _http.PostAsync(ActivateUri(personaId.Trim()), content: null, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new PersonaActivateSnapshot
                {
                    Reachable = true,
                    Ok = false,
                    Detail = string.IsNullOrWhiteSpace(body)
                        ? $"HTTP {(int)response.StatusCode}"
                        : TrimDetail(body)
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<ActivateDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var pack = dto?.Persona is null ? null : FromDto(dto.Persona);
            return new PersonaActivateSnapshot
            {
                Reachable = true,
                Ok = true,
                ActivePersonaId = dto?.ActivePersonaId ?? pack?.PersonaId,
                Persona = pack,
                Note = dto?.Note,
                Detail = null
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new PersonaActivateSnapshot { Reachable = false, Ok = false, Detail = ex.Message };
        }
    }

    public async Task<PersonaArchiveSnapshot> ArchiveAsync(
        string personaId,
        string summary,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(personaId))
        {
            return new PersonaArchiveSnapshot
            {
                Reachable = true,
                Ok = false,
                Detail = "personaId required"
            };
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            return new PersonaArchiveSnapshot
            {
                Reachable = true,
                Ok = false,
                Detail = "summary is required."
            };
        }

        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new PersonaArchiveSnapshot
            {
                Reachable = false,
                Ok = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var content = new StringContent(
                ArchiveRequestJson(summary),
                Encoding.UTF8,
                "application/json");
            using var response = await _http.PostAsync(ArchiveUri(personaId.Trim()), content, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new PersonaArchiveSnapshot
                {
                    Reachable = true,
                    Ok = false,
                    Detail = string.IsNullOrWhiteSpace(body)
                        ? $"HTTP {(int)response.StatusCode}"
                        : TrimDetail(body)
                };
            }

            return new PersonaArchiveSnapshot { Reachable = true, Ok = true };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new PersonaArchiveSnapshot { Reachable = false, Ok = false, Detail = ex.Message };
        }
    }

    /// <summary>
    /// POST /api/personas/{id}/description-draft. Returns text only.
    /// Does not create or update a pack.
    /// </summary>
    public async Task<DescriptionDraftSnapshot> DraftPhysicalDescriptionAsync(
        string personaId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(personaId))
        {
            return new DescriptionDraftSnapshot
            {
                Reachable = true,
                Ok = false,
                Detail = "personaId required"
            };
        }

        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new DescriptionDraftSnapshot
            {
                Reachable = false,
                Ok = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var content = new StringContent(
                DescriptionDraftRequestJson,
                Encoding.UTF8,
                "application/json");
            using var response = await _http.PostAsync(
                    DescriptionDraftUri(personaId.Trim()),
                    content,
                    cancellationToken)
                .ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new DescriptionDraftSnapshot
                {
                    Reachable = true,
                    Ok = false,
                    Detail = string.IsNullOrWhiteSpace(body)
                        ? $"HTTP {(int)response.StatusCode}"
                        : TrimDetail(body)
                };
            }

            var dto = JsonSerializer.Deserialize<DescriptionDraftDto>(body, JsonOptions);
            if (dto?.Text is null)
            {
                return new DescriptionDraftSnapshot
                {
                    Reachable = true,
                    Ok = false,
                    Detail = "Description draft returned no text."
                };
            }

            return new DescriptionDraftSnapshot
            {
                Reachable = true,
                Ok = true,
                Text = dto.Text
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new DescriptionDraftSnapshot { Reachable = false, Ok = false, Detail = ex.Message };
        }
    }

    /// <summary>
    /// Save body for create and update. Traits are every catalog key.
    /// A value left on the catalog default is still sent.
    /// </summary>
    public static string BuildPersonaWriteJson(PersonaPackWrite pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var traits = PersonaWizardLogic.BuildTraitSaveMap(pack.Traits ?? new PersonaTraitScalesInfo());
        var payload = new
        {
            personaId = pack.PersonaId,
            displayName = pack.DisplayName,
            humanAddress = pack.HumanAddress,
            contactId = pack.ContactId,
            charterSeedPath = pack.CharterSeedPath,
            playwrightProfileDir = pack.PlaywrightProfileDir,
            vmWindowTitle = pack.VmWindowTitle,
            inferenceModel = PersonaWizardLogic.NormalizeInferenceModel(pack.InferenceModel),
            identityBlurb = pack.IdentityBlurb,
            physicalDescription = PersonaWizardLogic.NormalizePhysicalDescription(pack.PhysicalDescription),
            toolPolicy = pack.ToolPolicy is null
                ? null
                : new
                {
                    allowDesktop = pack.ToolPolicy.AllowDesktop,
                    allowBrowser = pack.ToolPolicy.AllowBrowser,
                    allowEmail = pack.ToolPolicy.AllowEmail
                },
            traits
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public async Task<PersonaModeSnapshot> GetModeAsync(CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new PersonaModeSnapshot
            {
                Reachable = false,
                Ok = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var response = await _http.GetAsync(ModeUri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new PersonaModeSnapshot
                {
                    Reachable = true,
                    Ok = false,
                    Detail = $"HTTP {(int)response.StatusCode}"
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<ModeDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var mode = dto?.Mode?.Trim();
            if (mode is not (PersonaChatMode.One or PersonaChatMode.Many))
            {
                return new PersonaModeSnapshot
                {
                    Reachable = true,
                    Ok = false,
                    Detail = "mode missing"
                };
            }

            return new PersonaModeSnapshot { Reachable = true, Ok = true, Mode = mode };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new PersonaModeSnapshot { Reachable = false, Ok = false, Detail = ex.Message };
        }
    }

    public async Task<PersonaModeSnapshot> SetModeAsync(
        string mode,
        CancellationToken cancellationToken = default)
    {
        var normalized = PersonaChatMode.Normalize(mode);
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new PersonaModeSnapshot
            {
                Reachable = false,
                Ok = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ModeUri)
            {
                Content = new StringContent(ModeRequestJson(normalized), Encoding.UTF8, "application/json")
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new PersonaModeSnapshot
                {
                    Reachable = true,
                    Ok = false,
                    Detail = $"HTTP {(int)response.StatusCode}"
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<ModeDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var confirmed = dto?.Mode?.Trim();
            if (confirmed is not (PersonaChatMode.One or PersonaChatMode.Many))
            {
                return new PersonaModeSnapshot
                {
                    Reachable = true,
                    Ok = false,
                    Detail = "mode missing"
                };
            }

            return new PersonaModeSnapshot { Reachable = true, Ok = true, Mode = confirmed };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new PersonaModeSnapshot { Reachable = false, Ok = false, Detail = ex.Message };
        }
    }

    public void Dispose() => _http.Dispose();

    private async Task<PersonaWriteSnapshot> WriteAsync(
        HttpMethod method,
        Uri uri,
        PersonaPackWrite pack,
        CancellationToken cancellationToken)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new PersonaWriteSnapshot
            {
                Reachable = false,
                Ok = false,
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            var json = BuildPersonaWriteJson(pack);
            using var request = new HttpRequestMessage(method, uri)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new PersonaWriteSnapshot
                {
                    Reachable = true,
                    Ok = false,
                    Detail = string.IsNullOrWhiteSpace(body)
                        ? $"HTTP {(int)response.StatusCode}"
                        : TrimDetail(body)
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<PersonaDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (dto is null)
                return new PersonaWriteSnapshot { Reachable = true, Ok = false, Detail = "Empty response" };

            return new PersonaWriteSnapshot
            {
                Reachable = true,
                Ok = true,
                Persona = FromDto(dto)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new PersonaWriteSnapshot { Reachable = false, Ok = false, Detail = ex.Message };
        }
    }

    private static string TrimDetail(string body)
    {
        var t = body.Trim();
        return t.Length <= 240 ? t : t[..237] + "…";
    }

    private static PersonaPackInfo FromDto(PersonaDto dto) => new()
    {
        PersonaId = dto.PersonaId?.Trim() ?? "",
        DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? (dto.PersonaId ?? "Persona") : dto.DisplayName.Trim(),
        HumanAddress = dto.HumanAddress,
        ContactId = dto.ContactId,
        IdentityBlurb = dto.IdentityBlurb,
        PhysicalDescription = dto.PhysicalDescription ?? "",
        CharterSeedPath = dto.CharterSeedPath,
        PlaywrightProfileDir = dto.PlaywrightProfileDir,
        VmWindowTitle = dto.VmWindowTitle,
        InferenceModel = PersonaWizardLogic.NormalizeInferenceModel(dto.InferenceModel),
        ResolvedInferenceModel = PersonaWizardLogic.NormalizeInferenceModel(dto.ResolvedInferenceModel),
        CompiledDirectives = dto.CompiledDirectives,
        IsActive = dto.IsActive,
        Traits = dto.Traits is null
            ? new PersonaTraitScalesInfo()
            : PersonaTraitScalesInfo.FromScales(dto.Traits),
        ToolPolicy = dto.ToolPolicy is null
            ? new PersonaToolPolicyInfo()
            : new PersonaToolPolicyInfo
            {
                AllowDesktop = dto.ToolPolicy.AllowDesktop,
                AllowBrowser = dto.ToolPolicy.AllowBrowser,
                AllowEmail = dto.ToolPolicy.AllowEmail
            }
    };

    private sealed class ModeDto
    {
        [JsonPropertyName("mode")]
        public string? Mode { get; set; }
    }

    private sealed class ListDto
    {
        [JsonPropertyName("activePersonaId")]
        public string? ActivePersonaId { get; set; }

        [JsonPropertyName("personas")]
        public PersonaDto[]? Personas { get; set; }
    }

    private sealed class ActivateDto
    {
        [JsonPropertyName("activePersonaId")]
        public string? ActivePersonaId { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("persona")]
        public PersonaDto? Persona { get; set; }
    }

    private sealed class PersonaDto
    {
        [JsonPropertyName("personaId")]
        public string? PersonaId { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("humanAddress")]
        public string? HumanAddress { get; set; }

        [JsonPropertyName("contactId")]
        public string? ContactId { get; set; }

        [JsonPropertyName("identityBlurb")]
        public string? IdentityBlurb { get; set; }

        [JsonPropertyName("physicalDescription")]
        public string? PhysicalDescription { get; set; }

        [JsonPropertyName("charterSeedPath")]
        public string? CharterSeedPath { get; set; }

        [JsonPropertyName("playwrightProfileDir")]
        public string? PlaywrightProfileDir { get; set; }

        [JsonPropertyName("vmWindowTitle")]
        public string? VmWindowTitle { get; set; }

        [JsonPropertyName("inferenceModel")]
        public string? InferenceModel { get; set; }

        [JsonPropertyName("resolvedInferenceModel")]
        public string? ResolvedInferenceModel { get; set; }

        [JsonPropertyName("compiledDirectives")]
        public string? CompiledDirectives { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("traits")]
        public PersonaTraitScales? Traits { get; set; }

        [JsonPropertyName("toolPolicy")]
        public ToolPolicyDto? ToolPolicy { get; set; }
    }

    private sealed class InferenceModelsDto
    {
        [JsonPropertyName("models")]
        public string[]? Models { get; set; }

        [JsonPropertyName("available")]
        public bool Available { get; set; }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }

        [JsonPropertyName("hostModel")]
        public string? HostModel { get; set; }

        [JsonPropertyName("resolvedInferenceModel")]
        public string? ResolvedInferenceModel { get; set; }
    }

    private sealed class DescriptionDraftDto
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    private sealed class ToolPolicyDto
    {
        [JsonPropertyName("allowDesktop")]
        public bool AllowDesktop { get; set; } = true;

        [JsonPropertyName("allowBrowser")]
        public bool AllowBrowser { get; set; } = true;

        [JsonPropertyName("allowEmail")]
        public bool AllowEmail { get; set; } = true;
    }
}

public sealed class PersonaListSnapshot
{
    public bool Reachable { get; init; }
    /// <summary>HTTP status when the Host answered (even on error).</summary>
    public int? HttpStatus { get; init; }
    /// <summary>True when Host returned 404 for GET /api/personas (pre-persona Host).</summary>
    public bool PersonaApiMissing { get; init; }
    public string? ActivePersonaId { get; init; }
    public IReadOnlyList<PersonaPackInfo> Personas { get; init; } = Array.Empty<PersonaPackInfo>();
    public string? Detail { get; init; }
}

public sealed class PersonaGetSnapshot
{
    public bool Reachable { get; init; }
    public bool Ok { get; init; }
    public PersonaPackInfo? Persona { get; init; }
    public string? Detail { get; init; }
}

public sealed class PersonaWriteSnapshot
{
    public bool Reachable { get; init; }
    public bool Ok { get; init; }
    public PersonaPackInfo? Persona { get; init; }
    public string? Detail { get; init; }
}

public sealed class PersonaActivateSnapshot
{
    public bool Reachable { get; init; }
    public bool Ok { get; init; }
    public string? ActivePersonaId { get; init; }
    public PersonaPackInfo? Persona { get; init; }
    public string? Note { get; init; }
    public string? Detail { get; init; }
}

public sealed class PersonaArchiveSnapshot
{
    public bool Reachable { get; init; }
    public bool Ok { get; init; }
    public string? Detail { get; init; }
}

public sealed class PersonaModeSnapshot
{
    public bool Reachable { get; init; }
    public bool Ok { get; init; }
    public string? Mode { get; init; }
    public string? Detail { get; init; }
}

public sealed class DescriptionDraftSnapshot
{
    public bool Reachable { get; init; }
    public bool Ok { get; init; }
    public string? Text { get; init; }
    public string? Detail { get; init; }
}

public sealed class InferenceModelsSnapshot
{
    public bool Reachable { get; init; }
    public bool Available { get; init; }
    public IReadOnlyList<string> Models { get; init; } = Array.Empty<string>();
    public string? HostModel { get; init; }
    public string? ResolvedInferenceModel { get; init; }
    public string? Detail { get; init; }
}

public sealed class PersonaPackInfo
{
    public string PersonaId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? HumanAddress { get; init; }
    public string? ContactId { get; init; }
    public string? IdentityBlurb { get; init; }
    public string PhysicalDescription { get; init; } = "";
    public string? CharterSeedPath { get; init; }
    public string? PlaywrightProfileDir { get; init; }
    public string? VmWindowTitle { get; init; }
    /// <summary>PROP-15.11/15.12 — pack override; blank/null = Host default.</summary>
    public string? InferenceModel { get; init; }
    /// <summary>Host-resolved model for this pack (override or Host Inference:Model).</summary>
    public string? ResolvedInferenceModel { get; init; }
    public string? CompiledDirectives { get; init; }
    public bool IsActive { get; init; }
    public PersonaTraitScalesInfo Traits { get; init; } = new();
    public PersonaToolPolicyInfo ToolPolicy { get; init; } = new();

    public override string ToString() => DisplayName;
}

public sealed class PersonaPackWrite
{
    public string PersonaId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string HumanAddress { get; set; } = "Friend";
    public string? ContactId { get; set; }
    public string? IdentityBlurb { get; set; }
    /// <summary>Null on an update means leave the stored description. Empty clears it.</summary>
    public string? PhysicalDescription { get; set; }
    public string? CharterSeedPath { get; set; }
    public string? PlaywrightProfileDir { get; set; }
    public string? VmWindowTitle { get; set; }
    /// <summary>PROP-15.12 — blank/null = inherit Host Inference:Model.</summary>
    public string? InferenceModel { get; set; }
    public PersonaTraitScalesInfo Traits { get; set; } = new();
    public PersonaToolPolicyInfo? ToolPolicy { get; set; }
}

/// <summary>
/// Desktop view of one <see cref="PersonaTraitScales"/> object.
/// The original six stay as properties. Every other trait is read through the catalog.
/// </summary>
public sealed class PersonaTraitScalesInfo
{
    private readonly PersonaTraitScales _scales;

    public PersonaTraitScalesInfo()
        : this(new PersonaTraitScales())
    {
    }

    private PersonaTraitScalesInfo(PersonaTraitScales scales) => _scales = scales;

    internal PersonaTraitScales Scales => _scales;

    public static PersonaTraitScalesInfo FromScales(PersonaTraitScales scales)
    {
        ArgumentNullException.ThrowIfNull(scales);
        return new PersonaTraitScalesInfo(scales.Clone());
    }

    public double Warmth
    {
        get => _scales.Warmth;
        set => _scales.Warmth = value;
    }

    public double Directness
    {
        get => _scales.Directness;
        set => _scales.Directness = value;
    }

    public double Formality
    {
        get => _scales.Formality;
        set => _scales.Formality = value;
    }

    public double Playfulness
    {
        get => _scales.Playfulness;
        set => _scales.Playfulness = value;
    }

    public double BoundaryStrictness
    {
        get => _scales.BoundaryStrictness;
        set => _scales.BoundaryStrictness = value;
    }

    public double RecallBias
    {
        get => _scales.RecallBias;
        set => _scales.RecallBias = value;
    }

    public double Read(PersonaTraitDefinition trait)
    {
        ArgumentNullException.ThrowIfNull(trait);
        return trait.Get(_scales);
    }

    public void Write(PersonaTraitDefinition trait, double value)
    {
        ArgumentNullException.ThrowIfNull(trait);
        trait.Set(_scales, value);
    }

    public PersonaTraitScalesInfo Clone() => new(_scales.Clone());
}

public sealed class PersonaToolPolicyInfo
{
    public bool AllowDesktop { get; set; } = true;
    public bool AllowBrowser { get; set; } = true;
    public bool AllowEmail { get; set; } = true;

    public PersonaToolPolicyInfo Clone() => new()
    {
        AllowDesktop = AllowDesktop,
        AllowBrowser = AllowBrowser,
        AllowEmail = AllowEmail
    };
}
