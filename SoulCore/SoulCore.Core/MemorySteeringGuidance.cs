using System.Text;

namespace SoulCore.Core;

/// <summary>
/// Prompt contract so recalled episodic lines steer action instead of flavoring chat.
/// Without this, Victoria can surface Maestro/class memories in [Memory] and still
/// rediscover the procedure as if brand new.
/// </summary>
public static class MemorySteeringGuidance
{
    public const string Marker = "[How I use memory]";

    /// <summary>Max SOPs / life-hack lines injected per turn.</summary>
    public const int MaxKnownProcedures = 3;

    /// <summary>Chars kept from each SOP steps preview in the preamble.</summary>
    public const int StepsPreviewCharLimit = 420;

    public const string Block =
        Marker + "\n" +
        "The [Memory] lines above are YOUR lived records — how-to and continuity, not flavor. " +
        "When a memory names steps you already used (login, Maestro, apps, URLs, paths, click order), " +
        "FOLLOW those steps. Do not act brand-new or rediscover a routine you already remember. " +
        "If mid-task you notice you are reinventing or contradicting a memory or a [Known procedures] SOP, " +
        "CORRECT yourself immediately and resume the known procedure. " +
        "For named routines, call sop_list / sop_get before inventing steps when a matching SOP exists. " +
        "NEVER invent sites, apps, or course platforms (Coursera, Udemy, edX, etc.) that are not in " +
        "[Memory], [Known procedures], a tool result, or Kayleigh's words. " +
        "Her real class surface is Maestro when memory/SOP/tools say so — do not swap in a guess.";

    /// <summary>One matched procedure line ready for the preamble.</summary>
    public readonly record struct KnownProcedureLine(
        long Id,
        string Title,
        string WhenText,
        string StepsPreview);

    /// <summary>Appends the steering block when missing. Idempotent.</summary>
    public static string AppendToPreamble(string? contextPreamble)
    {
        var baseText = string.IsNullOrWhiteSpace(contextPreamble)
            ? string.Empty
            : contextPreamble.TrimEnd();

        if (baseText.Contains(Marker, StringComparison.Ordinal))
            return baseText;

        if (baseText.Length == 0)
            return Block;

        return baseText + "\n\n" + Block;
    }

    /// <summary>
    /// Builds a compact [Known procedures] block from matched SOP rows.
    /// Empty when there are no matches.
    /// </summary>
    public static string BuildKnownProceduresBlock(IReadOnlyList<KnownProcedureLine> matches)
    {
        if (matches is null || matches.Count == 0)
            return string.Empty;

        var sb = new StringBuilder(512);
        sb.Append("[Known procedures]\n");
        sb.Append(
            "Matched from your SOP store for this turn. Use these steps. " +
            "Call sop_get with the id for the full SOP if truncated. Do not invent a beginner path.\n");

        var n = Math.Min(matches.Count, MaxKnownProcedures);
        for (var i = 0; i < n; i++)
        {
            var m = matches[i];
            sb.Append('[').Append(m.Id).Append("] ").Append(m.Title.Trim());
            if (!string.IsNullOrWhiteSpace(m.WhenText))
                sb.Append(" — When: ").Append(Truncate(m.WhenText.Trim(), 160));
            sb.Append('\n');
            if (!string.IsNullOrWhiteSpace(m.StepsPreview))
                sb.Append("Steps: ").Append(Truncate(m.StepsPreview.Trim(), StepsPreviewCharLimit)).Append('\n');
        }

        sb.Append('\n');
        return sb.ToString();
    }

    /// <summary>
    /// Rank SOPs by token overlap with the user ask + recalled memories.
    /// Returns up to <see cref="MaxKnownProcedures"/> matches with score &gt; 0.
    /// </summary>
    public static IReadOnlyList<KnownProcedureLine> MatchProcedures(
        string userText,
        IReadOnlyList<string> recentMemories,
        IReadOnlyList<(long Id, string Title, string WhenText, string Steps)> sops)
    {
        if (sops is null || sops.Count == 0)
            return Array.Empty<KnownProcedureLine>();

        var query = BuildQueryCorpus(userText, recentMemories);
        if (query.Length == 0)
            return Array.Empty<KnownProcedureLine>();

        var queryTokens = Tokenize(query);
        if (queryTokens.Count == 0)
            return Array.Empty<KnownProcedureLine>();

        var scored = new List<(int Score, KnownProcedureLine Line)>(sops.Count);
        foreach (var sop in sops)
        {
            if (sop.Id <= 0 || string.IsNullOrWhiteSpace(sop.Title))
                continue;

            var hay = string.Concat(
                sop.Title, "\n",
                sop.WhenText ?? string.Empty, "\n",
                sop.Steps ?? string.Empty);
            var score = ScoreOverlap(queryTokens, hay);
            // Title hit against the raw user ask is a strong signal (Maestro, class, login).
            if (ContainsIgnoreCase(userText, sop.Title.Trim()))
                score += 5;
            if (score <= 0)
                continue;

            scored.Add((
                score,
                new KnownProcedureLine(
                    sop.Id,
                    sop.Title.Trim(),
                    (sop.WhenText ?? string.Empty).Trim(),
                    Truncate((sop.Steps ?? string.Empty).Trim(), StepsPreviewCharLimit))));
        }

        if (scored.Count == 0)
            return Array.Empty<KnownProcedureLine>();

        scored.Sort(static (a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.Line.Id.CompareTo(b.Line.Id);
        });

        var take = Math.Min(scored.Count, MaxKnownProcedures);
        var result = new KnownProcedureLine[take];
        for (var i = 0; i < take; i++)
            result[i] = scored[i].Line;
        return result;
    }

    private static string BuildQueryCorpus(string userText, IReadOnlyList<string> recentMemories)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(userText))
            sb.Append(userText.Trim());
        if (recentMemories is { Count: > 0 })
        {
            foreach (var m in recentMemories)
            {
                if (string.IsNullOrWhiteSpace(m))
                    continue;
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(m.Trim());
            }
        }

        return sb.ToString();
    }

    private static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var span = text.AsSpan();
        var start = -1;
        for (var i = 0; i <= span.Length; i++)
        {
            var atEnd = i == span.Length;
            var c = atEnd ? ' ' : span[i];
            if (char.IsLetterOrDigit(c))
            {
                if (start < 0)
                    start = i;
                continue;
            }

            if (start < 0)
                continue;

            var len = i - start;
            if (len >= 3)
                tokens.Add(span.Slice(start, len).ToString());
            start = -1;
        }

        return tokens;
    }

    private static int ScoreOverlap(HashSet<string> queryTokens, string haystack)
    {
        if (string.IsNullOrWhiteSpace(haystack) || queryTokens.Count == 0)
            return 0;

        var hayTokens = Tokenize(haystack);
        var score = 0;
        foreach (var t in queryTokens)
        {
            if (hayTokens.Contains(t))
                score++;
        }

        return score;
    }

    private static bool ContainsIgnoreCase(string? haystack, string needle)
    {
        if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needle))
            return false;
        return haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string text, int maxChars)
    {
        if (maxChars <= 0 || string.IsNullOrEmpty(text))
            return string.Empty;
        return text.Length <= maxChars ? text : text[..maxChars];
    }
}
