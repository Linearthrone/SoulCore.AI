using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace House.ChatDesktop.Services;

public sealed class ResearchSubjectRow
{
    public long Id { get; init; }
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public int PaperCount { get; init; }
    public string UpdatedAt { get; init; } = "";

    public string Line =>
        PaperCount == 1
            ? $"{Title} — 1 paper"
            : $"{Title} — {PaperCount} papers";
}

public sealed class ResearchPaperRow
{
    public long Id { get; init; }
    public long SubjectId { get; init; }
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public string Abstract { get; init; } = "";
    public string BodyApa { get; init; } = "";
    public string BibliographyApa { get; init; } = "";
    public string Status { get; init; } = "";
    public string UpdatedAt { get; init; } = "";

    public string Line => $"[{Kind}/{Status}] {Title}";
}

public sealed record ResearchSubjectsFetch(IReadOnlyList<ResearchSubjectRow> Subjects, string? Problem);
public sealed record ResearchPapersFetch(IReadOnlyList<ResearchPaperRow> Papers, string? Problem, string? SubjectTitle);
public sealed record ResearchPaperFetch(ResearchPaperRow? Paper, string? Problem);

/// <summary>Presence client for Host research subject journals.</summary>
public sealed class SoulCoreResearchClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };

    public async Task<ResearchSubjectsFetch> ListSubjectsAsync(CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return new ResearchSubjectsFetch(Array.Empty<ResearchSubjectRow>(), "Research journals stay on this machine.");

        try
        {
            using var response = await _http.GetAsync(Uri("/research/subjects"), cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ResearchSubjectsFetch(
                    Array.Empty<ResearchSubjectRow>(),
                    response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "Research journals need a Host restart."
                        : $"Research HTTP {(int)response.StatusCode}");
            }

            var dto = await response.Content.ReadFromJsonAsync<SubjectsDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var rows = (dto?.Subjects ?? new List<SubjectDto>())
                .Select(s => new ResearchSubjectRow
                {
                    Id = s.Id,
                    Title = s.Title ?? "",
                    Summary = s.Summary ?? "",
                    PaperCount = s.PaperCount,
                    UpdatedAt = s.UpdatedAt ?? ""
                })
                .ToList();
            return new ResearchSubjectsFetch(rows, null);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new ResearchSubjectsFetch(Array.Empty<ResearchSubjectRow>(), "Research journals unavailable.");
        }
    }

    public async Task<ResearchPapersFetch> ListPapersAsync(
        long subjectId,
        CancellationToken cancellationToken = default)
    {
        if (subjectId <= 0 || !ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return new ResearchPapersFetch(Array.Empty<ResearchPaperRow>(), "Invalid subject.", null);

        try
        {
            using var response = await _http.GetAsync(Uri($"/research/subjects/{subjectId}/papers"), cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ResearchPapersFetch(
                    Array.Empty<ResearchPaperRow>(),
                    response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "Subject not found (or Host needs restart)."
                        : $"Research HTTP {(int)response.StatusCode}",
                    null);
            }

            var dto = await response.Content.ReadFromJsonAsync<SubjectPapersDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var rows = (dto?.Papers ?? new List<PaperDto>()).Select(ToRow).ToList();
            return new ResearchPapersFetch(rows, null, dto?.Subject?.Title);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new ResearchPapersFetch(Array.Empty<ResearchPaperRow>(), "Research papers unavailable.", null);
        }
    }

    public async Task<ResearchPaperFetch> GetPaperAsync(long paperId, CancellationToken cancellationToken = default)
    {
        if (paperId <= 0 || !ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return new ResearchPaperFetch(null, "Invalid paper.");

        try
        {
            using var response = await _http.GetAsync(Uri($"/research/papers/{paperId}"), cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new ResearchPaperFetch(null, $"Research HTTP {(int)response.StatusCode}");

            var dto = await response.Content.ReadFromJsonAsync<PaperWrapDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return new ResearchPaperFetch(dto?.Paper is null ? null : ToRow(dto.Paper), null);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new ResearchPaperFetch(null, "Research paper unavailable.");
        }
    }

    public void Dispose() => _http.Dispose();

    private static Uri Uri(string path) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}{path}");

    private static ResearchPaperRow ToRow(PaperDto p) => new()
    {
        Id = p.Id,
        SubjectId = p.SubjectId,
        Kind = p.Kind ?? "",
        Title = p.Title ?? "",
        Abstract = p.AbstractText ?? "",
        BodyApa = p.BodyApa ?? "",
        BibliographyApa = p.BibliographyApa ?? "",
        Status = p.Status ?? "",
        UpdatedAt = p.UpdatedAt ?? ""
    };

    private sealed class SubjectsDto
    {
        [JsonPropertyName("subjects")]
        public List<SubjectDto>? Subjects { get; set; }
    }

    private sealed class SubjectDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("paperCount")]
        public int PaperCount { get; set; }

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }
    }

    private sealed class SubjectPapersDto
    {
        [JsonPropertyName("subject")]
        public SubjectDto? Subject { get; set; }

        [JsonPropertyName("papers")]
        public List<PaperDto>? Papers { get; set; }
    }

    private sealed class PaperWrapDto
    {
        [JsonPropertyName("paper")]
        public PaperDto? Paper { get; set; }
    }

    private sealed class PaperDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("subjectId")]
        public long SubjectId { get; set; }

        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("abstractText")]
        public string? AbstractText { get; set; }

        [JsonPropertyName("bodyApa")]
        public string? BodyApa { get; set; }

        [JsonPropertyName("bibliographyApa")]
        public string? BibliographyApa { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }
    }
}
