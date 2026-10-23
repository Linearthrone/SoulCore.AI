using Avalonia.Media.Imaging;

namespace House.ChatDesktop.Models;

public sealed class PresenceThreadRow
{
    public string PersonaId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Initials { get; init; } = "";
    public Bitmap? Photo { get; init; }
    public bool ShowInitials => Photo is null;
}
