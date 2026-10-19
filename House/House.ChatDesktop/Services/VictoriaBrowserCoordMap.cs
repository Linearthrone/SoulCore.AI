namespace House.ChatDesktop.Services;

/// <summary>
/// PROP-19.3 — Shared Her-screen pointer contract.
/// <para>
/// <b>HWND embed (Kurt inject / Victoria Absolute / soft-cursor tip)</b> — one stretch-fill map:
/// <list type="number">
/// <item><see cref="ResolveGuestFramebuffer"/> — live <c>IDisplay</c> &gt; hub/screenshot &gt; slot &gt; default.
/// Never treat the embed slot as FB when display/hub disagree (Auto-resize lag).</item>
/// <item><see cref="TryMapPointerStretchFill"/> — slot client px → guest framebuffer 0-based
/// (<c>Round</c> AwayFromZero, clamp).</item>
/// <item><see cref="ToAbsolute"/> — guest 0-based → VirtualBox Absolute <c>[+1,+1]</c> (SDK).
/// Same rule as <c>VboxConsoleMouse.PlanAbsolutePress</c> in Host.</item>
/// <item><see cref="TryMapGuestTipToSurfaceStretchFill"/> — inverse of (2): guest 0-based →
/// soft-cursor <b>tip</b> in slot client px (no glyph centering).</item>
/// </list>
/// Soft-cursor tip must paint at the same Guest0 that (2)/(3) inject. Prefer
/// <see cref="MapSlotToInject"/> when both guest and Absolute are needed.
/// </para>
/// <para>
/// <b>JPEG / Playwright</b> — Uniform letterbox (<see cref="TryMapPointerToPage"/> /
/// <see cref="TryMapGuestToSurfaceUniform"/>). Do not mix letterbox with embed Absolute.
/// </para>
/// </summary>
public static class VictoriaBrowserCoordMap
{
    /// <summary>Default guest size when embed is live but no screenshot has published frame size yet.</summary>
    public const int DefaultGuestWidth = 1280;
    public const int DefaultGuestHeight = 800;

    /// <summary>
    /// Guest framebuffer size for stretch-fill. Prefer live <c>IDisplay</c>, then hub,
    /// then HWND slot, then JPEG default. Delegates to <see cref="VictoriaVmPointerPlan.ResolveGuestFramebuffer"/>.
    /// </summary>
    public static (int Width, int Height) ResolveGuestFramebuffer(
        int hubWidth,
        int hubHeight,
        int? displayWidth,
        int? displayHeight,
        int surfaceWidth = 0,
        int surfaceHeight = 0) =>
        VictoriaVmPointerPlan.ResolveGuestFramebuffer(
            hubWidth, hubHeight, displayWidth, displayHeight, surfaceWidth, surfaceHeight);

    /// <summary>
    /// Guest 0-based framebuffer pixel → Absolute <c>[1,1]</c> origin (VirtualBox SDK).
    /// Soft-cursor and agent tools stay on 0-based Guest0; Absolute +1 is the last inject step only.
    /// </summary>
    public static (int X, int Y) ToAbsolute(int guestX, int guestY) =>
        VictoriaVmPointerPlan.ToAbsolute(guestX, guestY);

    /// <summary>
    /// Slot client → Guest0 + Absolute in one step (Kurt <c>PostFromSurface</c> / hover badge).
    /// Null when pointer is outside the surface or sizes are invalid.
    /// </summary>
    public static (int GuestX, int GuestY, int AbsX, int AbsY)? MapSlotToInject(
        double pointerX,
        double pointerY,
        double surfaceWidth,
        double surfaceHeight,
        int framebufferWidth,
        int framebufferHeight)
    {
        var guest = TryMapPointerStretchFill(
            pointerX, pointerY, surfaceWidth, surfaceHeight,
            framebufferWidth, framebufferHeight);
        if (guest is null)
            return null;

        var abs = ToAbsolute(guest.Value.X, guest.Value.Y);
        return (guest.Value.X, guest.Value.Y, abs.X, abs.Y);
    }

    /// <summary>
    /// Uniform letterbox map (JPEG / Playwright frame). Null when pointer is in letterbox bands.
    /// </summary>
    public static (int X, int Y)? TryMapPointerToPage(
        double pointerX,
        double pointerY,
        double surfaceWidth,
        double surfaceHeight,
        int imagePixelWidth,
        int imagePixelHeight)
    {
        if (surfaceWidth <= 0 || surfaceHeight <= 0
            || imagePixelWidth <= 0 || imagePixelHeight <= 0)
            return null;

        var scale = Math.Min(surfaceWidth / imagePixelWidth, surfaceHeight / imagePixelHeight);
        if (scale <= 0)
            return null;

        var dispW = imagePixelWidth * scale;
        var dispH = imagePixelHeight * scale;
        var offsetX = (surfaceWidth - dispW) / 2.0;
        var offsetY = (surfaceHeight - dispH) / 2.0;

        if (pointerX < offsetX || pointerY < offsetY
            || pointerX > offsetX + dispW || pointerY > offsetY + dispH)
            return null;

        var x = (int)Math.Floor((pointerX - offsetX) / scale);
        var y = (int)Math.Floor((pointerY - offsetY) / scale);
        x = Math.Clamp(x, 0, imagePixelWidth - 1);
        y = Math.Clamp(y, 0, imagePixelHeight - 1);
        return (x, y);
    }

    /// <summary>
    /// Stretch-to-fill map (VM HWND embed fills the slot). Null when pointer is outside the surface.
    /// Guest0 for Absolute / soft-cursor tip — same transform both ways via
    /// <see cref="TryMapGuestTipToSurfaceStretchFill"/>.
    /// </summary>
    public static (int X, int Y)? TryMapPointerStretchFill(
        double pointerX,
        double pointerY,
        double surfaceWidth,
        double surfaceHeight,
        int imagePixelWidth,
        int imagePixelHeight)
    {
        if (surfaceWidth <= 0 || surfaceHeight <= 0
            || imagePixelWidth <= 0 || imagePixelHeight <= 0)
            return null;

        if (pointerX < 0 || pointerY < 0
            || pointerX > surfaceWidth || pointerY > surfaceHeight)
            return null;

        // Round to nearest guest pixel (Floor biased clicks half a pixel up-left).
        var x = (int)Math.Round(pointerX / surfaceWidth * imagePixelWidth, MidpointRounding.AwayFromZero);
        var y = (int)Math.Round(pointerY / surfaceHeight * imagePixelHeight, MidpointRounding.AwayFromZero);
        x = Math.Clamp(x, 0, imagePixelWidth - 1);
        y = Math.Clamp(y, 0, imagePixelHeight - 1);
        return (x, y);
    }

    /// <summary>
    /// Guest0 → soft-cursor tip in slot client pixels (inverse of <see cref="TryMapPointerStretchFill"/>).
    /// No glyph size — tip is the inject point; draw the arrow with hotspot at this tip.
    /// </summary>
    public static (double TipX, double TipY)? TryMapGuestTipToSurfaceStretchFill(
        int guestX,
        int guestY,
        double surfaceWidth,
        double surfaceHeight,
        int imagePixelWidth,
        int imagePixelHeight)
    {
        if (surfaceWidth <= 0 || surfaceHeight <= 0
            || imagePixelWidth <= 0 || imagePixelHeight <= 0)
            return null;

        var tipX = (guestX / (double)imagePixelWidth) * surfaceWidth;
        var tipY = (guestY / (double)imagePixelHeight) * surfaceHeight;
        return (tipX, tipY);
    }

    /// <summary>
    /// Guest pixel → surface top-left for a centered soft-cursor glyph (stretch-to-fill).
    /// Prefer <see cref="TryMapGuestTipToSurfaceStretchFill"/> for tip = inject point.
    /// </summary>
    public static (double Left, double Top)? TryMapGuestToSurfaceStretchFill(
        int guestX,
        int guestY,
        double surfaceWidth,
        double surfaceHeight,
        int imagePixelWidth,
        int imagePixelHeight,
        double cursorWidth,
        double cursorHeight)
    {
        var tip = TryMapGuestTipToSurfaceStretchFill(
            guestX, guestY, surfaceWidth, surfaceHeight, imagePixelWidth, imagePixelHeight);
        if (tip is null)
            return null;

        return (tip.Value.TipX - (cursorWidth / 2), tip.Value.TipY - (cursorHeight / 2));
    }

    /// <summary>Guest pixel → surface point for soft-cursor draw (Uniform letterbox).</summary>
    public static (double Left, double Top)? TryMapGuestToSurfaceUniform(
        int guestX,
        int guestY,
        double surfaceWidth,
        double surfaceHeight,
        int imagePixelWidth,
        int imagePixelHeight,
        double cursorWidth,
        double cursorHeight)
    {
        if (surfaceWidth <= 0 || surfaceHeight <= 0
            || imagePixelWidth <= 0 || imagePixelHeight <= 0)
            return null;

        var scale = Math.Min(surfaceWidth / imagePixelWidth, surfaceHeight / imagePixelHeight);
        if (scale <= 0)
            return null;

        var drawW = imagePixelWidth * scale;
        var drawH = imagePixelHeight * scale;
        var offsetX = (surfaceWidth - drawW) / 2;
        var offsetY = (surfaceHeight - drawH) / 2;
        var left = offsetX + (guestX * scale) - (cursorWidth / 2);
        var top = offsetY + (guestY * scale) - (cursorHeight / 2);
        return (left, top);
    }

    public static string FormatClickHint(int x, int y) => $"click ({x}, {y})";

    /// <summary>Her agent soft-cursor guest coords (option C / always).</summary>
    public static string FormatHerHint(int x, int y) => $"her ({x}, {y})";

    /// <summary>
    /// Option C badge: her agent coords always; operator hover while present.
    /// Null when neither source has coordinates.
    /// </summary>
    public static string? FormatCoordBadge(int? herX, int? herY, int? hoverX, int? hoverY)
    {
        if (herX is int hx && herY is int hy && hoverX is int ox && hoverY is int oy)
            return $"{FormatHerHint(hx, hy)} · you {FormatClickHint(ox, oy)}";
        if (herX is int hxOnly && herY is int hyOnly)
            return FormatHerHint(hxOnly, hyOnly);
        if (hoverX is int oxOnly && hoverY is int oyOnly)
            return FormatClickHint(oxOnly, oyOnly);
        return null;
    }
}
