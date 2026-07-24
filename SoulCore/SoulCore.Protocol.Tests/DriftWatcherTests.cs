using SoulCore.Core.Safety;

namespace SoulCore.Protocol.Tests;

public class DriftWatcherTests
{
    [Fact]
    public void Constructor_ZeroOrNegativeSlo_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DriftWatcher(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DriftWatcher(TimeSpan.FromMinutes(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DriftWatcher(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DriftWatcher(-5));
    }

    [Fact]
    public void GetStatus_NoReports_ReturnsEmptyStatus()
    {
        var watcher = new DriftWatcher(15);
        var status = watcher.GetStatus();

        Assert.Null(status.LastDriftReport);
        Assert.Equal(0, status.UnackedReports);
        Assert.False(status.SloExceeded);
    }

    [Fact]
    public void RecordDrift_AddsToUnacked_AndUpdatesLastReport()
    {
        var watcher = new DriftWatcher(15);
        var now = DateTimeOffset.UtcNow;

        var report1 = new DriftReport("identity", 0.5, 0.3, "Slight identity drift", now);
        watcher.RecordDrift(report1);

        var status = watcher.GetStatus(now);
        Assert.Equal(report1, status.LastDriftReport);
        Assert.Equal(1, status.UnackedReports);
        Assert.False(status.SloExceeded);
    }

    [Fact]
    public void RecordDrift_MultipleReports_TracksAll()
    {
        var watcher = new DriftWatcher(15);
        var now = DateTimeOffset.UtcNow;

        var r1 = new DriftReport("identity", 0.4, 0.3, null, now);
        var r2 = new DriftReport("emotion", 0.8, 0.5, "High emotional drift", now.AddSeconds(10));

        watcher.RecordDrift(r1);
        watcher.RecordDrift(r2);

        var status = watcher.GetStatus(now.AddSeconds(10));
        Assert.Equal(r2, status.LastDriftReport);
        Assert.Equal(2, status.UnackedReports);
        Assert.False(status.SloExceeded);
    }

    [Fact]
    public void GetStatus_SloExceeded_WhenOldestReportTooOld()
    {
        var sloMinutes = 15;
        var watcher = new DriftWatcher(sloMinutes);
        var baseTime = DateTimeOffset.UtcNow;

        var oldReport = new DriftReport("identity", 0.5, 0.3, "Old report", baseTime);
        watcher.RecordDrift(oldReport);

        // 10 minutes later — within SLO
        var status10 = watcher.GetStatus(baseTime.AddMinutes(10));
        Assert.False(status10.SloExceeded);

        // 15 minutes + 1 second later — exceeds SLO
        var status16 = watcher.GetStatus(baseTime.AddMinutes(15).AddSeconds(1));
        Assert.True(status16.SloExceeded);
        Assert.Equal(1, status16.UnackedReports);
    }

    [Fact]
    public void GetStatus_SloNotExceeded_WhenOldestReportWithinWindow()
    {
        var watcher = new DriftWatcher(15);
        var now = DateTimeOffset.UtcNow;

        var report = new DriftReport("identity", 0.5, 0.3, null, now);
        watcher.RecordDrift(report);

        var status = watcher.GetStatus(now.AddMinutes(14));
        Assert.False(status.SloExceeded);
    }

    [Fact]
    public void AcknowledgeAll_ClearsUnacked_AndReturnsCount()
    {
        var watcher = new DriftWatcher(15);
        var now = DateTimeOffset.UtcNow;

        watcher.RecordDrift(new DriftReport("identity", 0.4, 0.3, null, now));
        watcher.RecordDrift(new DriftReport("emotion", 0.6, 0.5, null, now));
        watcher.RecordDrift(new DriftReport("identity", 0.7, 0.3, null, now));

        var acked = watcher.AcknowledgeAll();
        Assert.Equal(3, acked);

        var status = watcher.GetStatus();
        Assert.Equal(0, status.UnackedReports);
        Assert.False(status.SloExceeded);
    }

    [Fact]
    public void AcknowledgeOldest_RemovesFirstReport()
    {
        var watcher = new DriftWatcher(15);
        var now = DateTimeOffset.UtcNow;

        var r1 = new DriftReport("identity", 0.4, 0.3, null, now);
        var r2 = new DriftReport("emotion", 0.6, 0.5, null, now.AddSeconds(5));

        watcher.RecordDrift(r1);
        watcher.RecordDrift(r2);

        var acked = watcher.AcknowledgeOldest();
        Assert.True(acked);

        var status = watcher.GetStatus();
        Assert.Equal(1, status.UnackedReports);
        Assert.Equal(r2, status.LastDriftReport);
    }

    [Fact]
    public void AcknowledgeOldest_EmptyList_ReturnsFalse()
    {
        var watcher = new DriftWatcher(15);
        var result = watcher.AcknowledgeOldest();
        Assert.False(result);
    }

    [Fact]
    public void AcknowledgeAll_AfterSloExceeded_ClearsSloStatus()
    {
        var watcher = new DriftWatcher(15);
        var baseTime = DateTimeOffset.UtcNow;

        watcher.RecordDrift(new DriftReport("identity", 0.5, 0.3, null, baseTime));

        // Verify SLO is exceeded
        var exceeded = watcher.GetStatus(baseTime.AddMinutes(20));
        Assert.True(exceeded.SloExceeded);

        // Acknowledge and verify SLO clears
        watcher.AcknowledgeAll();
        var after = watcher.GetStatus(baseTime.AddMinutes(20));
        Assert.False(after.SloExceeded);
    }

    [Fact]
    public void DriftReport_ExceedsThreshold_ComputedCorrectly()
    {
        var report = new DriftReport("identity", 0.8, 0.5, null, DateTimeOffset.UtcNow);
        Assert.True(report.ExceedsThreshold);

        var report2 = new DriftReport("identity", 0.3, 0.5, null, DateTimeOffset.UtcNow);
        Assert.False(report2.ExceedsThreshold);

        var report3 = new DriftReport("identity", 0.5, 0.5, null, DateTimeOffset.UtcNow);
        Assert.False(report3.ExceedsThreshold); // equal, not exceeding
    }

    [Fact]
    public void RecordDrift_NullReport_Throws()
    {
        var watcher = new DriftWatcher(15);
        Assert.Throws<ArgumentNullException>(() => watcher.RecordDrift(null!));
    }
}
