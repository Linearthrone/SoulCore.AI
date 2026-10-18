namespace SoulCore.Memory;

/// <summary>
/// Subject research library (hobbies / scientific threads). Distinct from personal
/// journal books (<c>feeling</c>/<c>animation</c>/<c>environment</c>/<c>life_hacks</c>).
/// Papers are APA-leaning drafts she aggregates during R&amp;R.
/// </summary>
public interface IVictoriaResearchStore
{
    Task<IReadOnlyList<VictoriaResearchSubject>> ListSubjectsAsync(
        CancellationToken cancellationToken = default);

    Task<VictoriaResearchSubject?> GetSubjectAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<VictoriaResearchSubject?> GetSubjectBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default);

    /// <summary>Create or return existing subject matched by slugified title.</summary>
    Task<VictoriaResearchSubject> OpenOrCreateSubjectAsync(
        string title,
        string? summary = null,
        CancellationToken cancellationToken = default);

    Task<bool> TouchSubjectAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VictoriaResearchPaper>> ListPapersAsync(
        long? subjectId = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<VictoriaResearchPaper?> GetPaperAsync(
        long id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert a paper, or replace fields on an existing draft when
    /// <paramref name="paperId"/> is set.
    /// </summary>
    Task<VictoriaResearchPaper> UpsertPaperAsync(
        long subjectId,
        string title,
        string kind,
        string? abstractText,
        string? bodyApa,
        string? bibliographyApa,
        string? status,
        long? paperId = null,
        CancellationToken cancellationToken = default);
}

public sealed record VictoriaResearchSubject(
    long Id,
    string Slug,
    string Title,
    string Summary,
    string CreatedAt,
    string UpdatedAt,
    int PaperCount = 0);

public sealed record VictoriaResearchPaper(
    long Id,
    long SubjectId,
    string Kind,
    string Title,
    string Abstract,
    string BodyApa,
    string BibliographyApa,
    string Status,
    string CreatedAt,
    string UpdatedAt);

public static class VictoriaResearchRules
{
    public static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "review", "experiment", "theory"
    };

    public static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "draft", "published"
    };

    public static string NormalizeKind(string? kind)
    {
        var k = string.IsNullOrWhiteSpace(kind) ? "review" : kind.Trim().ToLowerInvariant();
        if (!AllowedKinds.Contains(k))
            throw new ArgumentException($"kind must be review|experiment|theory (got '{kind}').");
        return k;
    }

    public static string NormalizeStatus(string? status)
    {
        var s = string.IsNullOrWhiteSpace(status) ? "draft" : status.Trim().ToLowerInvariant();
        if (!AllowedStatuses.Contains(s))
            throw new ArgumentException($"status must be draft|published (got '{status}').");
        return s;
    }

    public static string Slugify(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("title must be non-empty.");
        var chars = title.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        if (slug.Length == 0)
            slug = "subject";
        if (slug.Length > 80)
            slug = slug[..80].Trim('-');
        return slug;
    }
}
