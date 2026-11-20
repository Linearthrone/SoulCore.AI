using System.Globalization;

namespace SoulCore.Memory;

/// <summary>
/// PROP-18.20: shared chronology formatting for [Daybook] preamble and
/// <c>recall_timeline</c>. Lives in Memory so Host.Ws and Inference.Tools share
/// one provenance / parse / truncate path without Host→Inference coupling.
/// </summary>
public static class ChronologyText
{
    public const string ActivityLogProvenance = "activity-log";

    /// <summary>
    /// Journal provenance: <c>journal:{book}</c> or <c>journal:{book}/{source}</c>
    /// when source is not empty/self/chat.
    /// </summary>
    public static string FormatJournalProvenance(string? bookId, string? source)
    {
        var book = (bookId ?? string.Empty).Trim().ToLowerInvariant();
        var src = (source ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(src) || src is "self" or "chat"
            ? "journal:" + book
            : "journal:" + book + "/" + src;
    }

    /// <summary>
    /// Activity-log crumb text: skips empty activity/subject/desc so empty fields
    /// do not become <c>" /  — "</c> junk. Truncates session desc to
    /// <paramref name="bodyCharLimit"/>.
    /// </summary>
    public static string FormatActivityText(
        string? activity,
        string? subject,
        string? sessionDesc,
        int bodyCharLimit)
    {
        var act = (activity ?? string.Empty).Trim();
        var subj = (subject ?? string.Empty).Trim();
        var desc = TruncateBody(sessionDesc, bodyCharLimit);
        var head = string.IsNullOrEmpty(subj)
            ? act
            : string.IsNullOrEmpty(act)
                ? subj
                : act + " / " + subj;
        if (string.IsNullOrEmpty(head))
            return desc;
        if (string.IsNullOrEmpty(desc))
            return head;
        return head + " — " + desc;
    }

    /// <summary>
    /// Store stamp → UTC <see cref="DateTimeOffset"/>. Invariant culture,
    /// AssumeUniversal + AdjustToUniversal (same contract as MemoryTape).
    /// </summary>
    public static DateTimeOffset? TryParseOccurredAt(string? occurredAt)
    {
        if (string.IsNullOrWhiteSpace(occurredAt))
            return null;

        if (DateTimeOffset.TryParse(
                occurredAt.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
            return dto;

        return null;
    }

    /// <summary>
    /// Collapse CRLF / double spaces, then truncate to <paramref name="charLimit"/>
    /// with an ellipsis. Desk (280) and tool (400) pass their own budgets.
    /// </summary>
    public static string TruncateBody(string? body, int charLimit)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;
        if (charLimit < 1)
            return string.Empty;

        var trimmed = body.Trim().Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ');
        while (trimmed.Contains("  ", StringComparison.Ordinal))
            trimmed = trimmed.Replace("  ", " ", StringComparison.Ordinal);
        if (trimmed.Length <= charLimit)
            return trimmed;
        return trimmed[..charLimit].TrimEnd() + "…";
    }
}
