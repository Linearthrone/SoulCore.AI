using SoulCore.Core.Safety;

namespace SoulCore.Protocol.Tests;

public class SpendMeterTests
{
    [Fact]
    public void Constructor_Defaults_HaveZeroRatesAnd30DollarCap()
    {
        var meter = new SpendMeter();
        var summary = meter.GetSummary();

        Assert.Equal(0, summary.TotalTokensIn);
        Assert.Equal(0, summary.TotalTokensOut);
        Assert.Equal(0m, summary.EstimatedCost);
        Assert.Equal(30m, summary.MonthlyCap);
        Assert.False(summary.CapExceeded);
    }

    [Fact]
    public void Constructor_NegativeRates_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpendMeter(inputRatePer1K: -0.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpendMeter(outputRatePer1K: -0.01m));
    }

    [Fact]
    public void Constructor_ZeroOrNegativeCap_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpendMeter(monthlyCapUsd: 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpendMeter(monthlyCapUsd: -1m));
    }

    [Fact]
    public void RecordUsage_WithZeroRates_NoCost()
    {
        var meter = new SpendMeter(inputRatePer1K: 0m, outputRatePer1K: 0m);

        meter.RecordUsage("ollama", 5000, 2000);

        var summary = meter.GetSummary();
        Assert.Equal(5000, summary.TotalTokensIn);
        Assert.Equal(2000, summary.TotalTokensOut);
        Assert.Equal(7000, summary.TotalTokens);
        Assert.Equal(0m, summary.EstimatedCost);
        Assert.False(summary.CapExceeded);
    }

    [Fact]
    public void RecordUsage_ComputesCostCorrectly()
    {
        var meter = new SpendMeter(
            inputRatePer1K: 0.01m,   // $0.01 per 1K input tokens
            outputRatePer1K: 0.03m,  // $0.03 per 1K output tokens
            monthlyCapUsd: 30m);

        meter.RecordUsage("ollama", 10000, 5000);

        var summary = meter.GetSummary();
        // Input: 10000/1000 * 0.01 = $0.10
        // Output: 5000/1000 * 0.03 = $0.15
        // Total: $0.25
        Assert.Equal(10000, summary.TotalTokensIn);
        Assert.Equal(5000, summary.TotalTokensOut);
        Assert.Equal(0.25m, summary.EstimatedCost);
        Assert.False(summary.CapExceeded);
    }

    [Fact]
    public void RecordUsage_AccumulatesAcrossCalls()
    {
        var meter = new SpendMeter(
            inputRatePer1K: 0.01m,
            outputRatePer1K: 0.02m,
            monthlyCapUsd: 30m);

        meter.RecordUsage("ollama", 1000, 1000);  // $0.01 + $0.02 = $0.03
        meter.RecordUsage("hermes", 2000, 500);  // $0.02 + $0.01 = $0.03
        meter.RecordUsage("ollama", 500, 500);   // $0.005 + $0.01 = $0.015

        var summary = meter.GetSummary();
        Assert.Equal(3500, summary.TotalTokensIn);
        Assert.Equal(2000, summary.TotalTokensOut);
        Assert.Equal(5500, summary.TotalTokens);
        Assert.Equal(0.075m, summary.EstimatedCost);
    }

    [Fact]
    public void GetSummary_CapExceeded_FlipsAtThreshold()
    {
        var meter = new SpendMeter(
            inputRatePer1K: 1.0m,   // $1.00 per 1K input tokens — expensive for testing
            outputRatePer1K: 0m,
            monthlyCapUsd: 5.0m);   // $5 cap

        // 4000 input tokens = $4.00 — under cap
        meter.RecordUsage("test", 4000, 0);
        var under = meter.GetSummary();
        Assert.False(under.CapExceeded);

        // 1000 more input tokens = $1.00 more → $5.00 total — reaches cap
        meter.RecordUsage("test", 1000, 0);
        var atCap = meter.GetSummary();
        Assert.Equal(5.0m, atCap.EstimatedCost);
        Assert.True(atCap.CapExceeded);
    }

    [Fact]
    public void GetSummary_CapExceeded_AtExactlyThreshold()
    {
        var meter = new SpendMeter(
            inputRatePer1K: 0.10m,
            outputRatePer1K: 0m,
            monthlyCapUsd: 1.0m);

        // 10000 input tokens at $0.10/1K = $1.00 — exactly at cap
        meter.RecordUsage("test", 10000, 0);
        var summary = meter.GetSummary();
        Assert.Equal(1.0m, summary.EstimatedCost);
        Assert.True(summary.CapExceeded);
    }

    [Fact]
    public void Reset_ClearsCounters_AndReturnsPreviousSummary()
    {
        var meter = new SpendMeter(
            inputRatePer1K: 0.01m,
            outputRatePer1K: 0.02m,
            monthlyCapUsd: 30m);

        meter.RecordUsage("ollama", 5000, 3000);
        // Cost: 5000/1000*0.01 + 3000/1000*0.02 = 0.05 + 0.06 = $0.11

        var before = meter.Reset();
        Assert.Equal(5000, before.TotalTokensIn);
        Assert.Equal(3000, before.TotalTokensOut);
        Assert.Equal(0.11m, before.EstimatedCost);

        var after = meter.GetSummary();
        Assert.Equal(0, after.TotalTokensIn);
        Assert.Equal(0, after.TotalTokensOut);
        Assert.Equal(0m, after.EstimatedCost);
        Assert.False(after.CapExceeded);
    }

    [Fact]
    public void RecordUsage_NullOrEmptyProvider_Throws()
    {
        var meter = new SpendMeter();
        Assert.Throws<ArgumentException>(() => meter.RecordUsage("", 100, 100));
        Assert.Throws<ArgumentException>(() => meter.RecordUsage("   ", 100, 100));
    }

    [Fact]
    public void RecordUsage_NegativeTokens_Throw()
    {
        var meter = new SpendMeter();
        Assert.Throws<ArgumentOutOfRangeException>(() => meter.RecordUsage("test", -1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => meter.RecordUsage("test", 100, -1));
    }

    [Fact]
    public void RecordUsage_ZeroTokens_AreValid()
    {
        var meter = new SpendMeter(inputRatePer1K: 0.01m, outputRatePer1K: 0.01m);
        meter.RecordUsage("test", 0, 0);

        var summary = meter.GetSummary();
        Assert.Equal(0, summary.TotalTokensIn);
        Assert.Equal(0, summary.TotalTokensOut);
        Assert.Equal(0m, summary.EstimatedCost);
    }

    [Fact]
    public void SpendSummary_TotalTokens_IsSumOfInAndOut()
    {
        var summary = new SpendSummary(100, 200, 0.5m, 30m, false);
        Assert.Equal(300, summary.TotalTokens);
    }

    [Fact]
    public void GetSummary_MultipleProviders_TrackedTogether()
    {
        var meter = new SpendMeter(
            inputRatePer1K: 0.01m,
            outputRatePer1K: 0.01m,
            monthlyCapUsd: 30m);

        meter.RecordUsage("ollama", 10000, 5000);
        meter.RecordUsage("hermes", 8000, 3000);
        meter.RecordUsage("openai", 5000, 2000);

        var summary = meter.GetSummary();
        Assert.Equal(23000, summary.TotalTokensIn);
        Assert.Equal(10000, summary.TotalTokensOut);
        Assert.Equal(33000, summary.TotalTokens);
        Assert.False(summary.CapExceeded);
    }
}
