namespace SoulCore.Core.Safety;

/// <summary>
/// Immutable record of a single drift observation (report-only; does not block acts).
/// </summary>
public sealed record DriftReport(
    string Dimension,
    double Score,
    double Threshold,
    string? Note,
    DateTimeOffset ObservedAt)
{
    public bool ExceedsThreshold => Score > Threshold;
}
