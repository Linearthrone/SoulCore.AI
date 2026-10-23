using Avalonia;

namespace House.ChatDesktop.Services;

/// <summary>
/// Hit shape for one radial slot. The words sit to the right of the dot and are part of the hit.
/// </summary>
public static class RadialSlotHit
{
    public const double DotRadius = 18;
    public const double DotPad = 6;
    public const double CaptionLead = 22;
    public const double CaptionPad = 4;

    public static Rect Dot(Point at) =>
        new(at.X - DotRadius, at.Y - DotRadius, DotRadius * 2, DotRadius * 2);

    public static Rect Caption(Point at, double captionWidth, double captionHeight) =>
        new(
            at.X + CaptionLead,
            at.Y - captionHeight / 2,
            Math.Max(0, captionWidth),
            Math.Max(0, captionHeight));

    public static Rect Grow(Rect rect, double by) =>
        new(rect.X - by, rect.Y - by, rect.Width + (by * 2), rect.Height + (by * 2));

    public static Rect For(Point at, double captionWidth, double captionHeight) =>
        Grow(Dot(at), DotPad).Union(Grow(Caption(at, captionWidth, captionHeight), CaptionPad));
}
