using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;

namespace House.ChatDesktop.Controls;

/// <summary>
/// PROP-22.6 / v3 — borderless, opaque popup that protrudes beside MainWindow. Owns the
/// Her-screen embed slot and What-she-saw panel so the VirtualBox HWND has a real layout
/// rectangle. Default size aims at a full guest display without scrollbars; the window is
/// resizable so the operator can grow it further.
/// </summary>
public partial class VmDrawerWindow : Window
{
    /// <summary>Gap between the main slab's rim and the drawer.</summary>
    public const int DockGapDip = 6;

    /// <summary>Preferred logical width so a typical guest desktop fits without scrollbars.</summary>
    public const double PreferredWidthDip = 1280;

    /// <summary>Preferred logical height (matches a landscape guest, independent of the portrait slab).</summary>
    public const double PreferredHeightDip = 800;

    public VmDrawerWindow()
    {
        InitializeComponent();
        Width = PreferredWidthDip;
        Height = PreferredHeightDip;
        VmDrawerCloseButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the operator hits the drawer's own close button.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Dock beside <paramref name="main"/> on <paramref name="onLeft"/> or the right edge.
    /// Keeps the preferred drawer size (does not shrink to the portrait slab height) so the
    /// full guest display can fit without scrollbars; clamps to the working area.
    /// </summary>
    public void DockTo(Window main, bool onLeft)
    {
        var scaling = main.DesktopScaling;
        var mainSize = PixelSize.FromSize(main.ClientSize, scaling);
        var mainPos = main.Position;
        var gap = (int)Math.Round(DockGapDip * scaling);

        var screen = main.Screens.ScreenFromWindow(main) ?? main.Screens.Primary;
        var area = screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);

        // Prefer a landscape drawer large enough for the guest; fall back to available space.
        var widthDip = Math.Min(PreferredWidthDip, Math.Max(MinWidth, area.Width / scaling - 40));
        var heightDip = Math.Min(PreferredHeightDip, Math.Max(MinHeight, area.Height / scaling - 40));
        // Never shorter than the slab when there is room — keeps the protrusion aligned.
        heightDip = Math.Max(heightDip, Math.Min(main.ClientSize.Height, area.Height / scaling - 40));

        Width = widthDip;
        Height = heightDip;

        var widthPx = (int)Math.Round(widthDip * scaling);
        var heightPx = (int)Math.Round(heightDip * scaling);

        var x = onLeft
            ? mainPos.X - widthPx - gap
            : mainPos.X + mainSize.Width + gap;
        // Vertically center on the slab when the drawer is taller than the phone window.
        var y = mainPos.Y + (mainSize.Height - heightPx) / 2;

        x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - widthPx));
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - heightPx));

        Position = new PixelPoint(x, y);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
