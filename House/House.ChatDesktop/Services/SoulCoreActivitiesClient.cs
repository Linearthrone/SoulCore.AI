using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace House.ChatDesktop.Services;

/// <summary>One activities-log row (PROP-18.7 desk columns).</summary>
public sealed class ActivityLogRow
{
    public long EntryNo { get; init; }
    public string OccurredAt { get; init; } = "";
    public string Activity { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Desc { get; init; } = "";
    public string? JournalBookId { get; init; }
    public long? JournalEntryId { get; init; }

    public string EntryText => EntryNo > 0 ? EntryNo.ToString(CultureInfo.InvariantCulture) : "—";
    public string DateTimeText => string.IsNullOrWhiteSpace(OccurredAt) ? "—" : OccurredAt.Trim();
    public string ActivityText => string.IsNullOrWhiteSpace(Activity) ? "—" : Activity.Trim();
    public string SubjectText => string.IsNullOrWhiteSpace(Subject) ? "—" : Subject.Trim();
    public string DescText => string.IsNullOrWhiteSpace(Desc) ? "—" : Desc.Trim();
}

/// <summary>Journal crumb from while-you-were-gone residue (PROP-18.3).</summary>
public sealed class JournalCrumbRow
{
    public long Id { get; init; }
    public string BookId { get; init; } = "";
    public string BodyPreview { get; init; } = "";
    public string TagsJson { get; init; } = "[]";
    public string OccurredAt { get; init; } = "";
}

public sealed record ActivitiesListFetch(
    IReadOnlyList<ActivityLogRow> Activities,
    string? Problem);

public sealed record WhileYouWereGoneFetch(
    string Since,
    bool Empty,
    IReadOnlyList<ActivityLogRow> Activities,
    IReadOnlyList<JournalCrumbRow> JournalCrumbs,
    string? Problem);

/// <summary>
/// PROP-18.3 client for Host residue APIs:
/// <c>GET /activities</c>, <c>GET /presence/while-you-were-gone</c>.
/// Topic-filtered journal crumbs stay on Host; desk surfaces tags via Her journal (PROP-18.31).
/// </summary>
public sealed class SoulCoreActivitiesClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };

    public async Task<ActivitiesListFetch> ListAsync(
        string? subject = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return new ActivitiesListFetch(Array.Empty<ActivityLogRow>(), "Activities stay on this machine.");

        try
        {
            var take = Math.Clamp(limit, 1, 200);
            var path = $"/activities?limit={take}";
            if (!string.IsNullOrWhiteSpace(subject))
                path += $"&subject={System.Uri.EscapeDataString(subject.Trim())}";

            using var response = await _http.GetAsync(ApiUri(path), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ActivitiesListFetch(
                    Array.Empty<ActivityLogRow>(),
                    response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "Activities log needs a Host restart (PROP-18.7)."
                        : $"Activities HTTP {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseActivitiesList(json);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return new ActivitiesListFetch(Array.Empty<ActivityLogRow>(), "Activities unavailable.");
        }
    }

    public async Task<WhileYouWereGoneFetch> WhileYouWereGoneAsync(
        string? since = null,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new WhileYouWereGoneFetch(
                since ?? "",
                true,
                Array.Empty<ActivityLogRow>(),
                Array.Empty<JournalCrumbRow>(),
                "While-you-were-gone stays on this machine.");
        }

        try
        {
            var take = Math.Clamp(limit, 1, 100);
            var path = $"/presence/while-you-were-gone?limit={take}";
            if (!string.IsNullOrWhiteSpace(since))
                path += $"&since={System.Uri.EscapeDataString(since.Trim())}";

            using var response = await _http.GetAsync(ApiUri(path), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new WhileYouWereGoneFetch(
                    since ?? "",
                    true,
                    Array.Empty<ActivityLogRow>(),
                    Array.Empty<JournalCrumbRow>(),
                    response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "While-you-were-gone needs a Host restart (PROP-18.7)."
                        : $"While-you-were-gone HTTP {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseWhileYouWereGone(json);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return new WhileYouWereGoneFetch(
                since ?? "",
                true,
                Array.Empty<ActivityLogRow>(),
                Array.Empty<JournalCrumbRow>(),
                "While-you-were-gone unavailable.");
        }
    }

    public void Dispose() => _http.Dispose();

    internal static ActivitiesListFetch ParseActivitiesList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ActivitiesListFetch(Array.Empty<ActivityLogRow>(), "Activities response was empty.");

        try
        {
            var dto = JsonSerializer.Deserialize<ActivitiesListDto>(json, JsonOpts);
            if (dto?.Activities is null)
                return new ActivitiesListFetch(Array.Empty<ActivityLogRow>(), "Activities response missing activities[].");

            return new ActivitiesListFetch(dto.Activities.Select(ToActivityRow).ToList(), null);
        }
        catch (JsonException)
        {
            return new ActivitiesListFetch(Array.Empty<ActivityLogRow>(), "Activities response was not valid JSON.");
        }
    }

    internal static WhileYouWereGoneFetch ParseWhileYouWereGone(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new WhileYouWereGoneFetch(
                "",
                true,
                Array.Empty<ActivityLogRow>(),
                Array.Empty<JournalCrumbRow>(),
                "While-you-were-gone response was empty.");
        }

        try
        {
            var dto = JsonSerializer.Deserialize<WhileYouWereGoneDto>(json, JsonOpts);
            if (dto is null)
            {
                return new WhileYouWereGoneFetch(
                    "",
                    true,
                    Array.Empty<ActivityLogRow>(),
                    Array.Empty<JournalCrumbRow>(),
                    "While-you-were-gone response was empty.");
            }

            var activities = (dto.Activities ?? new List<ActivityDto>()).Select(ToActivityRow).ToList();
            var crumbs = (dto.JournalCrumbs ?? new List<JournalCrumbDto>()).Select(ToCrumb).ToList();
            var empty = dto.Empty || (activities.Count == 0 && crumbs.Count == 0);
            return new WhileYouWereGoneFetch(dto.Since ?? "", empty, activities, crumbs, null);
        }
        catch (JsonException)
        {
            return new WhileYouWereGoneFetch(
                "",
                true,
                Array.Empty<ActivityLogRow>(),
                Array.Empty<JournalCrumbRow>(),
                "While-you-were-gone response was not valid JSON.");
        }
    }

    private static System.Uri ApiUri(string path) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}{path}");

    private static ActivityLogRow ToActivityRow(ActivityDto a) => new()
    {
        EntryNo = a.EntryNo,
        OccurredAt = a.OccurredAt ?? "",
        Activity = a.Activity ?? "",
        Subject = a.Subject ?? "",
        Desc = a.Desc ?? "",
        JournalBookId = a.JournalBookId,
        JournalEntryId = a.JournalEntryId
    };

    private static JournalCrumbRow ToCrumb(JournalCrumbDto c) => new()
    {
        Id = c.Id,
        BookId = c.BookId ?? "",
        BodyPreview = FirstNonEmpty(c.BodyPreview, c.Body) ?? "",
        TagsJson = c.TagsJson ?? "[]",
        OccurredAt = c.OccurredAt ?? ""
    };

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        return null;
    }

    private sealed class ActivitiesListDto
    {
        [JsonPropertyName("activities")]
        public List<ActivityDto>? Activities { get; set; }
    }

    private sealed class WhileYouWereGoneDto
    {
        [JsonPropertyName("since")]
        public string? Since { get; set; }

        [JsonPropertyName("empty")]
        public bool Empty { get; set; }

        [JsonPropertyName("activities")]
        public List<ActivityDto>? Activities { get; set; }

        [JsonPropertyName("journalCrumbs")]
        public List<JournalCrumbDto>? JournalCrumbs { get; set; }
    }

    private sealed class ActivityDto
    {
        [JsonPropertyName("entryNo")]
        public long EntryNo { get; set; }

        [JsonPropertyName("occurredAt")]
        public string? OccurredAt { get; set; }

        [JsonPropertyName("activity")]
        public string? Activity { get; set; }

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("desc")]
        public string? Desc { get; set; }

        [JsonPropertyName("journalBookId")]
        public string? JournalBookId { get; set; }

        [JsonPropertyName("journalEntryId")]
        public long? JournalEntryId { get; set; }
    }

    private sealed class JournalCrumbDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("bookId")]
        public string? BookId { get; set; }

        [JsonPropertyName("bodyPreview")]
        public string? BodyPreview { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("tagsJson")]
        public string? TagsJson { get; set; }

        [JsonPropertyName("occurredAt")]
        public string? OccurredAt { get; set; }
    }
}

/// <summary>Formats Host-proven honesty + while-you-were-gone copy (no fabrication).</summary>
public static class PresenceResidueFormatter
{
    public static string FormatHonestyLine(
        bool alive,
        DateTimeOffset? lastTickUtc,
        string? lastWantPhrase,
        string? lastJournalCrumb)
    {
        var tick = lastTickUtc is null
            ? "none yet"
            : lastTickUtc.Value.ToString("HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var want = string.IsNullOrWhiteSpace(lastWantPhrase) ? "none yet" : TrimOneLine(lastWantPhrase, 72);
        var journal = string.IsNullOrWhiteSpace(lastJournalCrumb) ? "none yet" : TrimOneLine(lastJournalCrumb, 72);
        return $"Alive — {(alive ? "yes" : "no")} · Last tick — {tick} · Last want — {want} · Last journal — {journal}";
    }

    public static string FormatWhileYouWereGoneBody(WhileYouWereGoneFetch fetch)
    {
        if (!string.IsNullOrWhiteSpace(fetch.Problem))
            return fetch.Problem!;

        if (fetch.Empty)
            return "Nothing stored while you were gone — empty is honest.";

        var sb = new StringBuilder();
        foreach (var a in fetch.Activities.Take(8))
        {
            sb.AppendLine(
                $"#{a.EntryNo} · {a.DateTimeText} · {a.ActivityText} · {a.SubjectText} — {TrimOneLine(a.DescText, 120)}");
        }

        foreach (var j in fetch.JournalCrumbs.Take(6))
        {
            var when = string.IsNullOrWhiteSpace(j.OccurredAt) ? "—" : j.OccurredAt.Trim();
            sb.AppendLine($"journal · {when} — {TrimOneLine(j.BodyPreview, 120)}");
        }

        return sb.ToString().TrimEnd();
    }

    public static string? NewestJournalPreview(WhileYouWereGoneFetch fetch)
    {
        if (fetch.JournalCrumbs.Count == 0)
            return null;
        return fetch.JournalCrumbs
            .OrderByDescending(c => c.OccurredAt, StringComparer.Ordinal)
            .Select(c => c.BodyPreview)
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
    }

    private static string TrimOneLine(string text, int max)
    {
        var t = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (t.Length <= max)
            return t;
        return t[..(max - 1)] + "…";
    }
}
