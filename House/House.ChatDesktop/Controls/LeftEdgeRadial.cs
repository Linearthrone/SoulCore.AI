using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Controls;

/// <summary>
/// Left-edge half-circle that grows about 4x into a radial.
/// Slots: Create Agent, Contacts, and at most six favorites.
/// The One/Many chip is a sibling layered on this control by <c>RadialEdgeWindow</c>.
/// </summary>
public sealed class LeftEdgeRadial : Control
{
    private readonly List<Hit> _hits = new();
    private readonly List<Bitmap> _photos = new();
    private readonly List<PreparedFavorite> _prepared = new();

    public LeftEdgeRadial()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public bool IsExpanded { get; set; }

    public event EventHandler? ExpandRequested;
    public event EventHandler? CollapseRequested;
    public event EventHandler? CreateAgentRequested;
    public event EventHandler? ContactsRequested;
    public event EventHandler<FavoriteSlot>? FavoriteRequested;

    public void SetFavorites(IReadOnlyList<FavoriteSlot>? favorites)
    {
        foreach (var photo in _photos)
            photo.Dispose();
        _photos.Clear();
        _prepared.Clear();
        foreach (var favorite in (favorites ?? Array.Empty<FavoriteSlot>()).Take(FavoriteRoster.MaxFavorites))
        {
            Bitmap? photo = null;
            if (!string.IsNullOrWhiteSpace(favorite.PhotoPath) && File.Exists(favorite.PhotoPath))
            {
                try
                {
                    photo = new Bitmap(favorite.PhotoPath);
                    _photos.Add(photo);
                }
                catch
                {
                    photo = null;
                }
            }

            _prepared.Add(new PreparedFavorite(favorite, photo));
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        if (bounds.Width < 2 || bounds.Height < 2)
            return;

        var fill = RadialTabChrome.CreateFillBrush();
        var stroke = RadialTabChrome.CreateStrokeBrush();
        var text = Brush("TextBrush", Brushes.Black);
        var muted = Brush("MutedBrush", Brushes.Gray);
        var center = new Point(0, bounds.Height / 2);
        var radius = Math.Max(8, bounds.Height / 2 - 6);
        var pen = new Pen(stroke, IsExpanded ? 1.5 : RadialTabChrome.BorderDip);
        context.DrawEllipse(fill, pen, center, radius, radius);

        _hits.Clear();
        if (!IsExpanded)
        {
            var label = new FormattedText(
                "CE",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                12,
                RadialTabChrome.CreateMarkBrush());
            context.DrawText(label, new Point(8, center.Y - label.Height / 2));
            return;
        }

        var slots = BuildSlots();
        var arcRadius = radius * 0.62;
        for (var i = 0; i < slots.Count; i++)
        {
            var t = slots.Count == 1 ? 0.5 : i / (double)(slots.Count - 1);
            var degrees = -72 + (144 * t);
            var radians = degrees * Math.PI / 180d;
            var at = new Point(
                center.X + Math.Cos(radians) * arcRadius,
                center.Y + Math.Sin(radians) * arcRadius);
            var dot = RadialSlotHit.Dot(at);
            context.DrawEllipse(
                Brush("CarbonBrush", Brushes.White),
                new Pen(stroke, 1),
                at,
                RadialSlotHit.DotRadius,
                RadialSlotHit.DotRadius);
            if (slots[i].Photo is { } photo)
            {
                using (context.PushClip(new RoundedRect(dot, RadialSlotHit.DotRadius)))
                    context.DrawImage(photo, dot);
            }
            else
            {
                var mark = new FormattedText(
                    slots[i].Mark,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    11,
                    text);
                context.DrawText(mark, new Point(at.X - mark.Width / 2, at.Y - mark.Height / 2));
            }

            var caption = new FormattedText(
                slots[i].Caption,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                11,
                muted);
            context.DrawText(
                caption,
                new Point(at.X + RadialSlotHit.CaptionLead, at.Y - caption.Height / 2));
            _hits.Add(new Hit(
                RadialSlotHit.For(at, caption.Width, caption.Height),
                slots[i].Kind,
                slots[i].Favorite));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var point = e.GetPosition(this);
        if (!IsExpanded)
        {
            e.Handled = true;
            ExpandRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        foreach (var hit in _hits)
        {
            if (!hit.Bounds.Contains(point))
                continue;
            e.Handled = true;
            switch (hit.Kind)
            {
                case SlotKind.CreateAgent:
                    CreateAgentRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case SlotKind.Contacts:
                    ContactsRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case SlotKind.Favorite when hit.Favorite is not null:
                    FavoriteRequested?.Invoke(this, hit.Favorite);
                    break;
            }

            return;
        }

        e.Handled = true;
        CollapseRequested?.Invoke(this, EventArgs.Empty);
    }

    private List<Slot> BuildSlots()
    {
        var slots = new List<Slot>
        {
            new(SlotKind.CreateAgent, "Create", "Create agent", null, null),
            new(SlotKind.Contacts, "All", "Contacts", null, null)
        };
        foreach (var prepared in _prepared)
        {
            var name = prepared.Slot.DisplayName;
            var caption = name.Length <= 14 ? name : name[..13] + "…";
            slots.Add(new Slot(
                SlotKind.Favorite,
                FavoriteRoster.InitialsFor(name),
                caption,
                prepared.Slot,
                prepared.Photo));
        }

        return slots;
    }

    private IBrush Brush(string key, IBrush fallback)
    {
        if (this.TryGetResource(key, ActualThemeVariant, out var value) && value is IBrush brush)
            return brush;
        return fallback;
    }

    private enum SlotKind
    {
        CreateAgent,
        Contacts,
        Favorite
    }

    private sealed record PreparedFavorite(FavoriteSlot Slot, Bitmap? Photo);

    private sealed record Slot(SlotKind Kind, string Mark, string Caption, FavoriteSlot? Favorite, Bitmap? Photo);

    private readonly record struct Hit(Rect Bounds, SlotKind Kind, FavoriteSlot? Favorite);
}
