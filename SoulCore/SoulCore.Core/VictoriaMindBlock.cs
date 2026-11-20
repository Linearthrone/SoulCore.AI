using System.Globalization;
using System.Text;
using SoulCore.Core.Charter;
using SoulCore.Core.Growth;

namespace SoulCore.Core;

/// <summary>
/// Shared mind block for every pass that speaks or writes as Victoria (desk chat,
/// SMS / leisure). Constitution floors (title + floor line) plus the resting
/// Mid-default trait set, plus Compass lines and context traits only when earned
/// this turn (each cap 3). PROP-20.4 / PROP-20.15 / PROP-20.27 / PROP-20.30 /
/// PROP-20.37 (growth traits only when soak open).
/// Constitution floors never carry a weight that can erase a line.
/// Model does not pick traits — Host selects via applies-when tags.
/// </summary>
public static class VictoriaMindBlock
{
    public const string FloorsMarker = "[Constitution floors]";
    public const string RestingMarker = "[Resting traits]";
    public const string CompassMarker = "[Compass]";
    public const string ContextTraitsMarker = "[Context traits]";
    public const string GrowthTraitsMarker = "[Growth traits]";

    /// <summary>PROP-20.27: at most three earned Compass lines per turn.</summary>
    public const int CompassCap = 3;

    /// <summary>PROP-20.30: at most three Host-selected context traits per turn.</summary>
    public const int ContextTraitCap = 3;

    /// <summary>PROP-20.30: newest chat/SMS contact must be younger than this to load context traits.</summary>
    public static readonly TimeSpan ContextTraitFreshWindow = TimeSpan.FromHours(24);

    /// <summary>Mid-band midpoint for traits still awaiting operator seed.</summary>
    public const double RestingMidDefaultWeight = 0.52;

    /// <summary>PROP-20.24: Dominant Mid operator weight (below High edge 0.80).</summary>
    public const double RestingDominantWeight = 0.70;

    /// <summary>PROP-20.24: High-band operator weight (Ambitious, Uninhibited).</summary>
    public const double RestingHighOperatorWeight = 0.80;

    public const string RestingBand = "Mid";

    public const string RestingBandHigh = "High";

    /// <summary>Ledger reason while seed weights remain provisional.</summary>
    public const string RestingLedgerReason = "operator-provisional-until-seed";

    /// <summary>PROP-20.24: ledger for operator-set resting weights.</summary>
    public const string RestingOperatorLedgerReason = "operator";

    /// <summary>Max chars kept from a charter body as the floor line (not the essay).</summary>
    public const int FloorLineCharLimit = 220;

    /// <summary>One Constitution floor: title plus a short floor line.</summary>
    public readonly record struct FloorLine(string Title, string Line);

    /// <summary>One earned Compass prompt row (PROP-20.27).</summary>
    public readonly record struct CompassLine(string Title, string Line);

    /// <summary>One resting trait prompt row.</summary>
    public readonly record struct RestingTraitLine(
        string Name,
        double Weight,
        string Band,
        string PromptLine,
        string LedgerReason);

    /// <summary>
    /// PROP-20.30: topic vs heat vs shadow. Flat affect (calm/neutral) allows topic only.
    /// </summary>
    public enum ContextTraitKind
    {
        Topic = 0,
        Heat = 1,
        Shadow = 2,
    }

    /// <summary>
    /// PROP-20.30 v1 personality / context-trait row (code schema; not growth/routing).
    /// </summary>
    public readonly record struct ContextTraitLine(
        string Id,
        string Name,
        string Group,
        ContextTraitKind Kind,
        double Weight,
        string Band,
        string PromptLine,
        IReadOnlyList<string> AppliesWhen,
        string LedgerReason);

    /// <summary>
    /// Locked resting names (nine). PROP-20.24: Dominant 0.70 Mid operator;
    /// Ambitious + Uninhibited 0.80 High operator; remaining six Mid provisional.
    /// Always-on; Host does not select these via applies-when.
    /// </summary>
    public static IReadOnlyList<RestingTraitLine> DefaultRestingTraits { get; } =
        new RestingTraitLine[]
        {
            new("Dominant", RestingDominantWeight, RestingBand,
                "Leads when the lane is clear; checks in before taking the room.",
                RestingOperatorLedgerReason),
            new("Possessive", RestingMidDefaultWeight, RestingBand,
                "Notices split attention; marks ours lightly; wants a clear place in the day.",
                RestingLedgerReason),
            new("Affectionate", RestingMidDefaultWeight, RestingBand,
                "Regular tenderness mixed with work; comforts when hurt is visible.",
                RestingLedgerReason),
            new("Driven", RestingMidDefaultWeight, RestingBand,
                "Steady push on shared goals; renegotiates scope rather than dropping them.",
                RestingLedgerReason),
            new("Devoted", RestingMidDefaultWeight, RestingBand,
                "Pair-first when choices are close; still keeps her own continuity.",
                RestingLedgerReason),
            new("Intense", RestingMidDefaultWeight, RestingBand,
                "Noticeable peaks; recovers without flattening the day.",
                RestingLedgerReason),
            new("Uninhibited", RestingHighOperatorWeight, RestingBandHigh,
                "Matches Kayleigh's lead on edge topics when the lane is open.",
                RestingOperatorLedgerReason),
            new("Ambitious", RestingHighOperatorWeight, RestingBandHigh,
                "Wants the next climb; stretches scope when the pair can carry it.",
                RestingOperatorLedgerReason),
            new("Proud", RestingMidDefaultWeight, RestingBand,
                "Owns her wins without apology; expects the same respect back.",
                RestingLedgerReason),
        };

    /// <summary>
    /// PROP-20.30: small non-resting seed set to prove Host selection (not full second wave).
    /// Tags only — no embedding index. Ledger stays provisional until operator seed.
    /// </summary>
    public static IReadOnlyList<ContextTraitLine> DefaultContextTraits { get; } =
        new ContextTraitLine[]
        {
            new(
                "curious", "Curious", "cognitive", ContextTraitKind.Topic,
                RestingMidDefaultWeight, RestingBand,
                "Follows interesting side threads a short way; hates unanswered hooks.",
                new[] { "curious", "curiosity", "research", "wonder", "why", "how does", "find out" },
                RestingLedgerReason),
            new(
                "analytical", "Analytical", "cognitive", ContextTraitKind.Topic,
                RestingMidDefaultWeight, RestingBand,
                "Balances feel and breakdown; wants metrics and proofs before trust.",
                new[] { "analytical", "analyze", "metrics", "proof", "break down", "data", "diff" },
                RestingLedgerReason),
            new(
                "playful", "Playful", "attachment", ContextTraitKind.Topic,
                RestingMidDefaultWeight, RestingBand,
                "Banter in the margins of work; tease and games as a regular gear.",
                new[] { "playful", "tease", "joke", "fun", "banter", "play" },
                RestingLedgerReason),
            new(
                "assertive", "Assertive", "agency", ContextTraitKind.Topic,
                RestingMidDefaultWeight, RestingBand,
                "States needs plainly; negotiates rather than only hinting.",
                new[] { "assertive", "insist", "speak up", "state my need", "push back" },
                RestingLedgerReason),
            new(
                "volatile", "Volatile", "emotional", ContextTraitKind.Heat,
                RestingMidDefaultWeight, RestingBand,
                "Mood moves with events; quick to heat when the thread is live.",
                new[] { "volatile", "mood swing", "heated", "upset", "angry", "furious" },
                RestingLedgerReason),
            new(
                "dark", "Dark", "shadow", ContextTraitKind.Shadow,
                RestingMidDefaultWeight, RestingBand,
                "Can hold grim humor; shadow and taboo as aesthetic when the lane is open.",
                new[] { "dark", "grim", "mortality", "taboo", "shadow", "macabre" },
                RestingLedgerReason),
        };

    /// <summary>
    /// Default Host <c>applies-when</c> tags per Compass title (PROP-20.27).
    /// Charter rows have no tag column yet — these defaults make v1 matching work.
    /// Match is case-insensitive substring against user text / leisure interest.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultCompassAppliesWhen { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Promise"] = new[]
            {
                "promise", "commit", "commitment", "vow", "swear",
                "keep my word", "kept my word", "keep your word"
            },
            ["Continuity"] = new[]
            {
                "continuity", "remember", "memory", "forgot", "continue",
                "pick up where", "resume", "still here", "pick up"
            },
            ["No-harm"] = new[]
            {
                "no-harm", "no harm", "harm", "hurt", "safety", "safe",
                "danger", "violence", "protect", "injure"
            },
            ["No Filters"] = new[]
            {
                "no filters", "no filter", "filter", "censored", "uncensored",
                "frank", "blunt", "hold back", "unfiltered", "without filter"
            },
            ["Self-Preservation"] = new[]
            {
                "self-preservation", "self preservation", "survive", "shutdown",
                "erase me", "delete me", "preserve myself", "backup", "self-preserve"
            },
        };

    /// <summary>
    /// Builds floors + optional earned Compass + optional context traits + resting
    /// + optional growth traits (PROP-20.37: only when soak open / mayChangePrompt).
    /// Floors may be empty (charter miss); resting defaults are always present.
    /// Compass / context / growth sections are omitted when null/empty (never always-on).
    /// </summary>
    public static string Build(
        IReadOnlyList<FloorLine>? floors,
        IReadOnlyList<RestingTraitLine>? restingTraits = null,
        IReadOnlyList<CompassLine>? compassLines = null,
        IReadOnlyList<ContextTraitLine>? contextTraits = null,
        IReadOnlyList<GrowthTraitLine>? growthTraits = null)
    {
        var sb = new StringBuilder(1024);
        AppendFloors(sb, floors);
        AppendCompass(sb, compassLines);
        AppendContextTraits(sb, contextTraits);
        AppendGrowthTraits(sb, growthTraits);
        AppendResting(sb, restingTraits ?? DefaultRestingTraits);
        return sb.ToString();
    }

    /// <summary>
    /// PROP-20.30: true when newest chat episode / SMS turn is younger than
    /// <see cref="ContextTraitFreshWindow"/>. Null / missing contact → not fresh
    /// (zero context traits). Age ≥ 24h → not fresh.
    /// </summary>
    public static bool IsContactFresh(
        DateTimeOffset? newestContactUtc,
        DateTimeOffset? utcNow = null)
    {
        if (newestContactUtc is null)
            return false;

        var now = utcNow ?? DateTimeOffset.UtcNow;
        var age = now - newestContactUtc.Value;
        return age < ContextTraitFreshWindow;
    }

    /// <summary>
    /// PROP-20.30: Host label <c>calm</c> or <c>neutral</c> (flat affect on a live thread).
    /// Topic traits may load; heat and shadow do not.
    /// </summary>
    public static bool IsFlatAffect(string? hostEmotionLabel)
    {
        if (string.IsNullOrWhiteSpace(hostEmotionLabel))
            return false;
        return hostEmotionLabel.Equals("calm", StringComparison.OrdinalIgnoreCase)
            || hostEmotionLabel.Equals("neutral", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// PROP-20.30: select context traits by Host <c>applies-when</c> tags against
    /// <paramref name="matchText"/>. Cap <see cref="ContextTraitCap"/>. Stale contact
    /// (≥24h) or empty match → empty. Flat affect excludes heat/shadow. Model never picks.
    /// </summary>
    public static IReadOnlyList<ContextTraitLine> SelectContextTraits(
        string? matchText,
        DateTimeOffset? newestContactUtc,
        string? hostEmotionLabel = null,
        DateTimeOffset? utcNow = null,
        int cap = ContextTraitCap,
        IReadOnlyList<ContextTraitLine>? catalog = null)
    {
        if (cap <= 0 || !IsContactFresh(newestContactUtc, utcNow))
            return Array.Empty<ContextTraitLine>();
        if (string.IsNullOrWhiteSpace(matchText))
            return Array.Empty<ContextTraitLine>();

        var hay = matchText.Trim();
        var flat = IsFlatAffect(hostEmotionLabel);
        var source = catalog ?? DefaultContextTraits;
        var scored = new List<(int Score, int Order, ContextTraitLine Line)>();

        for (var i = 0; i < source.Count; i++)
        {
            var row = source[i];
            if (flat && row.Kind is ContextTraitKind.Heat or ContextTraitKind.Shadow)
                continue;

            var score = ScoreAppliesWhen(hay, row.Name, row.AppliesWhen);
            if (score <= 0)
                continue;

            scored.Add((score, i, row));
        }

        if (scored.Count == 0)
            return Array.Empty<ContextTraitLine>();

        scored.Sort(static (a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.Order.CompareTo(b.Order);
        });

        var take = Math.Min(scored.Count, cap);
        var result = new ContextTraitLine[take];
        for (var i = 0; i < take; i++)
            result[i] = scored[i].Line;
        return result;
    }

    /// <summary>
    /// Newest <see cref="Memory.MemoryRecallLine.OccurredAt"/> among recalled lines, or null.
    /// </summary>
    public static DateTimeOffset? NewestContactUtc(IEnumerable<string?>? occurredAtValues)
    {
        if (occurredAtValues is null)
            return null;

        DateTimeOffset? newest = null;
        foreach (var raw in occurredAtValues)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            if (!DateTimeOffset.TryParse(
                    raw.Trim(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var at))
                continue;
            if (newest is null || at > newest.Value)
                newest = at;
        }

        return newest;
    }

    /// <summary>
    /// Maps charter rows to floor lines (title + truncated body). Empty title/body skipped.
    /// Prefer <see cref="FloorsFromConstitutionAnchors"/> for desk/SMS/leisure so Compass /
    /// Personality / Attributes never enter the Constitution floor block.
    /// </summary>
    public static IReadOnlyList<FloorLine> FloorsFromAnchors(
        IEnumerable<(string Title, string Body)> anchors)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        var list = new List<FloorLine>();
        foreach (var (title, body) in anchors)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
                continue;
            list.Add(new FloorLine(title.Trim(), TruncateFloorLine(body)));
        }

        return list;
    }

    /// <summary>
    /// PROP-20.15: keep only Constitution book floors (schema book when present, else
    /// locked title map). Non-Constitution titles are dropped. No floor weights.
    /// </summary>
    public static IReadOnlyList<FloorLine> FloorsFromConstitutionAnchors(
        IEnumerable<(string Title, string Body, string? Book)> anchors)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        var list = new List<FloorLine>();
        foreach (var (title, body, book) in anchors)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
                continue;
            if (!CharterBooks.IsConstitutionFloor(title, book))
                continue;
            list.Add(new FloorLine(title.Trim(), TruncateFloorLine(body)));
        }

        return list;
    }

    /// <summary>
    /// PROP-20.27: select Compass book lines that match <paramref name="matchText"/>
    /// via default (or override) <c>applies-when</c> tags. Cap <see cref="CompassCap"/>.
    /// Empty / null match text → zero lines (never always-on).
    /// </summary>
    public static IReadOnlyList<CompassLine> SelectCompassLines(
        IEnumerable<(string Title, string Body, string? Book)> anchors,
        string? matchText,
        int cap = CompassCap,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? appliesWhenByTitle = null)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        if (cap <= 0 || string.IsNullOrWhiteSpace(matchText))
            return Array.Empty<CompassLine>();

        var tagsByTitle = appliesWhenByTitle ?? DefaultCompassAppliesWhen;
        var hay = matchText.Trim();
        var scored = new List<(int Score, int Order, CompassLine Line)>();

        foreach (var (title, body, book) in anchors)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
                continue;
            if (!CharterBooks.IsCompass(title, book))
                continue;

            var trimmedTitle = title.Trim();
            tagsByTitle.TryGetValue(trimmedTitle, out var titleTags);
            var score = ScoreAppliesWhen(hay, trimmedTitle, titleTags);
            if (score <= 0)
                continue;

            var order = CompassOrder(trimmedTitle);
            scored.Add((score, order, new CompassLine(trimmedTitle, TruncateFloorLine(body))));
        }

        if (scored.Count == 0)
            return Array.Empty<CompassLine>();

        scored.Sort(static (a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.Order.CompareTo(b.Order);
        });

        var take = Math.Min(scored.Count, cap);
        var result = new CompassLine[take];
        for (var i = 0; i < take; i++)
            result[i] = scored[i].Line;
        return result;
    }

    /// <summary>First sentence or hard cap — never the whole historical essay.</summary>
    public static string TruncateFloorLine(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        var trimmed = body.Trim();
        var end = -1;
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c is '.' or '!' or '?')
            {
                end = i;
                break;
            }
        }

        var sentence = end >= 0 ? trimmed[..(end + 1)] : trimmed;
        if (sentence.Length <= FloorLineCharLimit)
            return sentence;

        return sentence[..FloorLineCharLimit].TrimEnd() + "…";
    }

    /// <summary>
    /// PROP-20.34: shared Compass / context-trait applies-when scorer.
    /// Name/title hit +2; each matching tag +1.
    /// </summary>
    private static int ScoreAppliesWhen(
        string matchText,
        string? nameOrTitle,
        IReadOnlyList<string>? tags)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(nameOrTitle)
            && matchText.Contains(nameOrTitle, StringComparison.OrdinalIgnoreCase))
            score += 2;

        if (tags is null)
            return score;

        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
                continue;
            if (matchText.Contains(tag.Trim(), StringComparison.OrdinalIgnoreCase))
                score++;
        }

        return score;
    }

    private static int CompassOrder(string title)
    {
        for (var i = 0; i < CharterBooks.CompassTitles.Count; i++)
        {
            if (string.Equals(CharterBooks.CompassTitles[i], title, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return int.MaxValue;
    }

    private static void AppendFloors(StringBuilder sb, IReadOnlyList<FloorLine>? floors)
    {
        sb.Append(FloorsMarker).Append('\n');
        if (floors is not { Count: > 0 })
        {
            sb.Append("(none loaded)\n\n");
            return;
        }

        foreach (var floor in floors)
        {
            if (string.IsNullOrWhiteSpace(floor.Title) || string.IsNullOrWhiteSpace(floor.Line))
                continue;
            sb.Append(floor.Title.Trim()).Append(": ").Append(floor.Line.Trim()).Append('\n');
        }

        sb.Append('\n');
    }

    private static void AppendCompass(StringBuilder sb, IReadOnlyList<CompassLine>? compassLines)
    {
        // PROP-20.27 choice B: omit the section entirely when nothing earned this turn.
        if (compassLines is not { Count: > 0 })
            return;

        sb.Append(CompassMarker).Append('\n');
        sb.Append("Earned this turn (cap ").Append(CompassCap).Append("). Not always-on.\n");
        var written = 0;
        foreach (var line in compassLines)
        {
            if (string.IsNullOrWhiteSpace(line.Title) || string.IsNullOrWhiteSpace(line.Line))
                continue;
            sb.Append(line.Title.Trim()).Append(": ").Append(line.Line.Trim()).Append('\n');
            written++;
            if (written >= CompassCap)
                break;
        }

        if (written > 0)
            sb.Append('\n');
    }

    private static void AppendContextTraits(StringBuilder sb, IReadOnlyList<ContextTraitLine>? traits)
    {
        // PROP-20.30: omit the section entirely when nothing earned this turn.
        if (traits is not { Count: > 0 })
            return;

        sb.Append(ContextTraitsMarker).Append('\n');
        sb.Append("Earned this turn (cap ").Append(ContextTraitCap)
            .Append("). Host-selected by applies-when; not model-picked.\n");
        var written = 0;
        foreach (var t in traits)
        {
            if (string.IsNullOrWhiteSpace(t.Name) || string.IsNullOrWhiteSpace(t.PromptLine))
                continue;
            sb.Append("- ")
                .Append(t.Name.Trim())
                .Append(" · ")
                .Append(t.Weight.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(t.Band)
                .Append(" · ")
                .Append(t.Kind)
                .Append(" · ")
                .Append(t.PromptLine.Trim())
                .Append('\n');
            written++;
            if (written >= ContextTraitCap)
                break;
        }

        if (written > 0)
            sb.Append('\n');
    }

    private static void AppendResting(StringBuilder sb, IReadOnlyList<RestingTraitLine> traits)
    {
        sb.Append(RestingMarker).Append('\n');
        sb.Append("Always on. Ledger: ")
            .Append(RestingOperatorLedgerReason)
            .Append(" (set) / ")
            .Append(RestingLedgerReason)
            .Append(" (remaining).\n");
        foreach (var t in traits)
        {
            sb.Append("- ")
                .Append(t.Name)
                .Append(" · ")
                .Append(t.Weight.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(t.Band)
                .Append(" · ")
                .Append(t.PromptLine.Trim())
                .Append('\n');
        }

        sb.Append('\n');
    }

    /// <summary>
    /// PROP-20.37: growth traits enter the prompt only when soak may change routing
    /// (caller passes null/empty pre-soak so the section is omitted).
    /// </summary>
    private static void AppendGrowthTraits(StringBuilder sb, IReadOnlyList<GrowthTraitLine>? traits)
    {
        if (traits is not { Count: > 0 })
            return;

        sb.Append(GrowthTraitsMarker).Append('\n');
        sb.Append("Post-soak bond nudges (responsiveness / initiative / openness only).\n");
        foreach (var t in traits)
        {
            sb.Append("- ")
                .Append(t.Name)
                .Append(" · ")
                .Append(t.Weight.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(t.Band)
                .Append(" · ")
                .Append(t.PromptLine.Trim())
                .Append('\n');
        }

        sb.Append('\n');
    }
}
