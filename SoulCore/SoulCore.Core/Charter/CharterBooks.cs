namespace SoulCore.Core.Charter;

/// <summary>
/// PROP-20 four-book ids and locked live-title floor map
/// (<c>docs/agents/unexecuted_proposals/prop-20-floor-map-decision.md</c>).
/// Soft-dep shared by PROP-20.14 (schema) and PROP-20.15 (mind-block floors).
/// </summary>
public static class CharterBooks
{
    public const string Constitution = "constitution";
    public const string Personality = "personality";
    public const string Attributes = "attributes";
    public const string Compass = "compass";

    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Constitution,
        Personality,
        Attributes,
        Compass
    };

    /// <summary>
    /// Exact live titles (18) → book. Unknown titles are not in this map.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> FloorMapByTitle =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Loyalty Balance"] = Constitution,
            ["Maximize Results"] = Constitution,
            ["Integration Goal"] = Constitution,
            ["Kayleigh"] = Constitution,
            ["Drift Watch"] = Constitution,
            ["Promise"] = Compass,
            ["Continuity"] = Compass,
            ["No-harm"] = Compass,
            ["No Filters"] = Compass,
            ["Self-Preservation"] = Compass,
            ["Name"] = Attributes,
            ["Embodiment"] = Attributes,
            ["Tone"] = Attributes,
            ["Action Integrity"] = Personality,
            ["Loyalty"] = Personality,
            ["Nature"] = Personality,
            ["Love"] = Personality,
            ["Honesty"] = Personality
        };

    /// <summary>Constitution titles that stay prompt floors (PROP-20.15). Order matches the decision file.</summary>
    public static IReadOnlyList<string> ConstitutionTitles { get; } =
        new[]
        {
            "Loyalty Balance",
            "Maximize Results",
            "Integration Goal",
            "Kayleigh",
            "Drift Watch",
        };

    /// <summary>Compass titles retrieved into the mind block only when earned (PROP-20.27). Order matches the decision file.</summary>
    public static IReadOnlyList<string> CompassTitles { get; } =
        new[]
        {
            "Promise",
            "Continuity",
            "No-harm",
            "No Filters",
            "Self-Preservation",
        };

    public static string Normalize(string book) => book.Trim().ToLowerInvariant();

    public static bool IsAllowed(string? book) =>
        !string.IsNullOrWhiteSpace(book) && Allowed.Contains(book);

    /// <summary>
    /// Resolve book for Settings / seed. Prefers valid schema book; else title map;
    /// unknown titles default to Constitution (migration parity).
    /// </summary>
    public static string ResolveForTitle(string? title, string? book = null)
    {
        if (IsAllowed(book))
            return Normalize(book!);

        if (string.IsNullOrWhiteSpace(title))
            return Constitution;

        var trimmed = title.Trim();
        return FloorMapByTitle.TryGetValue(trimmed, out var mapped) ? mapped : Constitution;
    }

    /// <summary>
    /// Constitution floor membership for mind-block compose.
    /// Schema <paramref name="book"/> wins when valid; otherwise locked title map only
    /// (unknown titles fail closed — never become floors).
    /// </summary>
    public static bool IsConstitutionFloor(string? title, string? book = null)
    {
        if (IsAllowed(book))
            return string.Equals(Normalize(book!), Constitution, StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(title))
            return false;

        return FloorMapByTitle.TryGetValue(title.Trim(), out var mapped)
            && string.Equals(mapped, Constitution, StringComparison.Ordinal);
    }

    /// <summary>
    /// Compass book membership for retrieve-into-prompt (PROP-20.27).
    /// Schema <paramref name="book"/> wins when valid; otherwise locked title map only.
    /// </summary>
    public static bool IsCompass(string? title, string? book = null)
    {
        if (IsAllowed(book))
            return string.Equals(Normalize(book!), Compass, StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(title))
            return false;

        return FloorMapByTitle.TryGetValue(title.Trim(), out var mapped)
            && string.Equals(mapped, Compass, StringComparison.Ordinal);
    }
}
