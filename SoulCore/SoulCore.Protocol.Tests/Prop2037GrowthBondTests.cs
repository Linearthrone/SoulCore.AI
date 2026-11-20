using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using SoulCore.Core;
using SoulCore.Core.Growth;
using SoulCore.Host.Growth;
using SoulCore.Memory;
using SoulCore.Memory.Repositories;

namespace SoulCore.Protocol.Tests;

/// <summary>PROP-20.37: bond counters, soak gate, inside-band auto-apply, band-cross pending.</summary>
public sealed class Prop2037GrowthBondTests
{
    [Fact]
    public void GrowthBands_ResolvePreferredRanges()
    {
        Assert.Equal(GrowthBands.Low, GrowthBands.ResolveBand(0.20));
        Assert.Equal(GrowthBands.Mid, GrowthBands.ResolveBand(0.52));
        Assert.Equal(GrowthBands.High, GrowthBands.ResolveBand(0.85));
        Assert.True(GrowthBands.IsInsideBand(0.50, 0.55));
        Assert.True(GrowthBands.IsBandCross(0.55, 0.82));
    }

    [Fact]
    public void SoakClock_DisplayOnly_BeforeFourteenDays()
    {
        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var day7 = start.AddDays(7);
        var snap = GrowthSoakClock.Evaluate(start, day7, contactDaysInWindow: 7);
        Assert.Equal(GrowthSoakClock.SoakPhase.DisplayOnly, snap.Phase);
        Assert.False(snap.MayChangePrompt);
        Assert.True(snap.DaysRemaining > 0);
    }

    [Fact]
    public void SoakClock_GateClosed_WhenFewerThanThreeContactDays()
    {
        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var day15 = start.AddDays(15);
        var snap = GrowthSoakClock.Evaluate(start, day15, contactDaysInWindow: 2);
        Assert.Equal(GrowthSoakClock.SoakPhase.GateClosed, snap.Phase);
        Assert.False(snap.MayChangePrompt);
        Assert.False(snap.ContactGateMet);
    }

    [Fact]
    public void SoakClock_Opens_AfterFourteenDays_WithThreeContactDays()
    {
        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var day15 = start.AddDays(15);
        var snap = GrowthSoakClock.Evaluate(start, day15, contactDaysInWindow: 3);
        Assert.Equal(GrowthSoakClock.SoakPhase.Open, snap.Phase);
        Assert.True(snap.MayChangePrompt);
        Assert.True(snap.IsFirstWeekAfterOpen);
    }

    [Fact]
    public void DeltaEngine_PreSoak_Rejects()
    {
        var d = GrowthDeltaEngine.Evaluate(
            GrowthTraits.Responsiveness,
            0.52,
            0.54,
            soakOpen: false,
            isFirstWeekAfterOpen: false);
        Assert.Equal(GrowthDeltaEngine.DecisionKind.Rejected, d.Kind);
        Assert.Equal("pre_soak_display_only", d.Reason);
    }

    [Fact]
    public void DeltaEngine_InsideBand_AutoApplies()
    {
        var d = GrowthDeltaEngine.Evaluate(
            GrowthTraits.Initiative,
            0.52,
            0.55,
            soakOpen: true,
            isFirstWeekAfterOpen: false);
        Assert.Equal(GrowthDeltaEngine.DecisionKind.AutoApply, d.Kind);
        Assert.False(d.IsBandCross);
        Assert.Equal(0.55, d.ToWeight);
    }

    [Fact]
    public void DeltaEngine_BandCross_StaysPending()
    {
        var d = GrowthDeltaEngine.Evaluate(
            GrowthTraits.Openness,
            0.55,
            0.85,
            soakOpen: true,
            isFirstWeekAfterOpen: false);
        Assert.Equal(GrowthDeltaEngine.DecisionKind.Pending, d.Kind);
        Assert.True(d.IsBandCross);
        Assert.Equal("band_cross_awaits_operator", d.Reason);
    }

    [Fact]
    public void DeltaEngine_FirstWeek_ClipsToSmallNudge()
    {
        var d = GrowthDeltaEngine.Evaluate(
            GrowthTraits.Responsiveness,
            0.52,
            0.58,
            soakOpen: true,
            isFirstWeekAfterOpen: true);
        Assert.Equal(GrowthDeltaEngine.DecisionKind.AutoApply, d.Kind);
        Assert.Equal(0.52 + GrowthTraits.FirstWeekSmallNudgeMax, d.ToWeight, 3);
        Assert.Equal("first_week_small_inside_band", d.Reason);
    }

    [Fact]
    public void DeltaEngine_Rejects_NonNudgeableTrait()
    {
        var d = GrowthDeltaEngine.Evaluate(
            "dominant",
            0.70,
            0.72,
            soakOpen: true,
            isFirstWeekAfterOpen: false);
        Assert.Equal(GrowthDeltaEngine.DecisionKind.Rejected, d.Kind);
        Assert.Equal("trait_not_nudgeable", d.Reason);
    }

    [Fact]
    public void MindBlock_OmitsGrowth_WhenNull_IncludesWhenProvided()
    {
        var without = VictoriaMindBlock.Build(Array.Empty<VictoriaMindBlock.FloorLine>());
        Assert.DoesNotContain(VictoriaMindBlock.GrowthTraitsMarker, without, StringComparison.Ordinal);

        var with = VictoriaMindBlock.Build(
            Array.Empty<VictoriaMindBlock.FloorLine>(),
            growthTraits: GrowthTraits.DefaultSeedTraits);
        Assert.Contains(VictoriaMindBlock.GrowthTraitsMarker, with, StringComparison.Ordinal);
        Assert.Contains("Responsiveness", with, StringComparison.Ordinal);
        Assert.Contains("Initiative", with, StringComparison.Ordinal);
        Assert.Contains("Openness", with, StringComparison.Ordinal);
    }

    /// <summary>
    /// PROP-20.41 F2: C# <see cref="GrowthTraits.DefaultSeedTraits"/> is SoT;
    /// migration 014 INSERT is a bootstrap snapshot and must stay in parity.
    /// </summary>
    [Fact]
    public void Migration014_SeedInsert_MatchesDefaultSeedTraits()
    {
        var asm = typeof(SqliteMemorySession).Assembly;
        using var stream = asm.GetManifestResourceStream("SoulCore.Memory.Migrations.014_growth_bond.sql");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        // Normalize EOLs so Windows CRLF embeddings still match C#-built fragments.
        var sql = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains(
            "INSERT OR IGNORE INTO growth_trait_weights (trait_id, weight, band, prompt_line, ledger_reason)",
            sql,
            StringComparison.Ordinal);
        Assert.Equal(3, GrowthTraits.DefaultSeedTraits.Count);
        foreach (var trait in GrowthTraits.DefaultSeedTraits)
        {
            var weight = trait.Weight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var escapedPrompt = trait.PromptLine.Replace("'", "''", StringComparison.Ordinal);
            var escapedReason = trait.LedgerReason.Replace("'", "''", StringComparison.Ordinal);
            var header = $"('{trait.Id}', {weight}, '{trait.Band}',";
            Assert.True(
                sql.Contains(header, StringComparison.Ordinal),
                $"Migration 014 missing seed header for '{trait.Id}': {header}");
            Assert.True(
                sql.Contains($"'{escapedPrompt}'", StringComparison.Ordinal),
                $"Migration 014 prompt_line drifted from DefaultSeedTraits[{trait.Id}]");
            Assert.True(
                sql.Contains($"'{escapedReason}'", StringComparison.Ordinal),
                $"Migration 014 ledger_reason drifted from DefaultSeedTraits[{trait.Id}]");
        }
    }

    [Fact]
    public async Task Store_Migration014_SeedsTraits_AndBondCounters()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-prop2037-{Guid.NewGuid():N}.db");
        try
        {
            await using var session = new SqliteMemorySession(path);
            var store = new SqliteGrowthRepository(session);
            var started = await store.EnsureSoakStartedAsync(
                new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
            Assert.Equal(2026, started.Year);

            var traits = await store.ListTraitWeightsAsync();
            Assert.Equal(3, traits.Count);
            Assert.Contains(traits, t => t.Id == GrowthTraits.Responsiveness);

            // Seed contact: 3 distinct user days inside window.
            var transcript = new SqliteChatTranscriptRepository(session);
            await transcript.AppendAsync("c1", "user", "hi day1", channel: "desk");
            // Manually stamp older days via SQL for distinct contact days.
            await SeedChatDayAsync(session, "2026-11-02T10:00:00.000Z", "user");
            await SeedChatDayAsync(session, "2026-11-03T10:00:00.000Z", "user");
            await SeedChatDayAsync(session, "2026-11-04T10:00:00.000Z", "user");
            await SeedChatDayAsync(session, "2026-11-04T10:05:00.000Z", "assistant");

            var counters = await store.ComputeBondCountersAsync(
                new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero));
            Assert.True(counters.DaysWithContact >= 3, $"daysWithContact={counters.DaysWithContact}");
            Assert.True(counters.ContactTimeMinutes > 0, $"contactTimeMinutes={counters.ContactTimeMinutes}");

            Console.WriteLine(
                $"[PROP-20.37] bondCounters days={counters.DaysWithContact} minutes={counters.ContactTimeMinutes} tasks={counters.EntrustedTasks}");
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task Service_DisplayOnly_RejectsDelta_NoPromptTraits()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-prop2037-svc-{Guid.NewGuid():N}.db");
        try
        {
            await using var session = new SqliteMemorySession(path);
            var store = new SqliteGrowthRepository(session);
            var svc = new GrowthService(store, NullLogger<GrowthService>.Instance);
            var now = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
            await store.EnsureSoakStartedAsync(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

            var decision = await svc.ApplyProposedDeltaAsync(
                GrowthTraits.Responsiveness, 0.55, now);
            Assert.Equal(GrowthDeltaEngine.DecisionKind.Rejected, decision.Kind);

            var prompt = await svc.GetPromptGrowthTraitsAsync(now);
            Assert.Null(prompt);

            var snap = await svc.GetIdentitySnapshotAsync(now);
            Assert.Equal(GrowthSoakClock.SoakPhase.DisplayOnly, snap.Soak.Phase);
            Assert.False(snap.Soak.MayChangePrompt);
            Assert.Equal(3, snap.GrowthTraits.Count);

            Console.WriteLine(
                $"[PROP-20.37] display-only phase={GrowthSoakClock.PhaseLabel(snap.Soak.Phase)} mayChange={snap.Soak.MayChangePrompt} decision={decision.Kind}/{decision.Reason}");
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task Service_PostSoak_InsideBandApplies_BandCrossPending_Confirm()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-prop2037-open-{Guid.NewGuid():N}.db");
        try
        {
            await using var session = new SqliteMemorySession(path);
            var store = new SqliteGrowthRepository(session);
            var svc = new GrowthService(store, NullLogger<GrowthService>.Instance);
            var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
            await store.EnsureSoakStartedAsync(start);

            // 3 contact days in the 14-day window.
            await SeedChatDayAsync(session, "2026-10-02T12:00:00.000Z", "user");
            await SeedChatDayAsync(session, "2026-10-05T12:00:00.000Z", "user");
            await SeedChatDayAsync(session, "2026-10-10T12:00:00.000Z", "user");

            var openNow = start.AddDays(15);

            var inside = await svc.ApplyProposedDeltaAsync(
                GrowthTraits.Initiative, 0.55, openNow);
            Assert.Equal(GrowthDeltaEngine.DecisionKind.AutoApply, inside.Kind);
            var ledger = await store.ListLedgerAsync();
            Assert.Contains(ledger, e => e.TraitId == GrowthTraits.Initiative && e.Source == "auto");

            var cross = await svc.ApplyProposedDeltaAsync(
                GrowthTraits.Openness, 0.85, openNow);
            Assert.Equal(GrowthDeltaEngine.DecisionKind.Pending, cross.Kind);
            var pending = await store.ListPendingAsync();
            Assert.Single(pending);
            Assert.Equal(GrowthTraits.Openness, pending[0].TraitId);

            // Weight unchanged until confirm.
            var traitsBefore = await store.ListTraitWeightsAsync();
            var opennessBefore = traitsBefore.First(t => t.Id == GrowthTraits.Openness).Weight;
            Assert.Equal(0.52, opennessBefore, 2);

            var ok = await svc.ConfirmPendingAsync(pending[0].Id);
            Assert.True(ok);
            var traitsAfter = await store.ListTraitWeightsAsync();
            var opennessAfter = traitsAfter.First(t => t.Id == GrowthTraits.Openness).Weight;
            Assert.Equal(0.85, opennessAfter, 2);

            var prompt = await svc.GetPromptGrowthTraitsAsync(openNow);
            Assert.NotNull(prompt);
            Assert.Equal(3, prompt!.Count);

            var mind = VictoriaMindBlock.Build(
                Array.Empty<VictoriaMindBlock.FloorLine>(),
                growthTraits: prompt);
            Assert.Contains(VictoriaMindBlock.GrowthTraitsMarker, mind, StringComparison.Ordinal);

            Console.WriteLine(
                $"[PROP-20.37] open inside={inside.Kind} cross={cross.Kind} confirmedWeight={opennessAfter:0.00} promptGrowth={prompt.Count}");
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task Service_ContactGate_BlocksOpen_EvenAfterFourteenDays()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulcore-prop2037-gate-{Guid.NewGuid():N}.db");
        try
        {
            await using var session = new SqliteMemorySession(path);
            var store = new SqliteGrowthRepository(session);
            var svc = new GrowthService(store, NullLogger<GrowthService>.Instance);
            var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
            await store.EnsureSoakStartedAsync(start);

            // Only 1 contact day — quiet fortnight must not fake intimacy.
            await SeedChatDayAsync(session, "2026-10-03T12:00:00.000Z", "user");

            var day15 = start.AddDays(15);
            var snap = await svc.GetIdentitySnapshotAsync(day15);
            Assert.Equal(GrowthSoakClock.SoakPhase.GateClosed, snap.Soak.Phase);
            Assert.False(snap.Soak.MayChangePrompt);
            Assert.True(snap.Soak.ContactDaysInWindow < 3);

            var decision = await svc.ApplyProposedDeltaAsync(
                GrowthTraits.Responsiveness, 0.55, day15);
            Assert.Equal(GrowthDeltaEngine.DecisionKind.Rejected, decision.Kind);

            Console.WriteLine(
                $"[PROP-20.37] gate-closed contactDays={snap.Soak.ContactDaysInWindow} phase={GrowthSoakClock.PhaseLabel(snap.Soak.Phase)}");
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    private static async Task SeedChatDayAsync(SqliteMemorySession session, string occurredAt, string role)
    {
        // Session Connection is internal — seed via a second connection on the same path.
        await using var conn = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = session.DatabasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Shared
            }.ToString());
        await conn.OpenAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO chat_messages (conversation_id, role, content, occurred_at, channel)
            VALUES ('prop2037', $role, $content, $occurred_at, 'desk');
            """;
        cmd.Parameters.AddWithValue("$role", role);
        cmd.Parameters.AddWithValue("$content", $"seed {occurredAt}");
        cmd.Parameters.AddWithValue("$occurred_at", occurredAt);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
