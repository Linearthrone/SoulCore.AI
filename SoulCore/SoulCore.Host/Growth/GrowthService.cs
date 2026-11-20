using Microsoft.Extensions.Logging;
using SoulCore.Core.Growth;
using SoulCore.Memory;

namespace SoulCore.Host.Growth;

/// <summary>PROP-20.37: bond counters + soak gate + inside-band / band-cross delta rules.</summary>
public sealed class GrowthService : IGrowthService
{
    private readonly IGrowthStore _store;
    private readonly ILogger<GrowthService> _logger;
    private readonly object _nudgeGate = new();
    private DateTimeOffset? _lastNudgeDay;

    public GrowthService(IGrowthStore store, ILogger<GrowthService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GrowthIdentitySnapshot> GetIdentitySnapshotAsync(
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        var now = utcNow ?? DateTimeOffset.UtcNow;
        var started = await _store.EnsureSoakStartedAsync(now, cancellationToken).ConfigureAwait(false);

        // Display counters through now; gate/soak via shared helper (window-capped).
        var counters = await _store
            .ComputeBondCountersAsync(started, now, cancellationToken)
            .ConfigureAwait(false);
        var (soak, _) = await ResolveSoakAsync(started, now, cancellationToken).ConfigureAwait(false);

        var traits = await _store.ListTraitWeightsAsync(cancellationToken).ConfigureAwait(false);
        var pending = await _store.ListPendingAsync(cancellationToken).ConfigureAwait(false);
        var ledger = await _store.ListLedgerAsync(20, cancellationToken).ConfigureAwait(false);

        if (soak.MayChangePrompt)
        {
            try
            {
                await EvaluateCounterNudgesAsync(now, cancellationToken).ConfigureAwait(false);
                traits = await _store.ListTraitWeightsAsync(cancellationToken).ConfigureAwait(false);
                pending = await _store.ListPendingAsync(cancellationToken).ConfigureAwait(false);
                ledger = await _store.ListLedgerAsync(20, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Growth counter nudge evaluate failed (Identity still returns)");
            }
        }

        return new GrowthIdentitySnapshot(counters, soak, traits, pending, ledger);
    }

    public async Task<IReadOnlyList<GrowthTraitLine>?> GetPromptGrowthTraitsAsync(
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        var now = utcNow ?? DateTimeOffset.UtcNow;
        var started = await _store.GetSoakStartedAtAsync(cancellationToken).ConfigureAwait(false);
        if (started is null)
            return null;

        var (soak, _) = await ResolveSoakAsync(started.Value, now, cancellationToken).ConfigureAwait(false);
        if (!soak.MayChangePrompt)
            return null;

        return await _store.ListTraitWeightsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<GrowthDeltaEngine.Decision> ApplyProposedDeltaAsync(
        string traitId,
        double proposedWeight,
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        var now = utcNow ?? DateTimeOffset.UtcNow;
        var started = await _store.EnsureSoakStartedAsync(now, cancellationToken).ConfigureAwait(false);
        var (soak, _) = await ResolveSoakAsync(started, now, cancellationToken).ConfigureAwait(false);

        var traits = await _store.ListTraitWeightsAsync(cancellationToken).ConfigureAwait(false);
        var current = traits.FirstOrDefault(t =>
            string.Equals(t.Id, traitId, StringComparison.OrdinalIgnoreCase));
        var currentWeight = string.IsNullOrEmpty(current.Id)
            ? GrowthBands.DefaultSeedWeight
            : current.Weight;

        var decision = GrowthDeltaEngine.Evaluate(
            traitId,
            currentWeight,
            proposedWeight,
            soak.MayChangePrompt,
            soak.IsFirstWeekAfterOpen);
        if (decision.Kind == GrowthDeltaEngine.DecisionKind.AutoApply)
        {
            await _store.SetTraitWeightAsync(
                    decision.TraitId,
                    decision.ToWeight,
                    decision.ToBand,
                    decision.Reason,
                    cancellationToken)
                .ConfigureAwait(false);
            await _store.AppendLedgerAsync(
                    decision.TraitId,
                    decision.FromWeight,
                    decision.ToWeight,
                    decision.FromBand,
                    decision.ToBand,
                    decision.Reason,
                    "auto",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (decision.Kind == GrowthDeltaEngine.DecisionKind.Pending)
        {
            await _store.CreatePendingAsync(
                    decision.TraitId,
                    decision.FromWeight,
                    decision.ToWeight,
                    decision.FromBand,
                    decision.ToBand,
                    decision.Reason,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return decision;
    }

    public async Task<bool> ConfirmPendingAsync(long proposalId, CancellationToken cancellationToken = default)
    {
        var pending = await _store.GetPendingAsync(proposalId, cancellationToken).ConfigureAwait(false);
        if (pending is null || pending.Value.Status != "pending")
            return false;

        var p = pending.Value;
        var confirmed = GrowthDeltaEngine.ConfirmPending(p.TraitId, p.FromWeight, p.ToWeight);
        await _store.SetTraitWeightAsync(
                confirmed.TraitId,
                confirmed.ToWeight,
                confirmed.ToBand,
                confirmed.Reason,
                cancellationToken)
            .ConfigureAwait(false);
        await _store.AppendLedgerAsync(
                confirmed.TraitId,
                confirmed.FromWeight,
                confirmed.ToWeight,
                confirmed.FromBand,
                confirmed.ToBand,
                confirmed.Reason,
                "operator_confirm",
                cancellationToken)
            .ConfigureAwait(false);
        return await _store.ResolvePendingAsync(proposalId, "confirmed", cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DismissPendingAsync(long proposalId, CancellationToken cancellationToken = default) =>
        await _store.ResolvePendingAsync(proposalId, "dismissed", cancellationToken).ConfigureAwait(false);

    public async Task EvaluateCounterNudgesAsync(
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        var now = utcNow ?? DateTimeOffset.UtcNow;
        lock (_nudgeGate)
        {
            var day = now.UtcDateTime.Date;
            if (_lastNudgeDay == day)
                return;
            _lastNudgeDay = day;
        }

        var started = await _store.EnsureSoakStartedAsync(now, cancellationToken).ConfigureAwait(false);
        var (soak, _) = await ResolveSoakAsync(started, now, cancellationToken).ConfigureAwait(false);
        if (!soak.MayChangePrompt)
            return;

        var counters = await _store
            .ComputeBondCountersAsync(started, now, cancellationToken)
            .ConfigureAwait(false);
        var traits = await _store.ListTraitWeightsAsync(cancellationToken).ConfigureAwait(false);
        var byId = traits.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
        // Small heuristic nudges from counters — only the three growth traits.
        // First week: |delta| capped by GrowthDeltaEngine to FirstWeekSmallNudgeMax.
        await ProposeIfNeededAsync(
                GrowthTraits.Responsiveness,
                byId,
                counters.DaysWithContact >= 3 && counters.ContactTimeMinutes >= 30
                    ? 0.02
                    : counters.DaysWithContact < 2 ? -0.01 : 0,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        await ProposeIfNeededAsync(
                GrowthTraits.Initiative,
                byId,
                counters.HerInitiatedReaches >= 3
                    ? 0.02
                    : counters.HerInitiatedReaches == 0 && counters.DaysWithContact >= 5 ? -0.01 : 0,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        await ProposeIfNeededAsync(
                GrowthTraits.Openness,
                byId,
                counters.JournalOpens >= 2
                    ? 0.02
                    : counters.OperatorCorrections >= 3 ? -0.01 : 0,
                now,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ProposeIfNeededAsync(
        string traitId,
        IReadOnlyDictionary<string, GrowthTraitLine> map,
        double delta,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (Math.Abs(delta) < 0.0001)
            return;
        if (!map.TryGetValue(traitId, out var row))
            return;
        await ApplyProposedDeltaAsync(traitId, row.Weight + delta, now, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Shared soak/gate path: counters capped to the 14-day wall window, then
    /// <see cref="GrowthSoakClock.Evaluate"/>. Identity still computes display-to-now
    /// counters separately outside this helper.
    /// </summary>
    private async Task<(GrowthSoakClock.SoakSnapshot Soak, BondCounterSnapshot GateCounters)> ResolveSoakAsync(
        DateTimeOffset started,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var soakWindowEnd = started.Add(GrowthSoakClock.DisplayOnlyDuration);
        var gateEnd = now < soakWindowEnd ? now : soakWindowEnd;
        var gateCounters = await _store
            .ComputeBondCountersAsync(started, gateEnd, cancellationToken)
            .ConfigureAwait(false);
        var soak = GrowthSoakClock.Evaluate(started, now, gateCounters.DaysWithContact);
        return (soak, gateCounters);
    }
}
