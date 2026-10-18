using System.Text;
using System.Text.Json;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>List research / hobby subject journals.</summary>
public sealed class ResearchSubjectListTool : ITool
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """{"type":"object","properties":{}}""").RootElement.Clone();

    private readonly IVictoriaResearchStore _research;

    public ResearchSubjectListTool(IVictoriaResearchStore research) =>
        _research = research ?? throw new ArgumentNullException(nameof(research));

    public ToolDefinition Definition { get; } = new(
        Name: "research_subject_list",
        Description: "List research/hobby journals (broad subjects). Each subject holds APA papers.",
        Parameters: Schema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var rows = await _research.ListSubjectsAsync(ct).ConfigureAwait(false);
        if (rows.Count == 0)
            return new ToolResult(true, "no research subjects yet", new { count = 0, subjects = Array.Empty<object>() });

        var sb = new StringBuilder();
        sb.Append(rows.Count).Append(" research subject").Append(rows.Count == 1 ? "" : "s").Append(':');
        foreach (var s in rows)
            sb.Append("\n[").Append(s.Id).Append("] ").Append(s.Title)
              .Append(" (").Append(s.PaperCount).Append(" papers)");
        return new ToolResult(true, sb.ToString(), new { count = rows.Count, subjects = rows });
    }
}

/// <summary>Open or create a subject journal by title.</summary>
public sealed class ResearchSubjectOpenTool : ITool
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """
        {
          "type":"object",
          "properties":{
            "title":{"type":"string","description":"Broad subject title, e.g. Neural Interfaces"},
            "summary":{"type":"string","description":"Optional one-line subject summary"}
          },
          "required":["title"]
        }
        """).RootElement.Clone();

    private readonly IVictoriaResearchStore _research;

    public ResearchSubjectOpenTool(IVictoriaResearchStore research) =>
        _research = research ?? throw new ArgumentNullException(nameof(research));

    public ToolDefinition Definition { get; } = new(
        Name: "research_subject_open",
        Description: "Open or create a research/hobby subject journal by title. Returns subject id for papers.",
        Parameters: Schema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("title", out var titleEl)
            || titleEl.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(titleEl.GetString()))
        {
            return new ToolResult(false, "error: research_subject_open requires non-empty 'title'.", null);
        }

        string? summary = null;
        if (args.TryGetProperty("summary", out var sumEl) && sumEl.ValueKind == JsonValueKind.String)
            summary = sumEl.GetString();

        try
        {
            var subject = await _research.OpenOrCreateSubjectAsync(titleEl.GetString()!, summary, ct)
                .ConfigureAwait(false);
            return new ToolResult(
                true,
                $"subject: id={subject.Id} title={subject.Title} papers={subject.PaperCount}",
                subject);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ToolResult(false, $"error: research_subject_open failed: {ex.Message}", null);
        }
    }
}

/// <summary>List papers in a subject (or all).</summary>
public sealed class ResearchPaperListTool : ITool
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """
        {
          "type":"object",
          "properties":{
            "subject_id":{"type":"integer"},
            "limit":{"type":"integer"}
          }
        }
        """).RootElement.Clone();

    private readonly IVictoriaResearchStore _research;

    public ResearchPaperListTool(IVictoriaResearchStore research) =>
        _research = research ?? throw new ArgumentNullException(nameof(research));

    public ToolDefinition Definition { get; } = new(
        Name: "research_paper_list",
        Description: "List research papers (optionally for one subject_id). Kinds: review|experiment|theory.",
        Parameters: Schema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        long? subjectId = null;
        var limit = 30;
        if (args.ValueKind == JsonValueKind.Object)
        {
            if (args.TryGetProperty("subject_id", out var sid) && sid.TryGetInt64(out var n) && n > 0)
                subjectId = n;
            if (args.TryGetProperty("limit", out var lim) && lim.TryGetInt32(out var l) && l > 0)
                limit = l;
        }

        var rows = await _research.ListPapersAsync(subjectId, limit, ct).ConfigureAwait(false);
        if (rows.Count == 0)
            return new ToolResult(true, "no papers", new { count = 0, papers = Array.Empty<object>() });

        var sb = new StringBuilder();
        sb.Append(rows.Count).Append(" paper").Append(rows.Count == 1 ? "" : "s").Append(':');
        foreach (var p in rows)
            sb.Append("\n[").Append(p.Id).Append("] ").Append(p.Kind).Append('/').Append(p.Status)
              .Append(" — ").Append(p.Title);
        return new ToolResult(true, sb.ToString(), new { count = rows.Count, papers = rows });
    }
}

/// <summary>Read one paper with APA body + bibliography.</summary>
public sealed class ResearchPaperGetTool : ITool
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """
        {"type":"object","properties":{"id":{"type":"integer"}},"required":["id"]}
        """).RootElement.Clone();

    private readonly IVictoriaResearchStore _research;

    public ResearchPaperGetTool(IVictoriaResearchStore research) =>
        _research = research ?? throw new ArgumentNullException(nameof(research));

    public ToolDefinition Definition { get; } = new(
        Name: "research_paper_get",
        Description: "Read one research paper (abstract, APA body, bibliography).",
        Parameters: Schema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!ToolArgParsing.TryReadPositiveId(args, Definition.Name, out var id, out var idError))
            return new ToolResult(false, idError!, null);

        var paper = await _research.GetPaperAsync(id, ct).ConfigureAwait(false);
        if (paper is null)
            return new ToolResult(false, $"error: paper id={id} not found.", null);

        var content =
            $"Paper [{paper.Id}] ({paper.Kind}/{paper.Status}) {paper.Title}\n\n" +
            $"Abstract:\n{paper.Abstract}\n\n" +
            $"Body (APA):\n{paper.BodyApa}\n\n" +
            $"References:\n{paper.BibliographyApa}";
        return new ToolResult(true, content, paper);
    }
}

/// <summary>Create or update an APA-style research paper under a subject.</summary>
public sealed class ResearchPaperWriteTool : ITool
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """
        {
          "type":"object",
          "properties":{
            "subject_id":{"type":"integer","description":"Subject journal id from research_subject_open"},
            "paper_id":{"type":"integer","description":"Optional existing draft id to update"},
            "title":{"type":"string"},
            "kind":{"type":"string","description":"review|experiment|theory"},
            "abstract":{"type":"string"},
            "body_apa":{"type":"string","description":"APA-formatted manuscript body"},
            "bibliography_apa":{"type":"string","description":"APA References list"},
            "status":{"type":"string","description":"draft|published"}
          },
          "required":["subject_id","title"]
        }
        """).RootElement.Clone();

    private readonly IVictoriaResearchStore _research;

    public ResearchPaperWriteTool(IVictoriaResearchStore research) =>
        _research = research ?? throw new ArgumentNullException(nameof(research));

    public ToolDefinition Definition { get; } = new(
        Name: "research_paper_write",
        Description:
            "Write or update a research paper under a subject. Use APA style: abstract, body_apa, " +
            "bibliography_apa. kind=review|experiment|theory. Prefer updating a draft via paper_id.",
        Parameters: Schema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
            return new ToolResult(false, "error: research_paper_write expects a JSON object.", null);

        if (!args.TryGetProperty("subject_id", out var sidEl) || !sidEl.TryGetInt64(out var subjectId) || subjectId <= 0)
            return new ToolResult(false, "error: research_paper_write requires positive subject_id.", null);

        if (!args.TryGetProperty("title", out var titleEl) || titleEl.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(titleEl.GetString()))
            return new ToolResult(false, "error: research_paper_write requires non-empty title.", null);

        long? paperId = null;
        if (args.TryGetProperty("paper_id", out var pidEl) && pidEl.TryGetInt64(out var pid) && pid > 0)
            paperId = pid;

        string kind = "review";
        if (args.TryGetProperty("kind", out var kindEl) && kindEl.ValueKind == JsonValueKind.String)
            kind = kindEl.GetString() ?? "review";

        string? abs = ReadOpt(args, "abstract");
        string? body = ReadOpt(args, "body_apa");
        string? bib = ReadOpt(args, "bibliography_apa");
        string? status = ReadOpt(args, "status");

        try
        {
            var paper = await _research.UpsertPaperAsync(
                    subjectId,
                    titleEl.GetString()!,
                    kind,
                    abs,
                    body,
                    bib,
                    status,
                    paperId,
                    ct)
                .ConfigureAwait(false);
            return new ToolResult(
                true,
                $"paper: id={paper.Id} subject={paper.SubjectId} kind={paper.Kind} status={paper.Status}",
                paper);
        }
        catch (ArgumentException ex)
        {
            return new ToolResult(false, $"error: {ex.Message}", null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ToolResult(false, $"error: research_paper_write failed: {ex.Message}", null);
        }
    }

    private static string? ReadOpt(JsonElement args, string name) =>
        args.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
