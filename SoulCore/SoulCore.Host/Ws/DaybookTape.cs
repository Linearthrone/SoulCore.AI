using System.Text;
using SoulCore.Memory;

namespace SoulCore.Host.Ws;

/// <summary>
/// PROP-18.2: [Daybook] bridge — recent dated journal (+ optional activity-log crumbs)
/// with provenance tags so leisure notes do not flood the identity/memory budget.
/// </summary>
public static class DaybookTape
{
    /// <summary>Soft char budget for the whole [Daybook] block inside the 16k preamble.</summary>
    public const int DaybookCharBudget = 2400;

    /// <summary>Max journal lines injected per turn.</summary>
    public const int MaxJournalLines = 5;

    /// <summary>Max activity-log crumbs injected per turn.</summary>
    public const int MaxActivityCrumbs = 3;

    /// <summary>Body / desc truncation so leisure prose stays short in chat.</summary>
    public const int BodyCharLimit = 280;

    /// <summary>How many journal rows to pull before selecting/truncating.</summary>
    public const int JournalRecallLimit = 12;

    /// <summary>How many activity-log rows to pull before selecting/truncating.</summary>
    public const int ActivityRecallLimit = 8;

    public const string Marker = "[Daybook]";

    /// <summary>
    /// Books preferred for chat daybook (feeling / life_hacks / mind journals).
    /// Animation/environment still allowed when they are the only recent residue.
    /// </summary>
    public static readonly HashSet<string> PreferredBookIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "feeling", "life_hacks", "thoughts", "wants", "dreams"
    };

    /// <summary>One provenance-tagged daybook line ready for the preamble or tool output.</summary>
    public readonly record struct DaybookLine(
        string Provenance,
        string OccurredAt,
        string Text,
        bool Preferred);

    /// <summary>Maps a journal entry to a tagged daybook line.</summary>
    public static DaybookLine FromJournal(VictoriaJournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var book = (entry.BookId ?? string.Empty).Trim().ToLowerInvariant();
        var provenance = ChronologyText.FormatJournalProvenance(entry.BookId, entry.Source);
        var preferred = PreferredBookIds.Contains(book);
        return new DaybookLine(
            provenance,
            entry.OccurredAt?.Trim() ?? string.Empty,
            TruncateBody(entry.Body),
            preferred);
    }

    /// <summary>Maps an activities-log row to a short crumb line.</summary>
    public static DaybookLine FromActivity(VictoriaActivityLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new DaybookLine(
            ChronologyText.ActivityLogProvenance,
            entry.OccurredAt?.Trim() ?? string.Empty,
            ChronologyText.FormatActivityText(
                entry.Activity,
                entry.Subject,
                entry.SessionDesc,
                BodyCharLimit),
            Preferred: false);
    }

    /// <summary>Prompt line: provenance | occurred_at | text.</summary>
    public static string FormatLine(DaybookLine line)
    {
        var text = line.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var at = line.OccurredAt?.Trim() ?? string.Empty;
        var prov = line.Provenance?.Trim() ?? "journal";
        if (string.IsNullOrEmpty(at))
            return prov + " | " + text;
        return prov + " | " + at + " | " + text;
    }

    /// <summary>
    /// Today-first order among preferred journals, then other journals, then
    /// activity crumbs (newest within each bucket).
    /// </summary>
    public static IReadOnlyList<DaybookLine> OrderForInject(
        IReadOnlyList<DaybookLine> lines,
        DateTimeOffset? utcNow = null)
    {
        if (lines is null || lines.Count == 0)
            return Array.Empty<DaybookLine>();

        var now = utcNow ?? DateTimeOffset.UtcNow;
        var today = now.UtcDateTime.Date;

        var preferredToday = new List<(DaybookLine Line, DateTimeOffset At, int Index)>();
        var preferredOlder = new List<(DaybookLine Line, DateTimeOffset At, int Index)>();
        var otherToday = new List<(DaybookLine Line, DateTimeOffset At, int Index)>();
        var otherOlder = new List<(DaybookLine Line, DateTimeOffset At, int Index)>();
        var activities = new List<(DaybookLine Line, DateTimeOffset At, int Index)>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            var at = ChronologyText.TryParseOccurredAt(line.OccurredAt) ?? DateTimeOffset.MinValue;
            var entry = (line, at, i);
            var isActivity = line.Provenance.StartsWith("activity-log", StringComparison.OrdinalIgnoreCase);
            if (isActivity)
            {
                activities.Add(entry);
                continue;
            }

            var isToday = at != DateTimeOffset.MinValue && at.UtcDateTime.Date == today;
            if (line.Preferred)
            {
                if (isToday) preferredToday.Add(entry);
                else preferredOlder.Add(entry);
            }
            else
            {
                if (isToday) otherToday.Add(entry);
                else otherOlder.Add(entry);
            }
        }

        static int CompareNewestFirst(
            (DaybookLine Line, DateTimeOffset At, int Index) a,
            (DaybookLine Line, DateTimeOffset At, int Index) b)
        {
            var cmp = b.At.CompareTo(a.At);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        }

        preferredToday.Sort(CompareNewestFirst);
        preferredOlder.Sort(CompareNewestFirst);
        otherToday.Sort(CompareNewestFirst);
        otherOlder.Sort(CompareNewestFirst);
        activities.Sort(CompareNewestFirst);

        var ordered = new List<DaybookLine>(lines.Count);
        void Take(List<(DaybookLine Line, DateTimeOffset At, int Index)> bucket, int remaining)
        {
            for (var i = 0; i < bucket.Count && remaining > 0; i++)
            {
                ordered.Add(bucket[i].Line);
                remaining--;
            }
        }

        // Cap journals vs crumbs separately so activity-log cannot crowd out journals.
        var journalSlots = MaxJournalLines;
        Take(preferredToday, journalSlots);
        journalSlots = MaxJournalLines - CountJournals(ordered);
        Take(preferredOlder, journalSlots);
        journalSlots = MaxJournalLines - CountJournals(ordered);
        Take(otherToday, journalSlots);
        journalSlots = MaxJournalLines - CountJournals(ordered);
        Take(otherOlder, journalSlots);

        var activitySlots = MaxActivityCrumbs;
        for (var i = 0; i < activities.Count && activitySlots > 0; i++)
        {
            ordered.Add(activities[i].Line);
            activitySlots--;
        }

        return ordered;
    }

    /// <summary>
    /// Builds the [Daybook] section. Returns empty when there is nothing to inject
    /// or <paramref name="budget"/> is too small for the header.
    /// </summary>
    public static (string Block, int DroppedCount) BuildDaybookBlock(
        IReadOnlyList<DaybookLine> lines,
        int budget = DaybookCharBudget,
        DateTimeOffset? utcNow = null)
    {
        if (lines is null || lines.Count == 0 || budget < Marker.Length + 8)
            return (string.Empty, lines?.Count ?? 0);

        var ordered = OrderForInject(lines, utcNow);
        const string header =
            Marker + "\n" +
            "Recent private notes (provenance-tagged). Leisure crumbs stay short; do not dump them into chat.\n";

        var kept = new List<string>(ordered.Count);
        var blankDropped = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            var formatted = FormatLine(ordered[i]);
            if (string.IsNullOrWhiteSpace(formatted))
            {
                blankDropped++;
                continue;
            }

            kept.Add(formatted);
        }

        var droppedCount = blankDropped + Math.Max(0, (lines?.Count ?? 0) - ordered.Count);
        while (kept.Count > 0)
        {
            var projected = header + string.Join("\n", kept) + "\n\n";
            if (projected.Length <= budget)
                break;
            kept.RemoveAt(kept.Count - 1);
            droppedCount++;
        }

        if (kept.Count == 0)
            return (string.Empty, droppedCount);

        return (header + string.Join("\n", kept) + "\n\n", droppedCount);
    }

    /// <summary>Collects daybook lines from journal + optional activity-log stores.</summary>
    public static async Task<IReadOnlyList<DaybookLine>> LoadLinesAsync(
        IVictoriaJournalStore? journals,
        IVictoriaActivitiesLogStore? activitiesLog,
        CancellationToken cancellationToken = default)
    {
        var list = new List<DaybookLine>(JournalRecallLimit + MaxActivityCrumbs);

        if (journals is not null)
        {
            var entries = await journals
                .ListEntriesAsync(bookId: null, limit: JournalRecallLimit, cancellationToken)
                .ConfigureAwait(false);
            foreach (var e in entries)
                list.Add(FromJournal(e));
        }

        if (activitiesLog is not null)
        {
            var crumbs = await activitiesLog
                .ListRecentAsync(subject: null, limit: ActivityRecallLimit, cancellationToken)
                .ConfigureAwait(false);
            foreach (var c in crumbs)
                list.Add(FromActivity(c));
        }

        return list;
    }

    public static string TruncateBody(string? body) =>
        ChronologyText.TruncateBody(body, BodyCharLimit);

    private static int CountJournals(List<DaybookLine> ordered)
    {
        var n = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            if (!ordered[i].Provenance.StartsWith("activity-log", StringComparison.OrdinalIgnoreCase))
                n++;
        }

        return n;
    }
}
