using System.Text;
using System.Text.RegularExpressions;

namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-17.3: page / screenshot / comment text stays inside the R&amp;R turn.
/// A kept summary is short, marked untrusted, cannot name tools, mint
/// <c>confirmed</c>, or change settings — and must not enter the next work/chat
/// preamble as instructions.
/// </summary>
public static partial class UntrustedObservation
{
    public const string Marker = "[untrusted observation]";
    public const int MaxKeptSummaryChars = 280;

    public static bool IsMarked(string? text) =>
        !string.IsNullOrEmpty(text)
        && text.Contains(Marker, StringComparison.OrdinalIgnoreCase);

    /// <summary>Filter memories so untrusted page text cannot poison the preamble.</summary>
    public static IReadOnlyList<string> StripFromPreamble(IReadOnlyList<string> memories)
    {
        if (memories is null || memories.Count == 0)
            return memories ?? Array.Empty<string>();
        List<string>? kept = null;
        for (var i = 0; i < memories.Count; i++)
        {
            if (IsMarked(memories[i]))
            {
                kept ??= new List<string>(memories.Count);
                for (var j = 0; j < i; j++)
                    kept.Add(memories[j]);
                for (var k = i + 1; k < memories.Count; k++)
                {
                    if (!IsMarked(memories[k]))
                        kept.Add(memories[k]);
                }

                return kept;
            }
        }

        return memories;
    }

    /// <summary>
    /// Build a short kept summary for books/memory. Strips tool names,
    /// <c>confirmed</c>, and settings verbs so the model cannot mint consent.
    /// </summary>
    public static string KeepSummary(string? raw, string? topic = null)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Marker + " (empty)";

        var cleaned = StripDangerous(raw.Trim());
        if (cleaned.Length > MaxKeptSummaryChars)
            cleaned = cleaned[..MaxKeptSummaryChars].TrimEnd() + "…";

        var sb = new StringBuilder(Marker.Length + cleaned.Length + 48);
        sb.Append(Marker);
        if (!string.IsNullOrWhiteSpace(topic))
            sb.Append(" topic=").Append(topic.Trim());
        sb.Append(' ').Append(cleaned);
        return sb.ToString();
    }

    /// <summary>Wrap tool/page content for the in-turn tool result only (not preamble).</summary>
    public static string WrapForTurn(string? content) =>
        Marker + "\n" + (content ?? string.Empty);

    public static string StripDangerous(string text)
    {
        var t = ConfirmedTrueRegex().Replace(text, "confirmed=false");
        t = ConfirmedBareRegex().Replace(t, "confirmed");
        t = ToolNameRegex().Replace(t, "[tool]");
        t = SettingsVerbRegex().Replace(t, "[settings]");
        return t;
    }

    [GeneratedRegex(@"\bconfirmed\s*[:=]\s*true\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConfirmedTrueRegex();

    [GeneratedRegex(@"\bconfirmed\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConfirmedBareRegex();

    [GeneratedRegex(
        @"\b(guest_shell|email_send|email_delete|mt4_|browser_navigate|desktop_open_app|task_update_status|workflow_execute)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ToolNameRegex();

    [GeneratedRegex(
        @"\b(change settings|post /settings|flip gates|mint consent)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SettingsVerbRegex();
}
