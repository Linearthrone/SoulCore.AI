using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    public static Uri PersonaUri(string personaId) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/personas/{Uri.EscapeDataString(personaId)}");

    public static Uri ActivateUri(string personaId) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/api/personas/{Uri.EscapeDataString(personaId)}/activate");

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
            var payload = new
            {
                personaId = pack.PersonaId,
                displayName = pack.DisplayName,
                humanAddress = pack.HumanAddress,
                contactId = pack.ContactId,
                charterSeedPath = pack.CharterSeedPath,
                playwrightProfileDir = pack.PlaywrightProfileDir,
                vmWindowTitle = pack.VmWindowTitle,
                // PROP-15.12/15.16: blank/null = Host Inference:Model for this pack only.
                inferenceModel = PersonaWizardLogic.NormalizeInferenceModel(pack.InferenceModel),
                identityBlurb = pack.IdentityBlurb,
                toolPolicy = pack.ToolPolicy is null
                    ? null
                    : new
                    {
                        allowDesktop = pack.ToolPolicy.AllowDesktop,
                        allowBrowser = pack.ToolPolicy.AllowBrowser,
                        allowEmail = pack.ToolPolicy.AllowEmail
                    },
                traits = new
                {
                    warmth = pack.Traits.Warmth,
                    directness = pack.Traits.Directness,
                    formality = pack.Traits.Formality,
                    playfulness = pack.Traits.Playfulness,
                    boundaryStrictness = pack.Traits.BoundaryStrictness,
                    recallBias = pack.Traits.RecallBias
                }
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
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
        CharterSeedPath = dto.CharterSeedPath,
        PlaywrightProfileDir = dto.PlaywrightProfileDir,
        VmWindowTitle = dto.VmWindowTitle,
        InferenceModel = PersonaWizardLogic.NormalizeInferenceModel(dto.InferenceModel),
        ResolvedInferenceModel = PersonaWizardLogic.NormalizeInferenceModel(dto.ResolvedInferenceModel),
        CompiledDirectives = dto.CompiledDirectives,
        IsActive = dto.IsActive,
        Traits = dto.Traits is null
            ? new PersonaTraitScalesInfo()
            : new PersonaTraitScalesInfo
            {
                Warmth = dto.Traits.Warmth,
                Directness = dto.Traits.Directness,
                Formality = dto.Traits.Formality,
                Playfulness = dto.Traits.Playfulness,
                BoundaryStrictness = dto.Traits.BoundaryStrictness,
                RecallBias = dto.Traits.RecallBias
            },
        ToolPolicy = dto.ToolPolicy is null
            ? new PersonaToolPolicyInfo()
            : new PersonaToolPolicyInfo
            {
                AllowDesktop = dto.ToolPolicy.AllowDesktop,
                AllowBrowser = dto.ToolPolicy.AllowBrowser,
                AllowEmail = dto.ToolPolicy.AllowEmail
            }
    };

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
        public TraitsDto? Traits { get; set; }

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

    private sealed class TraitsDto
    {
        [JsonPropertyName("warmth")]
        public double Warmth { get; set; } = 0.5;

        [JsonPropertyName("directness")]
        public double Directness { get; set; } = 0.5;

        [JsonPropertyName("formality")]
        public double Formality { get; set; } = 0.5;

        [JsonPropertyName("playfulness")]
        public double Playfulness { get; set; } = 0.5;

        [JsonPropertyName("boundaryStrictness")]
        public double BoundaryStrictness { get; set; } = 0.5;

        [JsonPropertyName("recallBias")]
        public double RecallBias { get; set; } = 0.5;
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
    public string? CharterSeedPath { get; set; }
    public string? PlaywrightProfileDir { get; set; }
    public string? VmWindowTitle { get; set; }
    /// <summary>PROP-15.12 — blank/null = inherit Host Inference:Model.</summary>
    public string? InferenceModel { get; set; }
    public PersonaTraitScalesInfo Traits { get; set; } = new();
    public PersonaToolPolicyInfo? ToolPolicy { get; set; }
}

public sealed class PersonaTraitScalesInfo
{
    public double Warmth { get; set; } = 0.5;
    public double Directness { get; set; } = 0.5;
    public double Formality { get; set; } = 0.5;
    public double Playfulness { get; set; } = 0.5;
    public double BoundaryStrictness { get; set; } = 0.5;
    public double RecallBias { get; set; } = 0.5;

    public PersonaTraitScalesInfo Clone() => new()
    {
        Warmth = Warmth,
        Directness = Directness,
        Formality = Formality,
        Playfulness = Playfulness,
        BoundaryStrictness = BoundaryStrictness,
        RecallBias = RecallBias
    };
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
