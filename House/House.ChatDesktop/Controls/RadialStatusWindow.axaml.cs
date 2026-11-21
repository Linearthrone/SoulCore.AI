using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using House.ChatDesktop.Services;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace House.ChatDesktop.Controls;

/// <summary>
/// PROP-22 v3 — glass wedges with a large status orb in each, arranged on the
/// outside of the slab's upper-left corner (board 02). Click outside or Esc closes it.
/// </summary>
public partial class RadialStatusWindow : Window
{
    private IReadOnlyList<ServiceStatus> _services = Array.Empty<ServiceStatus>();
    private RadialLayout _layout;
    private double _leftDip = 420;
    private double _aboveDip = 140;

    public RadialStatusWindow()
    {
        InitializeComponent();
        Deactivated += (_, _) => Close();
        Opened += (_, _) => ApplyHitRegion();
    }

    /// <summary>Ticks of <see cref="Environment.TickCount64"/> when the popup last closed (re-open guard).</summary>
    public static long LastClosedTick { get; private set; }

    /// <summary>True when the last close came from losing focus within the last <paramref name="ms"/> ms.</summary>
    public static bool ClosedRecently(int ms = 250) =>
        LastClosedTick != 0 && Environment.TickCount64 - LastClosedTick < ms;

    public void SetStatus(OrbState orb, IReadOnlyList<ServiceStatus> services)
    {
        _ = orb;
        _services = services;
        Rebuild();
    }

    /// <summary>
    /// Place the ring's origin on the main window's upper-left glass corner.
    /// The arc uses the open space to the left and above that corner.
    /// </summary>
    public void PositionOutside(Window main)
    {
        MeasureRoom(main);
        Rebuild();

        var scaling = Math.Max(0.5, main.DesktopScaling);
        var widthPx = (int)Math.Round(Width * scaling);
        var heightPx = (int)Math.Round(Height * scaling);
        var corner = (int)Math.Round(4 * scaling);
        var originX = (int)Math.Round(_layout.OriginX * scaling);
        var originY = (int)Math.Round(_layout.OriginY * scaling);

        var screen = main.Screens?.ScreenFromWindow(main) ?? main.Screens?.Primary;
        var area = screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);

        var x = main.Position.X + corner - originX;
        var y = main.Position.Y + corner - originY;
        x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - widthPx));
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - heightPx));
        Position = new PixelPoint(x, y);
        ApplyHitRegion();
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

    private void MeasureRoom(Window main)
    {
        var scaling = Math.Max(0.5, main.DesktopScaling);
        var screen = main.Screens?.ScreenFromWindow(main) ?? main.Screens?.Primary;
        var area = screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        var cornerX = main.Position.X + 4 * scaling;
        var cornerY = main.Position.Y + 4 * scaling;
        _leftDip = Math.Max(0, (cornerX - area.X) / scaling);
        _aboveDip = Math.Max(0, (cornerY - area.Y) / scaling);
    }

    private void Rebuild()
    {
        RadialCanvas.Children.Clear();
        if (_services.Count == 0)
        {
            Width = 16;
            Height = 16;
            return;
        }

        _layout = RadialGeometry.Compute(_services.Count, _leftDip, _aboveDip);
        Width = _layout.Width;
        Height = _layout.Height;
        RadialCanvas.Width = _layout.Width;
        RadialCanvas.Height = _layout.Height;

        for (var i = 0; i < _services.Count && i < _layout.Slots.Count; i++)
            AddChunk(_services[i], _layout.Slots[i]);
    }

    private void AddChunk(ServiceStatus status, RadialSlot slot)
    {
        var tip = $"{status.Label}: {status.Detail}";
        var wedge = new ShapePath
        {
            Data = WedgeGeometry(_layout.CenterX, _layout.CenterY, _layout.InnerRadius, _layout.OuterRadius, slot.StartDeg, slot.EndDeg),
            Fill = WedgeFill(),
            Stroke = new SolidColorBrush(Color.Parse("#D7E0EC")),
            StrokeThickness = 1.35,
            StrokeJoin = PenLineJoin.Round
        };
        ToolTip.SetTip(wedge, tip);
        RadialCanvas.Children.Add(wedge);

        var color = BrushColor(LightBrush(status));
        var dim = status.Light == ServiceLight.Off;
        var glow = new Ellipse
        {
            Width = slot.Diameter * 1.65,
            Height = slot.Diameter * 1.65,
            Fill = GlowBrush(color, dim),
            IsHitTestVisible = false,
            Opacity = dim ? 0.45 : 1
        };
        Canvas.SetLeft(glow, slot.OrbX - glow.Width / 2);
        Canvas.SetTop(glow, slot.OrbY - glow.Height / 2);
        RadialCanvas.Children.Add(glow);

        var orb = new Ellipse
        {
            Width = slot.Diameter,
            Height = slot.Diameter,
            Fill = SphereBrush(color),
            Stroke = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
            StrokeThickness = 1,
            Opacity = dim ? 0.55 : 1
        };
        Canvas.SetLeft(orb, slot.OrbX - slot.Diameter / 2);
        Canvas.SetTop(orb, slot.OrbY - slot.Diameter / 2);
        ToolTip.SetTip(orb, tip);
        RadialCanvas.Children.Add(orb);

        var spec = new Ellipse
        {
            Width = slot.Diameter * 0.34,
            Height = slot.Diameter * 0.18,
            Fill = new SolidColorBrush(Color.FromArgb(dim ? (byte)70 : (byte)150, 255, 255, 255)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(spec, slot.OrbX - slot.Diameter * 0.30);
        Canvas.SetTop(spec, slot.OrbY - slot.Diameter * 0.34);
        RadialCanvas.Children.Add(spec);

        var label = new TextBlock
        {
            Text = status.Label,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = Res("TextBrush"),
            Width = 76,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, slot.LabelX - 38);
        Canvas.SetTop(label, slot.LabelY - 7);
        RadialCanvas.Children.Add(label);
    }

    private static Geometry WedgeGeometry(double cx, double cy, double inner, double outer, double startDeg, double endDeg)
    {
        Point At(double radius, double deg)
        {
            var rad = deg * Math.PI / 180.0;
            return new Point(cx + radius * Math.Cos(rad), cy + radius * Math.Sin(rad));
        }

        var figure = new PathFigure
        {
            StartPoint = At(inner, startDeg),
            IsClosed = true
        };
        figure.Segments!.Add(new LineSegment { Point = At(outer, startDeg) });
        figure.Segments.Add(new ArcSegment
        {
            Point = At(outer, endDeg),
            Size = new Size(outer, outer),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = endDeg - startDeg > 180
        });
        figure.Segments.Add(new LineSegment { Point = At(inner, endDeg) });
        figure.Segments.Add(new ArcSegment
        {
            Point = At(inner, startDeg),
            Size = new Size(inner, inner),
            SweepDirection = SweepDirection.CounterClockwise,
            IsLargeArc = endDeg - startDeg > 180
        });

        var geo = new PathGeometry();
        geo.Figures!.Add(figure);
        return geo;
    }

    private static LinearGradientBrush WedgeFill()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(new GradientStop(Color.Parse("#F2171D27"), 0));
        brush.GradientStops.Add(new GradientStop(Color.Parse("#E60A0D12"), 0.6));
        brush.GradientStops.Add(new GradientStop(Color.Parse("#F211151C"), 1));
        return brush;
    }

    private static RadialGradientBrush SphereBrush(Color color)
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new RelativePoint(0.34, 0.30, RelativeUnit.Relative),
            Center = new RelativePoint(0.46, 0.42, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.72, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.72, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(new GradientStop(Lift(color, 0.78), 0));
        brush.GradientStops.Add(new GradientStop(color, 0.42));
        brush.GradientStops.Add(new GradientStop(Shade(color, 0.32), 1));
        return brush;
    }

    private static RadialGradientBrush GlowBrush(Color color, bool dim)
    {
        var brush = new RadialGradientBrush();
        var alpha = dim ? (byte)36 : (byte)120;
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, color.R, color.G, color.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(alpha / 3), color.R, color.G, color.B), 0.55));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        return brush;
    }

    private static Color Lift(Color color, double towardWhite) =>
        Color.FromArgb(
            255,
            (byte)(color.R + (255 - color.R) * towardWhite),
            (byte)(color.G + (255 - color.G) * towardWhite),
            (byte)(color.B + (255 - color.B) * towardWhite));

    private static Color Shade(Color color, double keep) =>
        Color.FromArgb(255, (byte)(color.R * keep), (byte)(color.G * keep), (byte)(color.B * keep));

    private static Color BrushColor(IBrush brush) =>
        brush is SolidColorBrush solid ? solid.Color : Color.Parse("#3A424E");

    private static IBrush Res(string key) =>
        Application.Current is { } app
        && app.TryFindResource(key, app.ActualThemeVariant, out var value)
        && value is IBrush brush
            ? brush
            : Brushes.Gray;

    /// <summary>
    /// Limit hit-testing to the wedges so the transparent popup does not swallow
    /// clicks on the status orb or the title bar underneath the bounding box.
    /// </summary>
    private void ApplyHitRegion()
    {
        if (!OperatingSystem.IsWindows() || _services.Count == 0)
            return;

        var hwnd = TryGetPlatformHandle()?.Handle ?? 0;
        if (hwnd == 0)
            return;

        try
        {
            var scale = Math.Max(0.5, DesktopScaling);
            nint acc = 0;
            foreach (var slot in _layout.Slots)
            {
                var poly = WedgeRegion(slot, scale);
                if (poly == 0)
                    continue;
                if (acc == 0)
                {
                    acc = poly;
                    continue;
                }

                var next = CreateRectRgn(0, 0, 1, 1);
                _ = CombineRgn(next, acc, poly, RgnOr);
                _ = DeleteObject(acc);
                _ = DeleteObject(poly);
                acc = next;
            }

            if (acc == 0)
                return;

            // Keep the corner status button clickable.
            var keep = OrbKeepOut(scale);
            if (keep != 0)
            {
                var cut = CreateRectRgn(0, 0, 1, 1);
                _ = CombineRgn(cut, acc, keep, RgnDiff);
                _ = DeleteObject(acc);
                _ = DeleteObject(keep);
                acc = cut;
            }

            if (SetWindowRgn(hwnd, acc, true) == 0)
                _ = DeleteObject(acc);
        }
        catch
        {
            // The ring still paints if the region cannot be applied.
        }
    }

    private nint WedgeRegion(RadialSlot slot, double scale)
    {
        var pts = new List<WinPoint>(24);
        void Arc(double radius, double from, double to)
        {
            const int steps = 6;
            for (var s = 0; s <= steps; s++)
            {
                var deg = from + (to - from) * s / steps;
                var rad = deg * Math.PI / 180.0;
                pts.Add(new WinPoint
                {
                    X = (int)Math.Round((_layout.CenterX + radius * Math.Cos(rad)) * scale),
                    Y = (int)Math.Round((_layout.CenterY + radius * Math.Sin(rad)) * scale)
                });
            }
        }

        Arc(_layout.OuterRadius + 8, slot.StartDeg, slot.EndDeg);
        Arc(Math.Max(4, _layout.InnerRadius - 6), slot.EndDeg, slot.StartDeg);
        return CreatePolygonRgn(pts.ToArray(), pts.Count, Winding);
    }

    private nint OrbKeepOut(double scale)
    {
        int Px(double dip) => (int)Math.Round(dip * scale);
        return CreateEllipticRgn(
            Px(_layout.OriginX - 6),
            Px(_layout.OriginY - 6),
            Px(_layout.OriginX + 62),
            Px(_layout.OriginY + 54));
    }

    private const int Winding = 1;
    private const int RgnOr = 2;
    private const int RgnDiff = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("gdi32.dll")]
    private static extern nint CreatePolygonRgn(WinPoint[] points, int count, int fillMode);

    [DllImport("gdi32.dll")]
    private static extern nint CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern nint CreateEllipticRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(nint dest, nint src1, nint src2, int mode);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint obj);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint hwnd, nint region, bool redraw);
}
