using Avalonia.Media;

namespace House.ChatDesktop.Services;

/// <summary>
/// Fill and stroke shared by the radial tab and the VM edge tab.
/// The fill is more translucent than the old alpha-230 near-white (247, 250, 255)
/// and carries a little more light blue. The mark stays dark so "CE" stays readable.
/// </summary>
public static class RadialTabChrome
{
    public const double BorderDip = 1d;

    public static Color FillColor { get; } = Color.FromArgb(168, 206, 228, 255);

    public static Color StrokeColor { get; } = Color.FromRgb(0x4B, 0xA3, 0xFF);

    public static Color MarkColor { get; } = Color.FromRgb(0x14, 0x20, 0x33);

    public static SolidColorBrush CreateFillBrush() => new(FillColor);

    public static SolidColorBrush CreateStrokeBrush() => new(StrokeColor);

    public static SolidColorBrush CreateMarkBrush() => new(MarkColor);
}
