using System.Text;
using System.Text.Json;
using SoulCore.Inference.Tooling;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools;

/// <summary>
/// PROP-18.2 optional tool: dated journal + activity-log (+ recent episodic) timeline
/// on demand. Cheap store reads — no embeddings. Complements preamble [Daybook].
/// PROP-18.20: provenance / parse / truncate via <see cref="ChronologyText"/>.
/// </summary>
public sealed class RecallTimelineTool : ITool
{
    public const int DefaultLimit = 8;
    public const int MaxLimit = 20;
    public const int BodyCharLimit = 400;

    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IVictoriaJournalStore? _journals;
    private readonly IVictoriaActivitiesLogStore? _activitiesLog;
    private readonly IMemoryStore? _memory;

    public RecallTimelineTool(
        IVictoriaJournalStore? journals = null,
        IVictoriaActivitiesLogStore? activitiesLog = null,
        IMemoryStore? memory = null)
    {
        _journals = journals;
        _activitiesLog = activitiesLog;
        _memory = memory;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "recall_timeline",
        Description:
            "Recall a dated timeline of recent journal notes, activity-log crumbs, " +
            "and episodic memories. Use when you need chronology beyond the [Daybook]/[Memory] preamble.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: recall_timeline expects a JSON object.",
                Data: null);
        }

        var limit = DefaultLimit;
        if (args.TryGetProperty("limit", out var limitProp)
            && limitProp.ValueKind == JsonValueKind.Number
            && limitProp.TryGetInt32(out var requested)
            && requested > 0)
        {
            limit = Math.Min(requested, MaxLimit);
        }

        string? book = null;
        if (args.TryGetProperty("book", out var bookProp)
            && bookProp.ValueKind == JsonValueKind.String)
        {
            book = bookProp.GetString();
            if (string.IsNullOrWhiteSpace(book))
                book = null;
        }

        var window = "recent";
        if (args.TryGetProperty("window", out var windowProp)
            && windowProp.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(windowProp.GetString()))
        {
            window = windowProp.GetString()!.Trim().ToLowerInvariant();
        }

        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? since = window switch
        {
            "today" => new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero),
            "week" => now.AddDays(-7),
            _ => null
        };

        try
        {
            var rows = new List<TimelineRow>(limit * 2);

            if (_journals is not null)
            {
                var entries = await _journals
                    .ListEntriesAsync(bookId: book, limit: Math.Min(limit, MaxLimit), ct)
                    .ConfigureAwait(false);
                foreach (var e in entries)
                {
                    if (!InWindow(e.OccurredAt, since))
                        continue;
                    rows.Add(new TimelineRow(
                        ChronologyText.FormatJournalProvenance(e.BookId, e.Source),
                        e.OccurredAt,
                        ChronologyText.TruncateBody(e.Body, BodyCharLimit)));
                }
            }

            if (_activitiesLog is not null && string.IsNullOrWhiteSpace(book))
            {
                var crumbs = await _activitiesLog
                    .ListRecentAsync(subject: null, limit: Math.Min(limit, MaxLimit), ct)
                    .ConfigureAwait(false);
                foreach (var c in crumbs)
                {
                    if (!InWindow(c.OccurredAt, since))
                        continue;
                    rows.Add(new TimelineRow(
                        ChronologyText.ActivityLogProvenance,
                        c.OccurredAt,
                        ChronologyText.FormatActivityText(
                            c.Activity,
                            c.Subject,
                            c.SessionDesc,
                            BodyCharLimit)));
                }
            }

            if (_memory is not null && string.IsNullOrWhiteSpace(book))
            {
                var episodic = await _memory
                    .RecallRecentDatedAsync(Math.Min(limit, MaxLimit), ct)
                    .ConfigureAwait(false);
                foreach (var m in episodic)
                {
                    if (!InWindow(m.OccurredAt, since))
                        continue;
                    rows.Add(new TimelineRow(
                        "episodic",
                        m.OccurredAt,
                        ChronologyText.TruncateBody(m.Content, BodyCharLimit)));
                }
            }

            if (rows.Count == 0)
            {
                return new ToolResult(
                    Success: true,
                    Content: "no timeline rows found.",
                    Data: new { count = 0, window, book, limit });
            }

            rows.Sort(static (a, b) =>
            {
                var atA = ChronologyText.TryParseOccurredAt(a.OccurredAt) ?? DateTimeOffset.MinValue;
                var atB = ChronologyText.TryParseOccurredAt(b.OccurredAt) ?? DateTimeOffset.MinValue;
                var cmp = atB.CompareTo(atA);
                return cmp != 0
                    ? cmp
                    : string.Compare(a.Provenance, b.Provenance, StringComparison.Ordinal);
            });

            if (rows.Count > limit)
                rows = rows.GetRange(0, limit);

            var content = FormatRows(rows, window);
            return new ToolResult(
                Success: true,
                Content: content,
                Data: new { count = rows.Count, window, book, limit });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: recall_timeline failed: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }
    }

    private static string FormatRows(IReadOnlyList<TimelineRow> rows, string window)
    {
        var sb = new StringBuilder(64 + rows.Count * 160);
        sb.Append("Timeline (").Append(window).Append(", ").Append(rows.Count)
            .Append(rows.Count == 1 ? " row" : " rows").Append("):");
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            sb.Append('\n').Append('[').Append(i + 1).Append("] ")
                .Append(r.Provenance).Append(" | ").Append(r.OccurredAt)
                .Append(" | ").Append(r.Text);
        }

        return sb.ToString();
    }

    private static bool InWindow(string? occurredAt, DateTimeOffset? since)
    {
        if (since is null)
            return true;
        var at = ChronologyText.TryParseOccurredAt(occurredAt);
        return at is { } dto && dto >= since.Value;
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "window": {
              "type": "string",
              "description": "Time window: recent (default), today, or week.",
              "enum": ["recent", "today", "week"],
              "default": "recent"
            },
            "book": {
              "type": "string",
              "description": "Optional journal book id filter (feeling, life_hacks, thoughts, wants, dreams, …)."
            },
            "limit": {
              "type": "integer",
              "description": "Maximum rows to return (default 8, capped at 20).",
              "default": 8,
              "minimum": 1
            }
          }
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private readonly record struct TimelineRow(string Provenance, string OccurredAt, string Text);
}
