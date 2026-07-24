namespace SoulCore.Config;

/// <summary>
/// Non-secret safety / spend knobs for Phase 3 (drift + spend meter).
/// Defined here for future runtime binding; NOT wired into Program.cs DI in TASK-080.
/// </summary>
public sealed class SafetyOptions
{
    public const string SectionName = "Safety";

    /// <summary>
    /// SLO window for drift reports. If the oldest unacknowledged report is older
    /// than this window, <c>DriftWatcher.GetStatus().SloExceeded</c> flips true.
    /// Default 15 minutes.
    /// </summary>
    public int DriftSloMinutes { get; set; } = 15;

    /// <summary>
    /// Monthly spend cap in USD. When <c>SpendMeter.GetSummary().EstimatedCost</c>
    /// reaches this value, <c>CapExceeded</c> flips true. Default $30/mo.
    /// </summary>
    public decimal MonthlyCapUsd { get; set; } = 30m;

    /// <summary>
    /// Cost per 1K input tokens (USD). Default $0 — set per provider rate.
    /// </summary>
    public decimal InputTokenRatePer1K { get; set; } = 0m;

    /// <summary>
    /// Cost per 1K output tokens (USD). Default $0 — set per provider rate.
    /// </summary>
    public decimal OutputTokenRatePer1K { get; set; } = 0m;
}
