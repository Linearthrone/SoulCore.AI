namespace House.ChatDesktop.Services;

/// <summary>
/// Top-right inset of the Start/Stop control on the Her-screen VM surface, in device-independent pixels.
/// </summary>
public static class VictoriaVmPowerButtonPlacement
{
    public const double Margin = 8;
    public const double ButtonWidth = 76;
    public const double ButtonHeight = 28;

    public readonly record struct DipPoint(double X, double Y);

    public static DipPoint TopRightOffset(
        double surfaceWidth,
        double buttonWidth = ButtonWidth,
        double margin = Margin)
    {
        var x = surfaceWidth - buttonWidth - margin;
        if (x < margin)
            x = margin;
        return new DipPoint(x, margin);
    }
}
