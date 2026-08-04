using System.Globalization;
using System.Text;

namespace SoulCore.Core;

/// <summary>
/// Deterministic, low-agency want proposal from emotion + recent episodic.
/// Categories vary the phrasing; never requests browser/MT4/email/file acts.
/// </summary>
public static class SoulLoopWantProposal
{
    public const string CategoryReflect = "reflect";
    public const string CategorySettle = "settle";
    public const string CategoryReconnect = "reconnect";
    public const string CategorySavor = "savor";
    public const string CategoryEngage = "engage";
    public const string CategoryClarify = "clarify";
    public const string CategoryRecall = "recall";
    public const string CategoryNotice = "notice";
    /// <summary>Curious walkabout / room discovery in the Home environment.</summary>
    public const string CategoryExplore = "explore";

    /// <summary>
    /// Builds a want string. Same inputs always yield the same output (InvariantCulture).
    /// Shape: <c>want[{category}]: {phrase} (emotion=… v=… a=… d=… f=…); recent=…</c>
    /// </summary>
    public static string Propose(
        string label,
        EmotionInfluencePrompt.EmotionFields fields,
        IReadOnlyList<string> recent)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(recent);

        var category = Classify(label, fields, recent);
        var phrase = PhraseFor(category, label, fields, recent);

        var sb = new StringBuilder(256);
        sb.Append("want[");
        sb.Append(category);
        sb.Append("]: ");
        sb.Append(phrase);
        sb.Append(" (emotion=");
        sb.Append(label);
        sb.Append(" v=");
        sb.Append(Format(fields.Valence));
        sb.Append(" a=");
        sb.Append(Format(fields.Arousal));
        sb.Append(" d=");
        sb.Append(Format(fields.Dominance));
        sb.Append(" f=");
        sb.Append(Format(fields.Focus));
        sb.Append(')');

        if (recent.Count > 0)
        {
            sb.Append("; recent=");
            AppendRecentSummary(sb, recent);
        }
        else
        {
            sb.Append("; recent=(none)");
        }

        return sb.ToString();
    }

    /// <summary>Public for tests / evidence — same classification as <see cref="Propose"/>.</summary>
    public static string Classify(
        string label,
        EmotionInfluencePrompt.EmotionFields fields,
        IReadOnlyList<string> recent)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(recent);

        var episodicHint = DetectEpisodicHint(recent);
        if (episodicHint == CategoryClarify)
            return CategoryClarify;
        if (episodicHint == CategoryRecall)
            return CategoryRecall;
        if (episodicHint == CategoryExplore)
            return CategoryExplore;

        var fromEmotion = label switch
        {
            "tense" => CategorySettle,
            "low" => CategoryReconnect,
            "content" => CategorySavor,
            "excited" => CategoryEngage,
            "calm" => recent.Count > 0 ? CategoryNotice : CategoryReflect,
            _ => recent.Count > 0 ? CategoryNotice : CategoryReflect
        };

        // High focus nudges engage/notice toward staying with the thread (still reflect-only).
        if (fromEmotion is CategoryReflect or CategoryNotice && fields.Focus >= 0.65)
            return CategoryEngage;

        return fromEmotion;
    }

    private static string PhraseFor(
        string category,
        string label,
        EmotionInfluencePrompt.EmotionFields fields,
        IReadOnlyList<string> recent)
    {
        var episodeCue = recent.Count switch
        {
            0 => "with an empty recent buffer",
            1 => "holding one recent beat",
            _ => $"holding {recent.Count} recent beats"
        };

        return category switch
        {
            CategorySettle =>
                $"ease the tension and settle into a steadier breath ({episodeCue})",
            CategoryReconnect =>
                $"reconnect softly and keep company without pushing ({episodeCue})",
            CategorySavor =>
                $"savor the easy mood and stay warmly present ({episodeCue})",
            CategoryEngage =>
                fields.Focus >= 0.65
                    ? $"stay with the thread and engage attentively ({episodeCue})"
                    : $"lean in with bright, curious presence ({episodeCue})",
            CategoryExplore =>
                $"walk the Home with open curiosity — notice rooms, light, and places worth returning to ({episodeCue})",
            CategoryClarify =>
                $"gently clarify what was meant before moving on ({episodeCue})",
            CategoryRecall =>
                $"recall the recent thread and weave it into presence ({episodeCue})",
            CategoryNotice =>
                $"notice what just happened and stay lightly aware ({episodeCue})",
            _ =>
                $"stay present and gently reflect while feeling {label} ({episodeCue})"
        };
    }

    private static string? DetectEpisodicHint(IReadOnlyList<string> recent)
    {
        if (recent.Count == 0)
            return null;

        foreach (var row in recent)
        {
            if (string.IsNullOrWhiteSpace(row))
                continue;

            if (ContainsAny(row,
                    "correction", "wrong", "misunderstand", "actually", "?", "clarify", "meant"))
                return CategoryClarify;

            if (ContainsAny(row,
                    "remember", "earlier", "yesterday", "previously", "last time", "ago", "recall"))
                return CategoryRecall;

            if (ContainsAny(row,
                    "explore", "home", "room", "walkabout", "wander", "curious",
                    "education module", "entertainment", "workstation", "vm desk"))
                return CategoryExplore;
        }

        return null;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void AppendRecentSummary(StringBuilder sb, IReadOnlyList<string> recent)
    {
        var snippet = recent[0].Replace('\n', ' ').Replace('\r', ' ');
        if (snippet.Length > 80)
            snippet = snippet[..80] + "…";
        sb.Append(snippet);
        if (recent.Count > 1)
            sb.Append(" (+").Append(recent.Count - 1).Append(" more)");
    }

    private static string Format(double value)
        => value.ToString("0.00", CultureInfo.InvariantCulture);
}
