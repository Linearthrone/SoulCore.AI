namespace House.ChatDesktop.Controls;

/// <summary>One wedge of the status radial, in popup-canvas DIP.</summary>
internal readonly record struct RadialSlot(
    double StartDeg,
    double EndDeg,
    double OrbX,
    double OrbY,
    double LabelX,
    double LabelY,
    double Diameter);

/// <summary>
/// Popup size and wedge placement for the v3 status radial.
/// Layout space has its origin on the slab's upper-left corner (+x into the slab, +y down).
/// </summary>
internal readonly record struct RadialLayout(
    double OriginX,
    double OriginY,
    double CenterX,
    double CenterY,
    double Width,
    double Height,
    double InnerRadius,
    double OuterRadius,
    double BottomDeg,
    double TopDeg,
    double CrownY,
    IReadOnlyList<RadialSlot> Slots);

/// <summary>
/// v3 board 02 — a fat glass C outside the slab's upper-left corner.
/// The circle's center sits just off that corner so the wedges hug the glass,
/// the orbs fill each wedge, and the crown stays inside the space above the window.
/// </summary>
internal static class RadialGeometry
{
    private const double SweepDeg = 210;
    private const double GapDeg = 2.8;
    private const double Pad = 14;
    private const double LabelBoxW = 76;
    private const double LabelBoxH = 16;

    internal static RadialLayout Compute(int count, double leftDip, double aboveDip)
    {
        count = Math.Max(1, count);
        var gaps = GapDeg * (count - 1);
        var wedge = gaps < SweepDeg ? (SweepDeg - gaps) / count : SweepDeg / count;
        var wedgeRad = wedge * Math.PI / 180.0;

        // Sphere fills the chunk: radial depth is only a little more than the orb.
        var diam = 56.0;
        var mid = (diam / 0.82) / wedgeRad;
        var inner = mid - diam * 0.58;
        var outer = mid + diam * 0.50 + 18;

        var room = Math.Max(150, leftDip - 12);
        var need = outer * 1.28 + 28;
        if (need > room)
        {
            var scale = room / need;
            diam *= scale;
            mid *= scale;
            inner = Math.Max(28, inner * scale);
            outer *= scale;
        }

        var bottom = 180 - SweepDeg / 2.0 + 4;
        var top = bottom + SweepDeg;
        var step = count == 1 ? 0 : wedge + (gaps < SweepDeg ? GapDeg : 0);
        var first = top - wedge / 2;

        var maxCos = Math.Max(Math.Cos(bottom * Math.PI / 180.0), Math.Cos(top * Math.PI / 180.0));
        var minSin = bottom <= 270 && top >= 270
            ? -1
            : Math.Min(Math.Sin(bottom * Math.PI / 180.0), Math.Sin(top * Math.PI / 180.0));

        var rise = Math.Max(0, aboveDip - 28);
        // Kiss the corner: the outermost right-hand point of the rim lands just left of the glass.
        var cx = 2 - maxCos * outer;
        var cy = -rise - outer * minSin;

        double minX = 0, minY = 0, maxX = 0, maxY = 0;
        var have = false;
        void Acc(double x, double y)
        {
            if (!have)
            {
                minX = maxX = x;
                minY = maxY = y;
                have = true;
                return;
            }

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        void AccPolar(double radius, double deg)
        {
            var rad = deg * Math.PI / 180.0;
            Acc(cx + radius * Math.Cos(rad), cy + radius * Math.Sin(rad));
        }

        var draft = new (double Start, double End, double Ang)[count];
        for (var i = 0; i < count; i++)
        {
            var ang = first - i * step;
            var start = ang - wedge / 2;
            var end = ang + wedge / 2;
            draft[i] = (start, end, ang);
            AccPolar(outer + 3, start);
            AccPolar(outer + 3, ang);
            AccPolar(outer + 3, end);
            AccPolar(Math.Max(8, inner - 2), start);
            AccPolar(Math.Max(8, inner - 2), end);

            var rad = ang * Math.PI / 180.0;
            var labelR = outer + 16;
            var lx = cx + labelR * Math.Cos(rad);
            var ly = cy + labelR * Math.Sin(rad);
            Acc(lx - LabelBoxW / 2, ly - LabelBoxH / 2);
            Acc(lx + LabelBoxW / 2, ly + LabelBoxH / 2);
        }

        var width = Math.Max(32, maxX - minX + Pad * 2);
        var height = Math.Max(32, maxY - minY + Pad * 2);
        var originX = Pad - minX;
        var originY = Pad - minY;

        var slots = new RadialSlot[count];
        for (var i = 0; i < count; i++)
        {
            var ang = draft[i].Ang;
            var rad = ang * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            var labelR = outer + 16;
            slots[i] = new RadialSlot(
                draft[i].Start,
                draft[i].End,
                originX + cx + mid * cos,
                originY + cy + mid * sin,
                originX + cx + labelR * cos,
                originY + cy + labelR * sin,
                diam);
        }

        return new RadialLayout(
            originX,
            originY,
            originX + cx,
            originY + cy,
            width,
            height,
            inner,
            outer,
            bottom,
            top,
            cy + outer * minSin,
            slots);
    }
}
