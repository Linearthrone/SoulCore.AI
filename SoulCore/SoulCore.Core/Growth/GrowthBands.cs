namespace SoulCore.Core.Growth;

/// <summary>
/// PROP-20.37 / growth-routing decision: Low 0.15–0.30, Mid 0.45–0.60, High 0.80–0.95.
/// Gaps between preferred ranges use midpoints for band membership.
/// </summary>
public static class GrowthBands
{
    public const string Low = "Low";
    public const string Mid = "Mid";
    public const string High = "High";

    public const double LowMin = 0.15;
    public const double LowMax = 0.30;
    public const double MidMin = 0.45;
    public const double MidMax = 0.60;
    public const double HighMin = 0.80;
    public const double HighMax = 0.95;

    /// <summary>Midpoint between LowMax and MidMin — below is Low.</summary>
    public const double LowMidBoundary = 0.375;

    /// <summary>Midpoint between MidMax and HighMin — below is Mid, at/above is High.</summary>
    public const double MidHighBoundary = 0.70;

    public const double DefaultSeedWeight = 0.52;

    public static string ResolveBand(double weight)
    {
        if (weight < LowMidBoundary)
            return Low;
        if (weight < MidHighBoundary)
            return Mid;
        return High;
    }

    public static bool IsBandCross(double fromWeight, double toWeight) =>
        !string.Equals(ResolveBand(fromWeight), ResolveBand(toWeight), StringComparison.Ordinal);

    public static bool IsInsideBand(double fromWeight, double toWeight) =>
        !IsBandCross(fromWeight, toWeight);

    public static double ClampWeight(double weight) =>
        Math.Clamp(weight, LowMin, HighMax);
}
