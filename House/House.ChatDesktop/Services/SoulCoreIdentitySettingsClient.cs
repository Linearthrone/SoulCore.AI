using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace House.ChatDesktop.Services;

public sealed class IdentityAnchorSnapshot
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public int Priority { get; init; }
    public bool IsLocked { get; init; }
    public string? Source { get; init; }
}

/// <summary>PROP-20.30/20.31 — resting or context trait row from GET /settings/identity.</summary>
public sealed class IdentityTraitSnapshot
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Group { get; init; } = "";
    public string? Kind { get; init; }
    public double Weight { get; init; }
    public string Band { get; init; } = "";
    public string? PromptLine { get; init; }
    public IReadOnlyList<string> AppliesWhen { get; init; } = Array.Empty<string>();
    public string LedgerReason { get; init; } = "";
}

/// <summary>PROP-20.37/20.38 — bond counters from Host facts (read-only).</summary>
public sealed class IdentityBondCountersSnapshot
{
    public double ContactTimeMinutes { get; init; }
    public int DaysWithContact { get; init; }
    public int EntrustedTasks { get; init; }
    public int OperatorCorrections { get; init; }
    public int HerInitiatedReaches { get; init; }
    public int JournalOpens { get; init; }
}

/// <summary>PROP-20.37/20.38 — soak clock (display-only vs open).</summary>
public sealed class IdentitySoakSnapshot
{
    public string Phase { get; init; } = "";
    public string? StartedAt { get; init; }
    public int DaysElapsed { get; init; }
    public int DaysRemaining { get; init; }
    public int ContactDaysInWindow { get; init; }
    public bool ContactGateMet { get; init; }
    public int MinContactDays { get; init; }
    public int DisplayOnlyDays { get; init; }
    public bool IsFirstWeekAfterOpen { get; init; }
    public bool MayChangePrompt { get; init; }
}

/// <summary>PROP-20.37/20.38 — pending band-cross proposal awaiting operator confirm/dismiss.</summary>
public sealed class IdentityPendingBandCrossSnapshot
{
    public long Id { get; init; }
    public string TraitId { get; init; } = "";
    public double FromWeight { get; init; }
    public double ToWeight { get; init; }
    public string FromBand { get; init; } = "";
    public string ToBand { get; init; } = "";
    public string Reason { get; init; } = "";
    public string Status { get; init; } = "";
    public string? CreatedAt { get; init; }

    public string Line
    {
        get
        {
            var trait = string.IsNullOrWhiteSpace(TraitId) ? "(trait?)" : TraitId.Trim();
            var fromW = FromWeight.ToString("0.00", CultureInfo.InvariantCulture);
            var toW = ToWeight.ToString("0.00", CultureInfo.InvariantCulture);
            var fromB = string.IsNullOrWhiteSpace(FromBand) ? "?" : FromBand.Trim();
            var toB = string.IsNullOrWhiteSpace(ToBand) ? "?" : ToBand.Trim();
            var reason = string.IsNullOrWhiteSpace(Reason) ? "(no reason)" : Reason.Trim();
            return $"#{Id}  {trait}  {fromW} {fromB} → {toW} {toB}  · {reason}";
        }
    }
}

/// <summary>PROP-20.37/20.38 — recent growth ledger row (display-only).</summary>
public sealed class IdentityGrowthLedgerEntrySnapshot
{
    public long Id { get; init; }
    public string TraitId { get; init; } = "";
    public double FromWeight { get; init; }
    public double ToWeight { get; init; }
    public string FromBand { get; init; } = "";
    public string ToBand { get; init; } = "";
    public string Reason { get; init; } = "";
    public string Source { get; init; } = "";
    public string? RecordedAt { get; init; }
}

public sealed record IdentityPendingActionResult(bool Ok, string? Detail);

public sealed class IdentitySettingsSnapshot
{
    public string? DisplayName { get; init; }
    public string? ContactId { get; init; }
    public int CharterAnchors { get; init; }
    public int CharterLocked { get; init; }
    public bool CharterFullyLocked { get; init; }
    public string? CharterMode { get; init; }
    public IReadOnlyList<IdentityAnchorSnapshot> IdentityAnchors { get; init; } =
        Array.Empty<IdentityAnchorSnapshot>();
    public IReadOnlyList<IdentityAnchorSnapshot> Anchors { get; init; } =
        Array.Empty<IdentityAnchorSnapshot>();
    /// <summary>PROP-20.30 — nine locked resting rows (weight / band / ledgerReason).</summary>
    public IReadOnlyList<IdentityTraitSnapshot> RestingTraits { get; init; } =
        Array.Empty<IdentityTraitSnapshot>();
    /// <summary>PROP-20.30 — turn-scoped; bare settings GET is usually empty.</summary>
    public IReadOnlyList<IdentityTraitSnapshot> ContextTraits { get; init; } =
        Array.Empty<IdentityTraitSnapshot>();
    /// <summary>PROP-20.30 — v1 seed catalog for available context kinds.</summary>
    public IReadOnlyList<IdentityTraitSnapshot> ContextTraitCatalog { get; init; } =
        Array.Empty<IdentityTraitSnapshot>();
    /// <summary>PROP-20.37 — null when Host did not return bondCounters (unreachable / HTTP error).</summary>
    public IdentityBondCountersSnapshot? BondCounters { get; init; }
    /// <summary>PROP-20.37 — null when Host did not return soak.</summary>
    public IdentitySoakSnapshot? Soak { get; init; }
    /// <summary>PROP-20.37 — growth traits (responsiveness / initiative / openness).</summary>
    public IReadOnlyList<IdentityTraitSnapshot> GrowthTraits { get; init; } =
        Array.Empty<IdentityTraitSnapshot>();
    /// <summary>PROP-20.37 — band-cross proposals awaiting confirm/dismiss.</summary>
    public IReadOnlyList<IdentityPendingBandCrossSnapshot> PendingBandCross { get; init; } =
        Array.Empty<IdentityPendingBandCrossSnapshot>();
    /// <summary>PROP-20.37 — recent auto-apply / confirm ledger (display).</summary>
    public IReadOnlyList<IdentityGrowthLedgerEntrySnapshot> GrowthLedger { get; init; } =
        Array.Empty<IdentityGrowthLedgerEntrySnapshot>();
    public string? Note { get; init; }
    public bool Reachable { get; init; }
    public string? Detail { get; init; }
}

/// <summary>GET /settings/identity — live charter anchors for the Identity tab (PROP-20.1).</summary>
public sealed class SoulCoreIdentitySettingsClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public SoulCoreIdentitySettingsClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public static Uri SettingsUri =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/settings/identity");

    public static Uri PendingConfirmUri(long id) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/settings/identity/growth/pending/{id}/confirm");

    public static Uri PendingDismissUri(long id) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/settings/identity/growth/pending/{id}/dismiss");

    public async Task<IdentitySettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new IdentitySettingsSnapshot
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
                return new IdentitySettingsSnapshot
                {
                    Reachable = true,
                    Detail = $"HTTP {(int)response.StatusCode}"
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseResponseJson(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException
                                       or JsonException)
        {
            return new IdentitySettingsSnapshot { Reachable = false, Detail = ex.Message };
        }
    }

    /// <summary>PROP-20.38 — POST confirm pending band-cross.</summary>
    public Task<IdentityPendingActionResult> ConfirmPendingAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        PostPendingActionAsync(PendingConfirmUri(id), id, cancellationToken);

    /// <summary>PROP-20.38 — POST dismiss pending band-cross.</summary>
    public Task<IdentityPendingActionResult> DismissPendingAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        PostPendingActionAsync(PendingDismissUri(id), id, cancellationToken);

    private async Task<IdentityPendingActionResult> PostPendingActionAsync(
        Uri uri,
        long id,
        CancellationToken cancellationToken)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new IdentityPendingActionResult(
                false,
                $"Non-loopback host blocked: {ConnectionDefaults.Host}");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            AttachAuth(request);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return new IdentityPendingActionResult(true, null);

            var detail = string.IsNullOrWhiteSpace(body)
                ? $"HTTP {(int)response.StatusCode} for pending #{id}"
                : $"HTTP {(int)response.StatusCode}: {body.Trim()}";
            return new IdentityPendingActionResult(false, detail);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new IdentityPendingActionResult(false, ex.Message);
        }
    }

    /// <summary>Deserialize Host identity JSON (numeric or string <c>id</c>) into a reachable snapshot.</summary>
    internal static IdentitySettingsSnapshot ParseResponseJson(string json)
    {
        var dto = JsonSerializer.Deserialize<IdentityDto>(json, JsonOptions);
        return FromDto(dto, reachable: true);
    }

    private static void AttachAuth(HttpRequestMessage request)
    {
        var token = CompanionToken.Resolve();
        if (string.IsNullOrEmpty(token))
            return;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("X-Api-Key", token);
    }

    private static IdentitySettingsSnapshot FromDto(IdentityDto? dto, bool reachable)
    {
        var identity = MapAnchors(dto?.IdentityAnchors);
        var all = MapAnchors(dto?.Anchors);
        return new IdentitySettingsSnapshot
        {
            Reachable = reachable,
            DisplayName = dto?.DisplayName,
            ContactId = dto?.ContactId,
            CharterAnchors = dto?.Charter?.Anchors ?? 0,
            CharterLocked = dto?.Charter?.Locked ?? 0,
            CharterFullyLocked = dto?.Charter?.FullyLocked ?? false,
            CharterMode = dto?.Charter?.Mode,
            IdentityAnchors = identity,
            Anchors = all,
            RestingTraits = MapTraits(dto?.RestingTraits),
            ContextTraits = MapTraits(dto?.ContextTraits),
            ContextTraitCatalog = MapTraits(dto?.ContextTraitCatalog),
            BondCounters = MapBondCounters(dto?.BondCounters),
            Soak = MapSoak(dto?.Soak),
            GrowthTraits = MapTraits(dto?.GrowthTraits),
            PendingBandCross = MapPending(dto?.PendingBandCross),
            GrowthLedger = MapLedger(dto?.GrowthLedger),
            Note = dto?.Note
        };
    }

    private static IReadOnlyList<IdentityAnchorSnapshot> MapAnchors(IReadOnlyList<AnchorDto>? rows)
    {
        if (rows is null || rows.Count == 0)
            return Array.Empty<IdentityAnchorSnapshot>();

        var list = new List<IdentityAnchorSnapshot>(rows.Count);
        foreach (var row in rows)
        {
            list.Add(new IdentityAnchorSnapshot
            {
                Id = FlexibleIdToString(row.Id),
                Kind = row.Kind ?? "",
                Title = row.Title ?? "",
                Body = row.Body ?? "",
                Priority = row.Priority,
                IsLocked = row.IsLocked,
                Source = row.Source
            });
        }

        return list;
    }

    private static IReadOnlyList<IdentityTraitSnapshot> MapTraits(IReadOnlyList<TraitDto>? rows)
    {
        if (rows is null || rows.Count == 0)
            return Array.Empty<IdentityTraitSnapshot>();

        var list = new List<IdentityTraitSnapshot>(rows.Count);
        foreach (var row in rows)
        {
            list.Add(new IdentityTraitSnapshot
            {
                Id = FlexibleIdToString(row.Id),
                Name = row.Name ?? "",
                Group = row.Group ?? "",
                Kind = row.Kind,
                Weight = row.Weight,
                Band = row.Band ?? "",
                PromptLine = row.PromptLine,
                AppliesWhen = row.AppliesWhen is { Count: > 0 }
                    ? row.AppliesWhen.ToArray()
                    : Array.Empty<string>(),
                LedgerReason = row.LedgerReason ?? ""
            });
        }

        return list;
    }

    private static IdentityBondCountersSnapshot? MapBondCounters(BondCountersDto? dto) =>
        dto is null
            ? null
            : new IdentityBondCountersSnapshot
            {
                ContactTimeMinutes = dto.ContactTimeMinutes,
                DaysWithContact = dto.DaysWithContact,
                EntrustedTasks = dto.EntrustedTasks,
                OperatorCorrections = dto.OperatorCorrections,
                HerInitiatedReaches = dto.HerInitiatedReaches,
                JournalOpens = dto.JournalOpens
            };

    private static IdentitySoakSnapshot? MapSoak(SoakDto? dto) =>
        dto is null
            ? null
            : new IdentitySoakSnapshot
            {
                Phase = dto.Phase ?? "",
                StartedAt = dto.StartedAt,
                DaysElapsed = dto.DaysElapsed,
                DaysRemaining = dto.DaysRemaining,
                ContactDaysInWindow = dto.ContactDaysInWindow,
                ContactGateMet = dto.ContactGateMet,
                MinContactDays = dto.MinContactDays,
                DisplayOnlyDays = dto.DisplayOnlyDays,
                IsFirstWeekAfterOpen = dto.IsFirstWeekAfterOpen,
                MayChangePrompt = dto.MayChangePrompt
            };

    private static IReadOnlyList<IdentityPendingBandCrossSnapshot> MapPending(
        IReadOnlyList<PendingBandCrossDto>? rows)
    {
        if (rows is null || rows.Count == 0)
            return Array.Empty<IdentityPendingBandCrossSnapshot>();

        var list = new List<IdentityPendingBandCrossSnapshot>(rows.Count);
        foreach (var row in rows)
        {
            list.Add(new IdentityPendingBandCrossSnapshot
            {
                Id = FlexibleIdToLong(row.Id),
                TraitId = row.TraitId ?? "",
                FromWeight = row.FromWeight,
                ToWeight = row.ToWeight,
                FromBand = row.FromBand ?? "",
                ToBand = row.ToBand ?? "",
                Reason = row.Reason ?? "",
                Status = row.Status ?? "",
                CreatedAt = row.CreatedAt
            });
        }

        return list;
    }

    private static IReadOnlyList<IdentityGrowthLedgerEntrySnapshot> MapLedger(
        IReadOnlyList<GrowthLedgerDto>? rows)
    {
        if (rows is null || rows.Count == 0)
            return Array.Empty<IdentityGrowthLedgerEntrySnapshot>();

        var list = new List<IdentityGrowthLedgerEntrySnapshot>(rows.Count);
        foreach (var row in rows)
        {
            list.Add(new IdentityGrowthLedgerEntrySnapshot
            {
                Id = FlexibleIdToLong(row.Id),
                TraitId = row.TraitId ?? "",
                FromWeight = row.FromWeight,
                ToWeight = row.ToWeight,
                FromBand = row.FromBand ?? "",
                ToBand = row.ToBand ?? "",
                Reason = row.Reason ?? "",
                Source = row.Source ?? "",
                RecordedAt = row.RecordedAt
            });
        }

        return list;
    }

    /// <summary>Host may emit <c>id</c> as JSON number or string; snapshot always stores string.</summary>
    private static string FlexibleIdToString(JsonElement id) =>
        id.ValueKind switch
        {
            JsonValueKind.String => id.GetString() ?? "",
            JsonValueKind.Number => id.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => ""
        };

    private static long FlexibleIdToLong(JsonElement id) =>
        id.ValueKind switch
        {
            JsonValueKind.Number when id.TryGetInt64(out var n) => n,
            JsonValueKind.String when long.TryParse(
                id.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => 0
        };

    public void Dispose() => _http.Dispose();

    private sealed class IdentityDto
    {
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("contactId")]
        public string? ContactId { get; set; }

        [JsonPropertyName("charter")]
        public CharterDto? Charter { get; set; }

        [JsonPropertyName("identityAnchors")]
        public List<AnchorDto>? IdentityAnchors { get; set; }

        [JsonPropertyName("anchors")]
        public List<AnchorDto>? Anchors { get; set; }

        [JsonPropertyName("restingTraits")]
        public List<TraitDto>? RestingTraits { get; set; }

        [JsonPropertyName("contextTraits")]
        public List<TraitDto>? ContextTraits { get; set; }

        [JsonPropertyName("contextTraitCatalog")]
        public List<TraitDto>? ContextTraitCatalog { get; set; }

        [JsonPropertyName("bondCounters")]
        public BondCountersDto? BondCounters { get; set; }

        [JsonPropertyName("soak")]
        public SoakDto? Soak { get; set; }

        [JsonPropertyName("growthTraits")]
        public List<TraitDto>? GrowthTraits { get; set; }

        [JsonPropertyName("pendingBandCross")]
        public List<PendingBandCrossDto>? PendingBandCross { get; set; }

        [JsonPropertyName("growthLedger")]
        public List<GrowthLedgerDto>? GrowthLedger { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    private sealed class BondCountersDto
    {
        [JsonPropertyName("contactTimeMinutes")]
        public double ContactTimeMinutes { get; set; }

        [JsonPropertyName("daysWithContact")]
        public int DaysWithContact { get; set; }

        [JsonPropertyName("entrustedTasks")]
        public int EntrustedTasks { get; set; }

        [JsonPropertyName("operatorCorrections")]
        public int OperatorCorrections { get; set; }

        [JsonPropertyName("herInitiatedReaches")]
        public int HerInitiatedReaches { get; set; }

        [JsonPropertyName("journalOpens")]
        public int JournalOpens { get; set; }
    }

    private sealed class SoakDto
    {
        [JsonPropertyName("phase")]
        public string? Phase { get; set; }

        [JsonPropertyName("startedAt")]
        public string? StartedAt { get; set; }

        [JsonPropertyName("daysElapsed")]
        public int DaysElapsed { get; set; }

        [JsonPropertyName("daysRemaining")]
        public int DaysRemaining { get; set; }

        [JsonPropertyName("contactDaysInWindow")]
        public int ContactDaysInWindow { get; set; }

        [JsonPropertyName("contactGateMet")]
        public bool ContactGateMet { get; set; }

        [JsonPropertyName("minContactDays")]
        public int MinContactDays { get; set; }

        [JsonPropertyName("displayOnlyDays")]
        public int DisplayOnlyDays { get; set; }

        [JsonPropertyName("isFirstWeekAfterOpen")]
        public bool IsFirstWeekAfterOpen { get; set; }

        [JsonPropertyName("mayChangePrompt")]
        public bool MayChangePrompt { get; set; }
    }

    private sealed class PendingBandCrossDto
    {
        [JsonPropertyName("id")]
        public JsonElement Id { get; set; }

        [JsonPropertyName("traitId")]
        public string? TraitId { get; set; }

        [JsonPropertyName("fromWeight")]
        public double FromWeight { get; set; }

        [JsonPropertyName("toWeight")]
        public double ToWeight { get; set; }

        [JsonPropertyName("fromBand")]
        public string? FromBand { get; set; }

        [JsonPropertyName("toBand")]
        public string? ToBand { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("createdAt")]
        public string? CreatedAt { get; set; }
    }

    private sealed class GrowthLedgerDto
    {
        [JsonPropertyName("id")]
        public JsonElement Id { get; set; }

        [JsonPropertyName("traitId")]
        public string? TraitId { get; set; }

        [JsonPropertyName("fromWeight")]
        public double FromWeight { get; set; }

        [JsonPropertyName("toWeight")]
        public double ToWeight { get; set; }

        [JsonPropertyName("fromBand")]
        public string? FromBand { get; set; }

        [JsonPropertyName("toBand")]
        public string? ToBand { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("recordedAt")]
        public string? RecordedAt { get; set; }
    }

    private sealed class TraitDto
    {
        [JsonPropertyName("id")]
        public JsonElement Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("group")]
        public string? Group { get; set; }

        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("weight")]
        public double Weight { get; set; }

        [JsonPropertyName("band")]
        public string? Band { get; set; }

        [JsonPropertyName("promptLine")]
        public string? PromptLine { get; set; }

        [JsonPropertyName("appliesWhen")]
        public List<string>? AppliesWhen { get; set; }

        [JsonPropertyName("ledgerReason")]
        public string? LedgerReason { get; set; }
    }

    private sealed class CharterDto
    {
        [JsonPropertyName("anchors")]
        public int Anchors { get; set; }

        [JsonPropertyName("locked")]
        public int Locked { get; set; }

        [JsonPropertyName("fullyLocked")]
        public bool FullyLocked { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode { get; set; }
    }

    private sealed class AnchorDto
    {
        /// <summary>Accepts JSON number or string (ISSUE-20261117-001).</summary>
        [JsonPropertyName("id")]
        public JsonElement Id { get; set; }

        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("isLocked")]
        public bool IsLocked { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }
}
