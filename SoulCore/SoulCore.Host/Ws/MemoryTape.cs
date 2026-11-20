using SoulCore.Inference.Presence;
using SoulCore.Memory;

namespace SoulCore.Host.Ws;

/// <summary>
/// PROP-20.2: today-first memory tape for chat preamble [Memory] block.
/// Extracted so mind/identity preamble (PROP-20.4) can edit ChatContextBuilder
/// without colliding on ordering / truncation helpers.
/// </summary>
public static class MemoryTape
{
    /// <summary>
    /// Orders lines so all of <paramref name="utcNow"/>'s calendar day come first
    /// (newest within day), then older relevant hits (newest first).
    /// </summary>
    public static IReadOnlyList<MemoryRecallLine> OrderTodayFirst(
        IReadOnlyList<MemoryRecallLine> lines,
        DateTimeOffset? utcNow = null)
    {
        if (lines is null || lines.Count == 0)
            return lines ?? Array.Empty<MemoryRecallLine>();

        var now = utcNow ?? DateTimeOffset.UtcNow;
        var today = now.UtcDateTime.Date;

        var todayBucket = new List<(MemoryRecallLine Line, DateTimeOffset At, int Index)>();
        var olderBucket = new List<(MemoryRecallLine Line, DateTimeOffset At, int Index)>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line.Content))
                continue;

            var at = TryParseOccurredAt(line.OccurredAt);
            var entry = (line, at ?? DateTimeOffset.MinValue, i);
            if (at is { } parsed && parsed.UtcDateTime.Date == today)
                todayBucket.Add(entry);
            else
                olderBucket.Add(entry);
        }

        static int CompareNewestFirst(
            (MemoryRecallLine Line, DateTimeOffset At, int Index) a,
            (MemoryRecallLine Line, DateTimeOffset At, int Index) b)
        {
            var cmp = b.At.CompareTo(a.At);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        }

        todayBucket.Sort(CompareNewestFirst);
        olderBucket.Sort(CompareNewestFirst);

        var ordered = new List<MemoryRecallLine>(todayBucket.Count + olderBucket.Count);
        foreach (var e in todayBucket)
            ordered.Add(e.Line);
        foreach (var e in olderBucket)
            ordered.Add(e.Line);
        return ordered;
    }

    /// <summary>Prompt line: occurred_at then content.</summary>
    public static string FormatLine(MemoryRecallLine line)
    {
        var content = line.Content?.Trim() ?? string.Empty;
        var at = line.OccurredAt?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(at))
            return content;
        return at + " | " + content;
    }

    /// <summary>
    /// Builds the [Memory] section from dated lines. Orders today-first, then
    /// truncates from the end (older first) so today's lines are kept when the
    /// budget is tight.
    /// </summary>
    /// <param name="linePrefix">
    /// Optional per-line prefix (PROP-18.12: SMS passes <c>"- "</c> for list style;
    /// desk leaves null).
    /// </param>
    /// <param name="maxLines">
    /// Optional hard cap after today-first order (PROP-18.12: SMS uses
    /// <c>SmsMemoryRecallLimit</c>). Extra lines drop from the end (older first).
    /// </param>
    public static (string Block, int DroppedCount) BuildMemoryBlock(
        IReadOnlyList<MemoryRecallLine> lines,
        int budget,
        DateTimeOffset? utcNow = null,
        string? linePrefix = null,
        int? maxLines = null)
    {
        if (lines is null || lines.Count == 0)
            return (string.Empty, 0);

        var ordered = OrderTodayFirst(lines, utcNow);
        const string header = "[Memory]\n";
        var prefix = linePrefix ?? string.Empty;
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

            kept.Add(prefix.Length == 0 ? formatted : prefix + formatted);
        }

        var droppedCount = blankDropped;
        if (maxLines is > 0 && kept.Count > maxLines.Value)
        {
            droppedCount += kept.Count - maxLines.Value;
            kept.RemoveRange(maxLines.Value, kept.Count - maxLines.Value);
        }

        while (kept.Count > 0)
        {
            var projected = header + string.Join("\n", kept) + "\n\n";
            if (projected.Length <= budget || budget <= 0)
                break;
            kept.RemoveAt(kept.Count - 1);
            droppedCount++;
        }

        if (kept.Count == 0)
            return (string.Empty, droppedCount);

        return (header + string.Join("\n", kept) + "\n\n", droppedCount);
    }

    /// <summary>Legacy string list: preserve input order; truncate oldest (end) first.</summary>
    public static (string Block, int DroppedCount) BuildMemoryBlock(
        IReadOnlyList<string> recentMemories,
        int budget)
    {
        if (recentMemories is null || recentMemories.Count == 0)
            return (string.Empty, 0);

        var lines = new MemoryRecallLine[recentMemories.Count];
        for (var i = 0; i < recentMemories.Count; i++)
            lines[i] = new MemoryRecallLine(recentMemories[i], string.Empty);

        // Empty OccurredAt → all land in "older"; OrderTodayFirst keeps relative
        // index order when timestamps tie at MinValue — so input order is preserved.
        return BuildMemoryBlock(lines, budget, utcNow: DateTimeOffset.UtcNow);
    }

    /// <summary>Delegates to <see cref="ChronologyText.TryParseOccurredAt"/> (PROP-18.20).</summary>
    public static DateTimeOffset? TryParseOccurredAt(string? occurredAt) =>
        ChronologyText.TryParseOccurredAt(occurredAt);

    public static IReadOnlyList<string> ProjectContent(IReadOnlyList<MemoryRecallLine> lines)
    {
        if (lines is null || lines.Count == 0)
            return Array.Empty<string>();
        var list = new List<string>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i].Content))
                list.Add(lines[i].Content.Trim());
        }

        return list;
    }

    public static IReadOnlyList<MemoryRecallLine> StripUntrusted(IReadOnlyList<MemoryRecallLine> lines) =>
        UntrustedObservation.StripWhereMarked(lines, static l => l.Content);
}
