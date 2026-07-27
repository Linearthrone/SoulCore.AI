namespace SoulCore.Inference.Tools.Body;

/// <summary>
/// Maps model-supplied animation names (and DetectAnimationIntent aliases) to
/// the canonical UE <c>play_animation</c> names used by
/// <c>ChatWebSocketHandler.DetectAnimationIntent</c> (BED-092 / BED-132).
/// </summary>
public static class AnimationNameMap
{
    /// <summary>
    /// The 12 canonical UE animation names returned by DetectAnimationIntent.
    /// </summary>
    public static readonly IReadOnlyList<string> CanonicalNames = new[]
    {
        "wave",
        "nod",
        "shake_head",
        "bow",
        "clap",
        "dance",
        "laugh",
        "point",
        "jump",
        "sit",
        "stand",
        "thumbs_up",
    };

    private static readonly Dictionary<string, string> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Canonical → self
            ["wave"] = "wave",
            ["nod"] = "nod",
            ["shake_head"] = "shake_head",
            ["bow"] = "bow",
            ["clap"] = "clap",
            ["dance"] = "dance",
            ["laugh"] = "laugh",
            ["point"] = "point",
            ["jump"] = "jump",
            ["sit"] = "sit",
            ["stand"] = "stand",
            ["thumbs_up"] = "thumbs_up",

            // DetectAnimationIntent multi-word / synonym aliases
            ["wave_hello"] = "wave",
            ["wave_goodbye"] = "wave",
            ["wave_bye"] = "wave",
            ["wave hello"] = "wave",
            ["wave goodbye"] = "wave",
            ["wave bye"] = "wave",
            ["yes"] = "nod",
            ["no"] = "shake_head",
            ["shake your head"] = "shake_head",
            ["shake_your_head"] = "shake_head",
            ["thumbs-up"] = "thumbs_up",
            ["thumbs up"] = "thumbs_up",
            ["applaud"] = "clap",
            ["giggle"] = "laugh",
            ["point at"] = "point",
            ["point_at"] = "point",
            ["sit down"] = "sit",
            ["sit_down"] = "sit",
            ["stand up"] = "stand",
            ["stand_up"] = "stand",
        };

    /// <summary>
    /// Resolves <paramref name="raw"/> to a canonical UE animation name when
    /// it matches a known alias; otherwise returns the trimmed lowercased raw
    /// string so unknown montage names can still be forwarded to UE.
    /// Returns null when <paramref name="raw"/> is null/whitespace.
    /// </summary>
    public static string? Resolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var key = raw.Trim();
        if (Aliases.TryGetValue(key, out var canonical))
            return canonical;

        // Normalize underscores/spaces for a second pass (e.g. "Wave Goodbye").
        var normalized = key.ToLowerInvariant().Replace('-', '_');
        if (Aliases.TryGetValue(normalized, out canonical))
            return canonical;

        var spaced = normalized.Replace('_', ' ');
        if (Aliases.TryGetValue(spaced, out canonical))
            return canonical;

        return normalized;
    }
}
