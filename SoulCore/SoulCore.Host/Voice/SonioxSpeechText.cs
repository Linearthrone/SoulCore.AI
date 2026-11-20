using System.Text;
using System.Text.RegularExpressions;

namespace SoulCore.Host.Voice;

/// <summary>
/// PROP-21: strip Soniox audio tags for chat display; keep mild *stress* in text.
/// </summary>
public static partial class SonioxSpeechText
{
    /// <summary>Bracketed audio tags like [warm], [chuckles], [long pause].</summary>
    [GeneratedRegex(@"\[[^\[\]]{1,64}\]", RegexOptions.CultureInvariant)]
    private static partial Regex AudioTagRegex();

    /// <summary>Markdown bold used as fake stage emphasis.</summary>
    [GeneratedRegex(@"\*\*([^*]+)\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownBoldRegex();

    /// <summary>Parenthetical stage directions: (smiles), (laughs softly).</summary>
    [GeneratedRegex(@"\((?:smiles|laughs|sighs|whispers|pauses|grins|nods|shakes head|clears throat)[^)]{0,40}\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StageParenRegex();

    public static string StripAudioTagsForDisplay(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var s = AudioTagRegex().Replace(text, " ");
        s = CollapseWs(s);
        return s.Trim();
    }

    /// <summary>
    /// Normalize model output for TTS: drop **bold** and (stage) asides; keep Soniox tags.
    /// </summary>
    public static string NormalizeForSpeak(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var s = MarkdownBoldRegex().Replace(text, "$1");
        s = StageParenRegex().Replace(s, " ");
        s = CollapseWs(s);
        return s.Trim();
    }

    public static string SpeechFormatGuidanceBlock { get; } =
        """
        [Speech]
        When you speak (Call / audible reply), write Soniox audio tags in English brackets before clauses — e.g. [warm], [chuckles], [softly], [excited], [pause].
        Shape emphasis with *word*, UPPERCASE, ..., —, ! — not markdown **bold** and not parenthetical stage directions like (smiles) or (laughs).
        Tags are delivery cues; do not narrate that you are using a tag.
        """;

    private static string CollapseWs(string s)
    {
        var sb = new StringBuilder(s.Length);
        var prevSpace = false;
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace)
                    sb.Append(' ');
                prevSpace = true;
            }
            else
            {
                sb.Append(ch);
                prevSpace = false;
            }
        }

        return sb.ToString();
    }
}
