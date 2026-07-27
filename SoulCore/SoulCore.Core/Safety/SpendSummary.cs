namespace SoulCore.Core.Safety;

/// <summary>
/// Snapshot of cumulative token usage and spend, returned by <see cref="SpendMeter.GetSummary"/>.
/// </summary>
public sealed record SpendSummary(
    long TotalTokensIn,
    long TotalTokensOut,
    decimal EstimatedCost,
    decimal MonthlyCap,
    bool CapExceeded,
    long MonthlyTokenCap = 0)
{
    public long TotalTokens => TotalTokensIn + TotalTokensOut;
}
