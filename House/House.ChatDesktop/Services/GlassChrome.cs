using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace House.ChatDesktop.Services;

/// <summary>
/// Light and dark glass brushes. Chrome alpha cannot fall below <see cref="OpacityFloor"/>.
/// Text brushes stay opaque so the floor keeps copy readable.
/// </summary>
public static class GlassChrome
{
    public const double OpacityFloor = 0.55;
    public const double DefaultOpacity = 0.82;
    public const string LightTheme = "Light";
    public const string DarkTheme = "Dark";

    public readonly record struct Palette(
        bool Light,
        double Opacity,
        Color Text,
        Color Muted,
        Color Accent,
        Color AccentDim,
        Color Shell,
        Color Panel,
        Color PanelSoft,
        Color PanelBorder,
        Color Carbon,
        Color CarbonEdge,
        Color Input,
        Color UserBubble,
        Color AssistantBubble,
        Color Background);

    public static double ClampOpacity(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return DefaultOpacity;
        if (value < OpacityFloor)
            return OpacityFloor;
        if (value > 1d)
            return 1d;
        return value;
    }

    public static bool IsLightTheme(string? theme) =>
        string.Equals(theme, LightTheme, StringComparison.OrdinalIgnoreCase);

    /// <summary>Opaque dialog fill. Chrome opacity does not thin this color.</summary>
    public static Color OpaqueBackground(bool light) =>
        light ? Color.FromRgb(0xF7, 0xFA, 0xFF) : Color.FromRgb(0x12, 0x13, 0x16);

    public static Palette CreatePalette(bool light, double opacity)
    {
        opacity = ClampOpacity(opacity);
        var alpha = (byte)Math.Round(opacity * 255d);
        if (light)
        {
            return new Palette(
                true,
                opacity,
                Color.FromRgb(0x14, 0x20, 0x33),
                Color.FromRgb(0x5C, 0x6B, 0x80),
                Color.FromRgb(0x1F, 0x6F, 0xEB),
                Color.FromRgb(0x8F, 0xB4, 0xF5),
                WithAlpha(Color.FromRgb(0xF7, 0xFA, 0xFF), alpha),
                WithAlpha(Color.FromRgb(0xFF, 0xFF, 0xFF), alpha),
                WithAlpha(Color.FromRgb(0xE7, 0xEE, 0xF8), alpha),
                Color.FromRgb(0xC5, 0xD2, 0xE4),
                WithAlpha(Color.FromRgb(0xE8, 0xEE, 0xF6), alpha),
                Color.FromRgb(0xC5, 0xD0, 0xE0),
                WithAlpha(Color.FromRgb(0xFF, 0xFF, 0xFF), alpha),
                WithAlpha(Color.FromRgb(0xD9, 0xE6, 0xF7), alpha),
                WithAlpha(Color.FromRgb(0xFF, 0xFF, 0xFF), alpha),
                WithAlpha(Color.FromRgb(0xE7, 0xEE, 0xF8), alpha));
        }

        return new Palette(
            false,
            opacity,
            Color.FromRgb(0xE6, 0xE7, 0xEA),
            Color.FromRgb(0x8B, 0x8E, 0x97),
            Color.FromRgb(0x4B, 0xA3, 0xFF),
            Color.FromRgb(0x2A, 0x5F, 0x99),
            WithAlpha(Color.FromRgb(0x12, 0x13, 0x16), alpha),
            WithAlpha(Color.FromRgb(0x17, 0x19, 0x1C), alpha),
            WithAlpha(Color.FromRgb(0x1C, 0x1E, 0x22), alpha),
            Color.FromRgb(0x2A, 0x2E, 0x36),
            WithAlpha(Color.FromRgb(0x0E, 0x0F, 0x12), alpha),
            Color.FromRgb(0x23, 0x26, 0x2D),
            WithAlpha(Color.FromRgb(0x14, 0x16, 0x1A), alpha),
            WithAlpha(Color.FromRgb(0x2A, 0x2D, 0x33), alpha),
            WithAlpha(Color.FromRgb(0x22, 0x25, 0x2A), alpha),
            WithAlpha(Color.FromRgb(0x0B, 0x0C, 0x0E), alpha));
    }

    public static void Apply(Application app, bool light, double opacity)
    {
        ArgumentNullException.ThrowIfNull(app);
        var palette = CreatePalette(light, opacity);
        app.RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;

        Set(app, "TextBrush", palette.Text);
        Set(app, "MutedBrush", palette.Muted);
        Set(app, "AccentBrush", palette.Accent);
        Set(app, "AccentDimBrush", palette.AccentDim);
        Set(app, "BgBrush", palette.Background);
        Set(app, "ShellBrush", palette.Shell);
        Set(app, "PanelBrush", palette.Panel);
        Set(app, "PanelBrushSoft", palette.PanelSoft);
        Set(app, "PanelBorderBrush", palette.PanelBorder);
        Set(app, "CarbonBrush", palette.Carbon);
        Set(app, "CarbonEdgeBrush", palette.CarbonEdge);
        Set(app, "InputBrush", palette.Input);
        Set(app, "UserBubbleBrush", palette.UserBubble);
        Set(app, "AssistantBubbleBrush", palette.AssistantBubble);
        Set(app, "GaugeTrackBrush", palette.Carbon);

        var shellTop = Shift(palette.Shell, light ? 8 : 18);
        app.Resources["ShellFaceBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(shellTop, 0),
                new GradientStop(palette.Shell, 0.45),
                new GradientStop(palette.Background, 1)
            }
        };
        app.Resources["ShellTextureOpacity"] = light ? 0.12 : 0.55;
    }

    private static void Set(Application app, string key, Color color) =>
        app.Resources[key] = new SolidColorBrush(color);

    private static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Color Shift(Color color, int delta)
    {
        static byte Move(byte channel, int by)
        {
            var next = channel + by;
            if (next < 0) return 0;
            if (next > 255) return 255;
            return (byte)next;
        }

        return Color.FromArgb(color.A, Move(color.R, delta), Move(color.G, delta), Move(color.B, delta));
    }
}
