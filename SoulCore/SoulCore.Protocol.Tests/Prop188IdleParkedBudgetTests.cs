using SoulCore.Inference.Presence;

namespace SoulCore.Protocol.Tests;

/// <summary>PROP-18.8 / Q3 B3: soft budget for unsupervised parked-task advances.</summary>
public class Prop188IdleParkedBudgetTests
{
    private static readonly TimeZoneInfo FixedTz =
        TimeZoneInfo.CreateCustomTimeZone("prop188-test", TimeSpan.FromHours(-5), "prop188", "prop188");

    private static FileIdleParkedWorkBudget NewBudget(string path, DateTimeOffset? seedNow = null)
    {
        var budget = new FileIdleParkedWorkBudget(path, FixedTz);
        // Touch EnsureDay via snapshot so day key is seeded for assertions.
        _ = budget.GetSnapshot(seedNow ?? new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
        return budget;
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"prop188-budget-{Guid.NewGuid():N}.json");

    [Fact]
    public void AdvancesCap_AllowsThreeThenBlocksFourth()
    {
        var path = TempPath();
        try
        {
            var now = new DateTimeOffset(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);
            var budget = NewBudget(path, now);

            Assert.True(budget.TryBeginAdvance(1, now));
            budget.EndAdvance(1, now);
            Assert.True(budget.TryBeginAdvance(2, now));
            budget.EndAdvance(2, now);
            Assert.True(budget.TryBeginAdvance(3, now));
            budget.EndAdvance(3, now);

            Assert.False(budget.CanStartAdvance(now));
            Assert.False(budget.TryBeginAdvance(4, now));

            var snap = budget.GetSnapshot(now);
            Assert.Equal(3, snap.AdvancesUsed);
            Assert.Equal(3, snap.AdvancesCap);
            Assert.True(snap.Exhausted);
            Assert.Equal("advances_cap", snap.ExhaustReason);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void ToolTimeCap_BlocksNewAdvancesAt45Minutes()
    {
        var path = TempPath();
        try
        {
            var now = new DateTimeOffset(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);
            var budget = NewBudget(path, now);

            Assert.True(budget.TryBeginAdvance(10, now));
            budget.RecordToolTime(TimeSpan.FromMinutes(44), now);
            Assert.False(budget.IsToolTimeExhausted(now));
            Assert.True(budget.CanStartAdvance(now) is false); // serial busy
            budget.EndAdvance(10, now);

            Assert.True(budget.CanStartAdvance(now));
            Assert.True(budget.TryBeginAdvance(11, now));
            budget.RecordToolTime(TimeSpan.FromMinutes(1.5), now);
            Assert.True(budget.IsToolTimeExhausted(now));

            budget.EndAdvance(11, now);
            Assert.False(budget.CanStartAdvance(now));
            Assert.False(budget.TryBeginAdvance(12, now));

            var snap = budget.GetSnapshot(now);
            Assert.True(snap.Exhausted);
            Assert.Equal("tool_time_cap", snap.ExhaustReason);
            Assert.True(snap.ToolMinutesUsed >= 45.0);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void Serial_OneActiveParkedAdvanceAtATime()
    {
        var path = TempPath();
        try
        {
            var now = new DateTimeOffset(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);
            var budget = NewBudget(path, now);

            Assert.True(budget.TryBeginAdvance(21, now));
            // Same task may re-enter (probe pass).
            Assert.True(budget.TryBeginAdvance(21, now));
            // Different task refused while serial slot held.
            Assert.False(budget.TryBeginAdvance(22, now));
            Assert.False(budget.CanStartAdvance(now));

            budget.EndAdvance(21, now);
            Assert.True(budget.CanStartAdvance(now));
            Assert.True(budget.TryBeginAdvance(22, now));
            Assert.Equal(22, budget.GetSnapshot(now).ActiveTaskId);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void LeisureJournalActivityLog_AreExemptKinds()
    {
        // PROP-18.19 / F2: single law catalog — production leisure/soul-loop/parked gates call this.
        Assert.True(FileIdleParkedWorkBudget.IsExemptKind("leisure"));
        Assert.True(FileIdleParkedWorkBudget.IsExemptKind("journal"));
        Assert.True(FileIdleParkedWorkBudget.IsExemptKind("activity-log"));
        Assert.True(FileIdleParkedWorkBudget.IsExemptKind("activity_log"));
        Assert.True(FileIdleParkedWorkBudget.IsExemptKind("soulloop"));
        Assert.True(FileIdleParkedWorkBudget.IsExemptKind("soul-loop"));
        Assert.False(FileIdleParkedWorkBudget.IsExemptKind("parked-work"));
        Assert.False(FileIdleParkedWorkBudget.IsExemptKind("assigned-work"));
        Assert.False(FileIdleParkedWorkBudget.IsExemptKind("research"));
        Assert.False(FileIdleParkedWorkBudget.IsExemptKind(null));
    }

    [Fact]
    public void LeisureExemption_RecordingWithoutActiveAdvanceDoesNotConsume()
    {
        var path = TempPath();
        try
        {
            var now = new DateTimeOffset(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);
            var budget = NewBudget(path, now);

            // Leisure / journal / activity-log never call TryBeginAdvance / RecordToolTime.
            // Guard: RecordToolTime with no active advance is a no-op.
            Assert.True(FileIdleParkedWorkBudget.IsExemptKind("leisure"));
            budget.RecordToolTime(TimeSpan.FromMinutes(60), now);

            var snap = budget.GetSnapshot(now);
            Assert.Equal(0, snap.AdvancesUsed);
            Assert.Equal(0.0, snap.ToolMinutesUsed);
            Assert.False(snap.Exhausted);
            Assert.True(budget.CanStartAdvance(now));
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void LocalDayRollover_ResetsCounters()
    {
        var path = TempPath();
        try
        {
            var day1 = new DateTimeOffset(2026, 11, 19, 20, 0, 0, TimeSpan.Zero); // local 15:00 on -5
            var budget = NewBudget(path, day1);
            Assert.True(budget.TryBeginAdvance(1, day1));
            budget.EndAdvance(1, day1);
            Assert.True(budget.TryBeginAdvance(2, day1));
            budget.EndAdvance(2, day1);
            Assert.True(budget.TryBeginAdvance(3, day1));
            budget.EndAdvance(3, day1);
            Assert.False(budget.CanStartAdvance(day1));

            // Next local calendar day (UTC morning → still prior local evening? use clear next day).
            var day2 = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero); // local 07:00
            Assert.True(budget.CanStartAdvance(day2));
            Assert.True(budget.TryBeginAdvance(99, day2));
            var snap = budget.GetSnapshot(day2);
            Assert.Equal("2026-11-20", snap.LocalDay);
            Assert.Equal(1, snap.AdvancesUsed);
            Assert.False(snap.Exhausted);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void Persist_ReloadsCountersForSameLocalDay()
    {
        var path = TempPath();
        try
        {
            // Use "now" so ctor EnsureDay(UtcNow) and reload share the same local day.
            var now = DateTimeOffset.UtcNow;
            var first = new FileIdleParkedWorkBudget(path, FixedTz);
            Assert.True(first.TryBeginAdvance(7, now));
            first.RecordToolTime(TimeSpan.FromMinutes(10), now);
            first.EndAdvance(7, now);

            var second = new FileIdleParkedWorkBudget(path, FixedTz);
            var snap = second.GetSnapshot(now);
            Assert.Equal(1, snap.AdvancesUsed);
            Assert.True(snap.ToolMinutesUsed >= 9.9);
            Assert.Null(snap.ActiveTaskId);
            Assert.True(second.CanStartAdvance(now));
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort
        }
    }
}
