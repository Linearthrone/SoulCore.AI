namespace SoulCore.Core.Growth;

/// <summary>
/// PROP-20.37 delta rules: inside-band auto-apply + ledger; band-cross → pending.
/// First week after soak: only small inside-band auto-applies.
/// </summary>
public static class GrowthDeltaEngine
{
    public enum DecisionKind
    {
        Rejected = 0,
        AutoApply = 1,
        Pending = 2,
    }

    public readonly record struct Decision(
        DecisionKind Kind,
        string TraitId,
        double FromWeight,
        double ToWeight,
        string FromBand,
        string ToBand,
        string Reason,
        bool IsBandCross);

    public static Decision Evaluate(
        string traitId,
        double currentWeight,
        double proposedWeight,
        bool soakOpen,
        bool isFirstWeekAfterOpen)
    {
        if (!soakOpen)
        {
            return Reject(
                traitId,
                currentWeight,
                proposedWeight,
                "pre_soak_display_only");
        }

        if (!GrowthTraits.IsNudgeable(traitId))
        {
            return Reject(
                traitId,
                currentWeight,
                proposedWeight,
                "trait_not_nudgeable");
        }

        var from = GrowthBands.ClampWeight(currentWeight);
        var to = GrowthBands.ClampWeight(proposedWeight);
        if (Math.Abs(to - from) < 0.0001)
        {
            return Reject(traitId, from, to, "no_delta");
        }

        var fromBand = GrowthBands.ResolveBand(from);
        var toBand = GrowthBands.ResolveBand(to);
        var cross = GrowthBands.IsBandCross(from, to);

        if (cross)
        {
            return new Decision(
                DecisionKind.Pending,
                NormalizeId(traitId),
                from,
                to,
                fromBand,
                toBand,
                "band_cross_awaits_operator",
                IsBandCross: true);
        }

        var absDelta = Math.Abs(to - from);
        if (isFirstWeekAfterOpen && absDelta > GrowthTraits.FirstWeekSmallNudgeMax)
        {
            // First week: clip to small inside-band auto-apply (law: only small nudges).
            var clipped = from + Math.Sign(to - from) * GrowthTraits.FirstWeekSmallNudgeMax;
            clipped = GrowthBands.ClampWeight(clipped);
            if (GrowthBands.IsBandCross(from, clipped))
            {
                return new Decision(
                    DecisionKind.Pending,
                    NormalizeId(traitId),
                    from,
                    to,
                    fromBand,
                    toBand,
                    "first_week_band_edge_pending",
                    IsBandCross: true);
            }

            return new Decision(
                DecisionKind.AutoApply,
                NormalizeId(traitId),
                from,
                clipped,
                fromBand,
                GrowthBands.ResolveBand(clipped),
                "first_week_small_inside_band",
                IsBandCross: false);
        }

        return new Decision(
            DecisionKind.AutoApply,
            NormalizeId(traitId),
            from,
            to,
            fromBand,
            toBand,
            GrowthTraits.LedgerReasonAutoApply,
            IsBandCross: false);
    }

    /// <summary>
    /// Operator confirm of a pending band-cross: always applies (ledger operator-confirm).
    /// </summary>
    public static Decision ConfirmPending(
        string traitId,
        double currentWeight,
        double proposedWeight)
    {
        var from = GrowthBands.ClampWeight(currentWeight);
        var to = GrowthBands.ClampWeight(proposedWeight);
        return new Decision(
            DecisionKind.AutoApply,
            NormalizeId(traitId),
            from,
            to,
            GrowthBands.ResolveBand(from),
            GrowthBands.ResolveBand(to),
            GrowthTraits.LedgerReasonOperatorConfirm,
            IsBandCross: GrowthBands.IsBandCross(from, to));
    }

    private static Decision Reject(
        string traitId,
        double from,
        double to,
        string reason) =>
        new(
            DecisionKind.Rejected,
            NormalizeId(traitId),
            GrowthBands.ClampWeight(from),
            GrowthBands.ClampWeight(to),
            GrowthBands.ResolveBand(from),
            GrowthBands.ResolveBand(to),
            reason,
            IsBandCross: GrowthBands.IsBandCross(from, to));

    private static string NormalizeId(string traitId) =>
        traitId.Trim().ToLowerInvariant();
}
