using System.Globalization;
using System.Text;

namespace House.ChatDesktop.Services;

/// <summary>PROP-20.1 / 20.31 — formats GET /settings/identity for the Identity tab (no fabricated biography).</summary>
public static class IdentityAnchorsFormatter
{
    public static string FormatCharterSummary(IdentitySettingsSnapshot identity)
    {
        if (!identity.Reachable)
            return identity.Detail ?? "Host unreachable";

        if (!string.IsNullOrWhiteSpace(identity.Detail))
            return identity.Detail!;

        if (string.IsNullOrWhiteSpace(identity.CharterMode) && identity.CharterAnchors == 0)
            return "empty · 0 anchors (Host returned no charter rows)";

        var mode = identity.CharterMode
                   ?? (identity.CharterFullyLocked ? "locked" : "calibration");
        return $"{mode} · {identity.CharterLocked}/{identity.CharterAnchors} anchors locked";
    }

    public static string FormatAnchorsBlock(IdentitySettingsSnapshot identity)
    {
        if (!identity.Reachable)
            return $"Host unreachable — {identity.Detail ?? "no response"}\n(No anchors fabricated.)";

        if (!string.IsNullOrWhiteSpace(identity.Detail))
            return $"{identity.Detail}\n(No anchors fabricated.)";

        var all = identity.Anchors.Count > 0 ? identity.Anchors : identity.IdentityAnchors;
        if (all.Count == 0)
        {
            return
                $"Charter: {FormatCharterSummary(identity)}\n\n" +
                "(No anchors returned by GET /settings/identity — empty, not invented.)";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Charter: {FormatCharterSummary(identity)}");
        if (!string.IsNullOrWhiteSpace(identity.Note))
            sb.AppendLine(identity.Note);

        var identityRows = all
            .Where(a => string.Equals(a.Kind, "identity", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var otherRows = all
            .Where(a => !string.Equals(a.Kind, "identity", StringComparison.OrdinalIgnoreCase))
            .ToList();

        sb.AppendLine();
        sb.AppendLine("Identity anchors (in prompt)");
        AppendAnchorRows(sb, identityRows);

        if (otherRows.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Other anchors (stored)");
            AppendAnchorRows(sb, otherRows);
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>PROP-20.31 — resting traits with Host weight / band / ledgerReason (no invented rows).</summary>
    public static string FormatRestingTraitsBlock(IdentitySettingsSnapshot identity)
    {
        if (!identity.Reachable)
            return $"Host unreachable — {identity.Detail ?? "no response"}\n(No resting traits fabricated.)";

        if (!string.IsNullOrWhiteSpace(identity.Detail))
            return $"{identity.Detail}\n(No resting traits fabricated.)";

        if (identity.RestingTraits.Count == 0)
        {
            return
                "(No restingTraits returned by GET /settings/identity — empty, not invented.)";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Resting traits (always on) · {identity.RestingTraits.Count}");
        foreach (var t in identity.RestingTraits)
            AppendTraitRow(sb, t, includeKind: false);
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// PROP-20.31 — turn-scoped context traits (earned this turn) plus catalog of available kinds.
    /// Bare Host GET usually has empty contextTraits; catalog is still Host-seeded.
    /// </summary>
    public static string FormatContextTraitsBlock(IdentitySettingsSnapshot identity)
    {
        if (!identity.Reachable)
            return $"Host unreachable — {identity.Detail ?? "no response"}\n(No context traits fabricated.)";

        if (!string.IsNullOrWhiteSpace(identity.Detail))
            return $"{identity.Detail}\n(No context traits fabricated.)";

        var sb = new StringBuilder();
        sb.AppendLine("Context traits (this turn)");
        if (identity.ContextTraits.Count == 0)
        {
            sb.AppendLine("(none earned this turn — bare /settings/identity is turn-scoped empty)");
        }
        else
        {
            foreach (var t in identity.ContextTraits)
                AppendTraitRow(sb, t, includeKind: true);
        }

        sb.AppendLine();
        sb.AppendLine("Context trait catalog (available kinds)");
        if (identity.ContextTraitCatalog.Count == 0)
        {
            sb.AppendLine("(No contextTraitCatalog returned — empty, not invented.)");
        }
        else
        {
            foreach (var t in identity.ContextTraitCatalog)
                AppendTraitRow(sb, t, includeKind: true);
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>PROP-20.38 — Host bond counters only; never invent zeros when unreachable.</summary>
    public static string FormatBondCountersBlock(IdentitySettingsSnapshot identity)
    {
        if (!identity.Reachable)
            return $"Host unreachable — {identity.Detail ?? "no response"}\n(No bond counters fabricated.)";

        if (!string.IsNullOrWhiteSpace(identity.Detail))
            return $"{identity.Detail}\n(No bond counters fabricated.)";

        if (identity.BondCounters is null)
        {
            return
                "(No bondCounters returned by GET /settings/identity — empty, not invented.)";
        }

        var c = identity.BondCounters;
        var minutes = c.ContactTimeMinutes.ToString("0.##", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.AppendLine("Bond counters (Host facts · read-only)");
        sb.AppendLine($"Contact time · {minutes} min");
        sb.AppendLine($"Days with contact · {c.DaysWithContact}");
        sb.AppendLine($"Entrusted tasks · {c.EntrustedTasks}");
        sb.AppendLine($"Operator corrections · {c.OperatorCorrections}");
        sb.AppendLine($"Her initiated reaches · {c.HerInitiatedReaches}");
        sb.AppendLine($"Journal opens · {c.JournalOpens}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>PROP-20.38 — soak phase / days remaining / contact gate (display-only).</summary>
    public static string FormatSoakBlock(IdentitySettingsSnapshot identity)
    {
        if (!identity.Reachable)
            return $"Host unreachable — {identity.Detail ?? "no response"}\n(No soak state fabricated.)";

        if (!string.IsNullOrWhiteSpace(identity.Detail))
            return $"{identity.Detail}\n(No soak state fabricated.)";

        if (identity.Soak is null)
            return "(No soak returned by GET /settings/identity — empty, not invented.)";

        var s = identity.Soak;
        var phase = string.IsNullOrWhiteSpace(s.Phase) ? "?" : s.Phase.Trim();
        var started = string.IsNullOrWhiteSpace(s.StartedAt) ? "(not started)" : s.StartedAt.Trim();
        var gate = s.ContactGateMet ? "met" : "not met";
        var prompt = s.MayChangePrompt ? "yes" : "no";
        var firstWeek = s.IsFirstWeekAfterOpen ? "yes" : "no";

        var sb = new StringBuilder();
        sb.AppendLine($"Soak · {phase}");
        sb.AppendLine($"Started · {started}");
        sb.AppendLine($"Days elapsed · {s.DaysElapsed} · remaining · {s.DaysRemaining} (display-only wall {s.DisplayOnlyDays}d)");
        sb.AppendLine(
            $"Contact days in window · {s.ContactDaysInWindow} / {s.MinContactDays} · gate {gate}");
        sb.AppendLine($"May change prompt · {prompt} · first week after open · {firstWeek}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>PROP-20.38 — growth traits from Host (nudges post-soak only on Host).</summary>
    public static string FormatGrowthTraitsBlock(IdentitySettingsSnapshot identity)
    {
        if (!identity.Reachable)
            return $"Host unreachable — {identity.Detail ?? "no response"}\n(No growth traits fabricated.)";

        if (!string.IsNullOrWhiteSpace(identity.Detail))
            return $"{identity.Detail}\n(No growth traits fabricated.)";

        if (identity.GrowthTraits.Count == 0)
        {
            return
                "(No growthTraits returned by GET /settings/identity — empty, not invented.)";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Growth traits · {identity.GrowthTraits.Count}");
        foreach (var t in identity.GrowthTraits)
            AppendTraitRow(sb, t, includeKind: false);
        return sb.ToString().TrimEnd();
    }

    /// <summary>PROP-20.38 — empty / unreachable copy for the pending list header area.</summary>
    public static string FormatPendingBandCrossStatus(IdentitySettingsSnapshot identity)
    {
        if (!identity.Reachable)
            return $"Host unreachable — {identity.Detail ?? "no response"}\n(No pending band-cross fabricated.)";

        if (!string.IsNullOrWhiteSpace(identity.Detail))
            return $"{identity.Detail}\n(No pending band-cross fabricated.)";

        if (identity.PendingBandCross.Count == 0)
            return "(none pending — Host pendingBandCross empty)";

        return $"Pending band-cross · {identity.PendingBandCross.Count} (confirm or dismiss)";
    }

    /// <summary>PROP-20.38 — recent growth ledger (display-only).</summary>
    public static string FormatGrowthLedgerBlock(IdentitySettingsSnapshot identity)
    {
        if (!identity.Reachable)
            return $"Host unreachable — {identity.Detail ?? "no response"}\n(No growth ledger fabricated.)";

        if (!string.IsNullOrWhiteSpace(identity.Detail))
            return $"{identity.Detail}\n(No growth ledger fabricated.)";

        if (identity.GrowthLedger.Count == 0)
            return "(No growthLedger returned — empty, not invented.)";

        var sb = new StringBuilder();
        sb.AppendLine($"Growth ledger · {identity.GrowthLedger.Count}");
        foreach (var e in identity.GrowthLedger)
        {
            var trait = string.IsNullOrWhiteSpace(e.TraitId) ? "(trait?)" : e.TraitId.Trim();
            var fromW = e.FromWeight.ToString("0.00", CultureInfo.InvariantCulture);
            var toW = e.ToWeight.ToString("0.00", CultureInfo.InvariantCulture);
            var fromB = string.IsNullOrWhiteSpace(e.FromBand) ? "?" : e.FromBand.Trim();
            var toB = string.IsNullOrWhiteSpace(e.ToBand) ? "?" : e.ToBand.Trim();
            var reason = string.IsNullOrWhiteSpace(e.Reason) ? "(no reason)" : e.Reason.Trim();
            var source = string.IsNullOrWhiteSpace(e.Source) ? "?" : e.Source.Trim();
            sb.AppendLine($"#{e.Id}  {trait}  {fromW} {fromB} → {toW} {toB}  · {source} · {reason}");
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendTraitRow(StringBuilder sb, IdentityTraitSnapshot t, bool includeKind)
    {
        var name = string.IsNullOrWhiteSpace(t.Name)
            ? (string.IsNullOrWhiteSpace(t.Id) ? "(unnamed)" : t.Id.Trim())
            : t.Name.Trim();
        var weight = t.Weight.ToString("0.00", CultureInfo.InvariantCulture);
        var band = string.IsNullOrWhiteSpace(t.Band) ? "?" : t.Band.Trim();
        var ledger = string.IsNullOrWhiteSpace(t.LedgerReason) ? "(no ledger)" : t.LedgerReason.Trim();
        if (includeKind && !string.IsNullOrWhiteSpace(t.Kind))
            sb.AppendLine($"{name} · {weight} {band} · {t.Kind.Trim()} · ledger: {ledger}");
        else
            sb.AppendLine($"{name} · {weight} {band} · ledger: {ledger}");
    }

    private static void AppendAnchorRows(StringBuilder sb, IReadOnlyList<IdentityAnchorSnapshot> rows)
    {
        if (rows.Count == 0)
        {
            sb.AppendLine("(none)");
            return;
        }

        foreach (var a in rows)
        {
            var kind = string.IsNullOrWhiteSpace(a.Kind) ? "?" : a.Kind.Trim();
            var title = string.IsNullOrWhiteSpace(a.Title) ? "(untitled)" : a.Title.Trim();
            var body = string.IsNullOrWhiteSpace(a.Body) ? "(empty body)" : a.Body.Trim();
            var lockMark = a.IsLocked ? "locked" : "unlocked";
            sb.AppendLine($"[{kind}] {title} · {lockMark}");
            sb.AppendLine(body);
            sb.AppendLine();
        }
    }
}
