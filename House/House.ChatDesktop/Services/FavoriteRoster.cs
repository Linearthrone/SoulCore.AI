namespace House.ChatDesktop.Services;

public sealed class FavoriteSlot
{
    public string PersonaId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? PhotoPath { get; set; }
}

/// <summary>
/// Left-edge radial favorites. Cap is six. Display names are unique.
/// </summary>
public static class FavoriteRoster
{
    public const int MaxFavorites = 6;

    public static List<FavoriteSlot> Normalize(IEnumerable<FavoriteSlot>? slots)
    {
        var next = new List<FavoriteSlot>();
        if (slots is null)
            return next;

        foreach (var slot in slots)
        {
            if (next.Count >= MaxFavorites)
                break;
            var id = (slot.PersonaId ?? string.Empty).Trim();
            var name = (slot.DisplayName ?? string.Empty).Trim();
            if (id.Length == 0 || name.Length == 0)
                continue;
            if (next.Any(s => string.Equals(s.PersonaId, id, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (next.Any(s => string.Equals(s.DisplayName, name, StringComparison.OrdinalIgnoreCase)))
                continue;
            next.Add(new FavoriteSlot
            {
                PersonaId = id,
                DisplayName = name,
                PhotoPath = string.IsNullOrWhiteSpace(slot.PhotoPath) ? null : slot.PhotoPath.Trim()
            });
        }

        return next;
    }

    public static bool TryAdd(
        IList<FavoriteSlot> slots,
        string personaId,
        string displayName,
        string? photoPath,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(slots);
        error = null;
        var id = (personaId ?? string.Empty).Trim();
        var name = (displayName ?? string.Empty).Trim();
        if (id.Length == 0)
        {
            error = "Persona id is required.";
            return false;
        }

        if (name.Length == 0)
        {
            error = "A favorite needs a name.";
            return false;
        }

        if (slots.Any(s => string.Equals(s.PersonaId, id, StringComparison.OrdinalIgnoreCase)))
        {
            error = "That persona is already a favorite.";
            return false;
        }

        if (slots.Any(s => string.Equals(s.DisplayName, name, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Favorite names must be unique.";
            return false;
        }

        if (slots.Count >= MaxFavorites)
        {
            error = "Favorites are capped at six.";
            return false;
        }

        slots.Add(new FavoriteSlot
        {
            PersonaId = id,
            DisplayName = name,
            PhotoPath = string.IsNullOrWhiteSpace(photoPath) ? null : photoPath.Trim()
        });
        return true;
    }

    public static string InitialsFor(string? name)
    {
        var parts = (name ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return "?";
        if (parts.Length == 1)
        {
            var word = parts[0];
            return word.Length >= 2
                ? word[..2].ToUpperInvariant()
                : word.ToUpperInvariant();
        }

        return string.Concat(
            char.ToUpperInvariant(parts[0][0]),
            char.ToUpperInvariant(parts[^1][0]));
    }
}
