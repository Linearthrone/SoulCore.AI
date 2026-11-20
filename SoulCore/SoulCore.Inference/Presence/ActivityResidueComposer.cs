using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-18.7 / Q1+Q4: build non-generic activities-log Desc and topic journal bodies.
/// Desc ≈ 1–2 sentences of what was new; journal floor ≈ 1–2 paragraphs, no upper cap.
/// </summary>
public static partial class ActivityResidueComposer
{
    public const string TopicJournalBookId = "thoughts";
    public const int MinJournalParagraphs = 2;

    /// <summary>Allowed activity kinds for the activities log (Q1).</summary>
    public static bool IsAllowedActivity(string? activity)
    {
        if (string.IsNullOrWhiteSpace(activity))
            return false;
        return activity.Equals("leisure", StringComparison.OrdinalIgnoreCase)
            || activity.Equals("research", StringComparison.OrdinalIgnoreCase)
            || activity.Equals("parked-work", StringComparison.OrdinalIgnoreCase)
            || activity.Equals("parked_work", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeActivity(string activity)
    {
        var t = activity.Trim().ToLowerInvariant().Replace('_', '-');
        if (t is "parkedwork")
            t = "parked-work";
        return t;
    }

    /// <summary>
    /// True when <paramref name="findings"/> has enough substance to leave residue
    /// (not empty theater).
    /// </summary>
    public static bool HasSubstance(string? findings)
    {
        if (string.IsNullOrWhiteSpace(findings))
            return false;
        var cleaned = CleanFindings(findings);
        return cleaned.Length >= 24;
    }

    /// <summary>
    /// Session-specific Desc (1–2 sentences). Throws when generic / empty.
    /// Different findings on the same subject must yield different Desc text.
    /// </summary>
    public static string BuildSessionDesc(string subject, string findings)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject must be non-empty.", nameof(subject));
        if (!HasSubstance(findings))
            throw new ArgumentException("Findings must carry session-specific substance.", nameof(findings));

        var cleaned = CleanFindings(findings);
        var topic = subject.Trim();
        var sentences = SplitSentences(cleaned);
        var pick = new StringBuilder();
        var count = 0;
        foreach (var s in sentences)
        {
            if (count >= 2)
                break;
            if (pick.Length > 0)
                pick.Append(' ');
            pick.Append(s);
            count++;
        }

        if (pick.Length == 0)
            pick.Append(cleaned);

        // Bound Desc to about two sentences without inventing a recycled template.
        var desc = pick.ToString().Trim();
        if (desc.Length > 420)
            desc = desc[..417].TrimEnd() + "…";

        // Anchor subject so the row is scannable, but keep the finding text dominant.
        if (!desc.Contains(topic, StringComparison.OrdinalIgnoreCase)
            && desc.Length + topic.Length < 400)
        {
            desc = "On " + topic + ": " + desc;
        }

        if (IsGenericDesc(desc, topic))
        {
            // Last resort: force the distinctive finding text into the Desc so two
            // sessions with different findings cannot collapse to the same template.
            desc = "This pass on " + topic + " turned up: " + TruncateOneLine(cleaned, 280);
        }

        if (IsGenericDesc(desc, topic))
            throw new InvalidOperationException(
                "Refusing generic activities-log Desc — findings must be session-specific.");

        return desc;
    }

    /// <summary>
    /// First-person topic journal body. Floor ≈ 1–2 paragraphs when there is
    /// something to say; no upper character cap (length follows the findings).
    /// </summary>
    public static string BuildTopicJournalBody(string subject, string findings, string activity)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject must be non-empty.", nameof(subject));
        if (!HasSubstance(findings))
            throw new ArgumentException("Findings must carry session-specific substance.", nameof(findings));

        var cleaned = CleanFindings(findings);
        var topic = subject.Trim();
        var kind = NormalizeActivity(activity);

        // If the model already wrote multi-paragraph first-person prose, keep it (no cap).
        var existingParas = SplitParagraphs(cleaned);
        if (existingParas.Count >= MinJournalParagraphs
            && LooksFirstPerson(cleaned))
        {
            return string.Join("\n\n", existingParas);
        }

        var lead = kind switch
        {
            "research" =>
                "I spent time researching " + topic
                + ". What felt new this pass was worth keeping in my own words.",
            "parked-work" =>
                "I advanced parked work around " + topic
                + ". Here is what actually moved while I was on it alone.",
            _ =>
                "I lingered with " + topic
                + " between talks. This is what stood out to me this pass."
        };

        var detail = cleaned;
        // Never emit one-liner theater: ensure a second paragraph of substance.
        if (SplitParagraphs(lead + "\n\n" + detail).Count < MinJournalParagraphs
            || detail.Length < 80)
        {
            detail = ExpandDetailParagraph(topic, cleaned);
        }

        var body = lead + "\n\n" + detail;
        var paras = SplitParagraphs(body);
        while (paras.Count < MinJournalParagraphs)
        {
            paras.Add(
                "I am leaving this note so the hour is not empty — the specifics above are from this session on "
                + topic + ", not a recycled template.");
        }

        return string.Join("\n\n", paras);
    }

    /// <summary>Tags for topic-scoped thoughts journal entries.</summary>
    public static string BuildTopicTagsJson(string subject, string activity)
    {
        var topic = subject.Trim();
        var kind = NormalizeActivity(activity);
        // Compact JSON array; subject as a plain tag for FED/topic filters.
        return "[\"topic\",\"activity-residue\",\""
            + EscapeJson(kind) + "\",\""
            + EscapeJson(topic) + "\"]";
    }

    public static bool IsGenericDesc(string desc, string subject)
    {
        if (string.IsNullOrWhiteSpace(desc))
            return true;
        var d = CollapseWs(desc).Trim();
        var topic = CollapseWs(subject).Trim();

        if (GenericTemplateRegex().IsMatch(d))
            return true;

        // "Read paper on X" / "Researched X" / "Continued research on X" with no extra meat.
        if (!string.IsNullOrEmpty(topic))
        {
            var onlyTopic = CollapseWs(
                Regex.Replace(
                    d,
                    @"\b(read(ing)?(\s+a)?\s+paper\s+on|continued\s+(research|reading|work)(\s+on)?|researched|looked\s+into|worked\s+on|leisure\s+session(\s+on)?|parked[-\s]?work\s+(session|pass)(\s+on)?)\b",
                    "",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            onlyTopic = onlyTopic.Trim(' ', '.', '-', ':');
            if (onlyTopic.Equals(topic, StringComparison.OrdinalIgnoreCase)
                || onlyTopic.Length < 12)
                return true;
        }

        return d.Length < 24;
    }

    public static string CleanFindings(string findings)
    {
        var t = findings.Trim();
        // Drop untrusted marker + topic= prefix if a leisure summary was passed through.
        if (t.StartsWith(UntrustedObservation.Marker, StringComparison.OrdinalIgnoreCase))
        {
            t = t[UntrustedObservation.Marker.Length..].TrimStart();
            if (t.StartsWith("topic=", StringComparison.OrdinalIgnoreCase))
            {
                var sp = t.IndexOf(' ');
                t = sp > 0 ? t[(sp + 1)..].TrimStart() : "";
            }
        }

        t = UntrustedObservation.StripDangerous(t);
        return CollapseWs(t).Trim();
    }

    private static string ExpandDetailParagraph(string topic, string cleaned)
    {
        var sb = new StringBuilder(cleaned.Length + 160);
        sb.Append("The concrete thread I followed on ").Append(topic).Append(" was: ");
        sb.Append(cleaned);
        if (!cleaned.EndsWith('.') && !cleaned.EndsWith('!') && !cleaned.EndsWith('?'))
            sb.Append('.');
        sb.Append(" I want that specificity on the page so tomorrow’s me can tell this hour from another.");
        return sb.ToString();
    }

    private static bool LooksFirstPerson(string text) =>
        FirstPersonRegex().IsMatch(text);

    /// <summary>
    /// Paragraph count using the same split rules as journal composition
    /// (PROP-18.29 F2 — Writer must reuse this, not re-split).
    /// </summary>
    public static int CountParagraphs(string text) => SplitParagraphs(text).Count;

    private static List<string> SplitParagraphs(string text)
    {
        return text
            .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToList();
    }

    private static IEnumerable<string> SplitSentences(string text)
    {
        var parts = SentenceSplitRegex().Split(text);
        foreach (var p in parts)
        {
            var s = p.Trim();
            if (s.Length == 0)
                continue;
            if (!s.EndsWith('.') && !s.EndsWith('!') && !s.EndsWith('?'))
                s += ".";
            yield return s;
        }
    }

    private static string TruncateOneLine(string text, int max)
    {
        var one = CollapseWs(text);
        if (one.Length <= max)
            return one;
        return one[..(max - 1)].TrimEnd() + "…";
    }

    private static string CollapseWs(string text) =>
        WsRegex().Replace(text, " ").Trim();

    private static string EscapeJson(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WsRegex();

    [GeneratedRegex(
        @"(?i)^(read(ing)?(\s+a)?\s+paper\s+on\b|continued\s+(research|reading|work)\b|leisure\s+session\b|parked[-\s]?work\s+(session|pass)\b).*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex GenericTemplateRegex();

    [GeneratedRegex(
        @"(?<=[\.!\?])\s+",
        RegexOptions.CultureInvariant)]
    private static partial Regex SentenceSplitRegex();

    [GeneratedRegex(
        @"\b(I|I'm|I've|I'd|me|my|mine)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FirstPersonRegex();
}
