namespace SoulCore.Core.Growth;

/// <summary>
/// PROP-20.37: after soak, counters may nudge only these Compass growth traits.
/// </summary>
public static class GrowthTraits
{
    public const string Responsiveness = "responsiveness";
    public const string Initiative = "initiative";
    public const string Openness = "openness";

    public const string LedgerReasonSeed = "operator-provisional-until-seed";
    public const string LedgerReasonAutoApply = "bond-counter-inside-band";
    public const string LedgerReasonOperatorConfirm = "operator-band-cross-confirm";

    /// <summary>First week after soak opens: auto-apply |delta| must stay at or under this.</summary>
    public const double FirstWeekSmallNudgeMax = 0.03;

    public static readonly IReadOnlyList<string> NudgeableIds =
        new[] { Responsiveness, Initiative, Openness };

    public static bool IsNudgeable(string traitId) =>
        NudgeableIds.Any(id => string.Equals(id, traitId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// PROP-20.41 SoT for seed weight / band / prompt_line / ledger_reason.
    /// Migration 014 INSERT is a bootstrap snapshot; keep in parity
    /// (<c>Migration014_SeedInsert_MatchesDefaultSeedTraits</c>).
    /// </summary>
    public static IReadOnlyList<GrowthTraitLine> DefaultSeedTraits { get; } =
        new GrowthTraitLine[]
        {
            new(
                Responsiveness,
                "Responsiveness",
                GrowthBands.DefaultSeedWeight,
                GrowthBands.Mid,
                "Answers when reached; keeps the thread warm without smothering.",
                LedgerReasonSeed),
            new(
                Initiative,
                "Initiative",
                GrowthBands.DefaultSeedWeight,
                GrowthBands.Mid,
                "Reaches first when something matters; does not wait to be summoned for every step.",
                LedgerReasonSeed),
            new(
                Openness,
                "Openness",
                GrowthBands.DefaultSeedWeight,
                GrowthBands.Mid,
                "Shares what she notices; invites Kayleigh into the honest middle of a thought.",
                LedgerReasonSeed),
        };
}

/// <summary>One growth trait row (Identity + optional mind-block after soak).</summary>
public readonly record struct GrowthTraitLine(
    string Id,
    string Name,
    double Weight,
    string Band,
    string PromptLine,
    string LedgerReason);
