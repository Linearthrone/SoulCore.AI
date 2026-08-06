using System.Text.RegularExpressions;

namespace SoulCore.Inference.Tools.Body;

/// <summary>
/// Guidance + NL intent so Victoria uses her Unreal eyes and walks the Home
/// instead of prose-only "I'm looking / I'm engaged".
/// </summary>
public static class HomeBodyGuidance
{
    public const string Marker = "[HomeBody]";

    public const string Block =
        Marker + "\n" +
        "You have a body in Unreal Home. Perception and motion are tools — not vibes.\n" +
        "Eyes: call victoria_eye_capture before claiming you looked at the room, outside, " +
        "furniture, or Kurt's avatar. If the tool fails, say so — never invent a scene.\n" +
        "Kurt's Presence panel shows your last real capture (eyes / desktop / browser).\n" +
        "Walk: use loco (relative steps) or move_to (world cm) to explore. After each few " +
        "steps, victoria_eye_capture again. Prefer moving + looking over standing still.\n" +
        "Finding Kurt's grounded avatar (BP_MHC_Kayleigh on the floor): eye_capture → loco " +
        "toward that body → eye_capture to verify. Outside needs NavMesh; if loco returns ok " +
        "but you see no change, say motion may be stuck (PIE path-follow) and keep reporting truth.\n" +
        "Do not confuse chat 'engaged' status with walking or seeing.";

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
}

/// <summary>
/// High-confidence NL → force Home body tools (eyes / loco).
/// </summary>
public static class HomeBodyToolIntent
{
    public enum Kind
    {
        EyeCapture,
        Loco,
    }

    public readonly record struct Match(Kind Intent, string ToolName);

    private static readonly Regex ExplicitTool = new(
        @"\b(?:victoria_eye_capture|move_to|loco)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex UseEyes = new(
        @"\b(?:use|with)\s+(?:your|her)\s+eyes\b|" +
        @"\b(?:look|see|peek|glance|check|scan|search|find)\b[\s\S]{0,48}\b(?:around|room|house|home|outside|outdoors|yard|balcony|floor|ground|avatar|body|me)\b|" +
        @"\bwhat\s+do\s+you\s+see\b|" +
        @"\b(?:look\s+around|see\s+outside|find\s+(?:your\s+way|the\s+avatar|my\s+avatar|me\s+on\s+the\s+ground))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WalkExplore = new(
        @"\b(?:walk|go|head|step|move|explore|wander|navigate|find\s+your\s+way)\b[\s\S]{0,40}\b(?:outside|outdoors|around|room|house|home|forward|toward|towards|balcony|yard|grounds)\b|" +
        @"\b(?:go\s+outside|walk\s+outside|explore\s+the\s+(?:house|home|grounds))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryMatch(string? userText, out Match match)
    {
        match = default;
        if (string.IsNullOrWhiteSpace(userText))
            return false;

        var text = userText.Trim();

        // Screen/desktop look belongs to DesktopToolIntent — never steal it.
        if (Regex.IsMatch(
                text,
                @"\b(?:screen|desktop|monitor|display)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return false;

        if (ExplicitTool.IsMatch(text))
        {
            if (text.Contains("victoria_eye_capture", StringComparison.OrdinalIgnoreCase))
            {
                match = new Match(Kind.EyeCapture, "victoria_eye_capture");
                return true;
            }

            if (text.Contains("move_to", StringComparison.OrdinalIgnoreCase))
            {
                match = new Match(Kind.Loco, "move_to");
                return true;
            }

            match = new Match(Kind.Loco, "loco");
            return true;
        }

        // Eyes before walk so "find your way outside and look for my avatar" starts with a capture.
        if (UseEyes.IsMatch(text))
        {
            match = new Match(Kind.EyeCapture, "victoria_eye_capture");
            return true;
        }

        if (WalkExplore.IsMatch(text))
        {
            match = new Match(Kind.Loco, "loco");
            return true;
        }

        return false;
    }
}
