using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Controls;

/// <summary>
/// PROP-22.5 / v3 — transparent borderless popup whose center sits on the main slab's
/// upper-left corner. Eight large status orbs fill a three-quarter arc in the space
/// outside the glass (the quadrant occupied by the window is empty).
/// Click outside (focus loss) or Esc closes it.
/// </summary>
public partial class RadialStatusWindow : Window
{
    /// <summary>Logical size of each status orb — dominates its radial chunk.</summary>
    private const double OrbDip = 56;

    /// <summary>Radius from the slab corner to orb centers.</summary>
    private const double RingRadiusDip = 118;

    public RadialStatusWindow()
    {
        InitializeComponent();
        Deactivated += (_, _) => Close();
    }

    /// <summary>Ticks of <see cref="Environment.TickCount64"/> when the popup last closed (re-open guard).</summary>
    public static long LastClosedTick { get; private set; }

    /// <summary>True when the last close came from losing focus within the last <paramref name="ms"/> ms.</summary>
    public static bool ClosedRecently(int ms = 250) =>
        LastClosedTick != 0 && Environment.TickCount64 - LastClosedTick < ms;

    public void SetStatus(OrbState orb, IReadOnlyList<ServiceStatus> services)
    {
        _ = orb;
        RadialCanvas.Children.Clear();

        // Corner of the main slab maps to the bottom-right of this canvas.
        var cx = Width - 24;
        var cy = Height - 24;

        // Window occupies the SE quadrant from that corner (screen: x right, y down).
        // Outside arc runs clockwise from south (90°) through west/north to east (360°).
        const double startDeg = 95;
        const double sweepDeg = 260;
        var n = Math.Max(1, services.Count);
        for (var i = 0; i < n; i++)
        {
            var t = (i + 0.5) / n;
            var deg = startDeg + sweepDeg * t;
            var rad = deg * Math.PI / 180.0;
            var x = cx + RingRadiusDip * Math.Cos(rad);
            var y = cy + RingRadiusDip * Math.Sin(rad);
            RadialCanvas.Children.Add(BuildOrbChip(services[i], x, y));
        }
    }

    /// <summary>
    /// Place so the radial's corner origin coincides with the main window's upper-left
    /// (the ring therefore protrudes left and above the glass).
    /// </summary>
    public void PositionOutside(Window main)
    {
        var scaling = main.DesktopScaling;
        var widthPx = (int)Math.Round(Width * scaling);
        var heightPx = (int)Math.Round(Height * scaling);
        var inset = (int)Math.Round(24 * scaling);

        var screen = main.Screens.ScreenFromWindow(main) ?? main.Screens.Primary;
        var area = screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);

        var mainPos = main.Position;
        // Bottom-right of this popup ≈ top-left of main.
        var x = mainPos.X - widthPx + inset;
        var y = mainPos.Y - heightPx + inset;

        x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - widthPx));
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - heightPx));
        Position = new PixelPoint(x, y);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        LastClosedTick = Environment.TickCount64;
        base.OnClosed(e);
    }

    private static Control BuildOrbChip(ServiceStatus s, double centerX, double centerY)
    {
        var light = new Ellipse
        {
            Width = OrbDip,
            Height = OrbDip,
            Fill = LightBrush(s),
            Stroke = new SolidColorBrush(Color.Parse("#66D0DCEB")),
            StrokeThickness = 1.5,
            Opacity = s.Light == ServiceLight.Off ? 0.45 : 1.0
        };

        var label = new TextBlock
        {
            Text = s.Label,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Opacity = 0.9,
            MaxWidth = OrbDip + 28,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var stack = new StackPanel
        {
            Width = OrbDip + 28,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.Children.Add(light);
        stack.Children.Add(label);
        ToolTip.SetTip(stack, $"{s.Label}: {s.Detail}");

        Canvas.SetLeft(stack, centerX - (OrbDip + 28) / 2);
        Canvas.SetTop(stack, centerY - OrbDip / 2);
        return stack;
    }

    /// <summary>Off is dim grey; Up is green; Down is red (critical) or yellow (non-critical).</summary>
    internal static IBrush LightBrush(ServiceStatus s) =>
        s.Light switch
        {
            ServiceLight.Off => Res("OffDimBrush"),
            ServiceLight.Up => Res("OkBrush"),
            ServiceLight.Down => s.Critical ? Res("BadBrush") : Res("YellowBrush"),
            _ => throw new ArgumentOutOfRangeException(nameof(s), s.Light, null)
        };

    internal static IBrush OrbBrush(OrbState state) =>
        state switch
        {
            OrbState.Green => Res("OkBrush"),
            OrbState.Yellow => Res("YellowBrush"),
            OrbState.Amber => Res("AmberBrush"),
            OrbState.PinkPulse => Res("PinkPulseBrush"),
            OrbState.Red => Res("BadBrush"),
            OrbState.FastRedFlash => Res("BadBrush"),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };

    private static IBrush Res(string key) =>
        Application.Current is { } app
        && app.TryFindResource(key, app.ActualThemeVariant, out var value)
        && value is IBrush brush
            ? brush
            : Brushes.Gray;
}
