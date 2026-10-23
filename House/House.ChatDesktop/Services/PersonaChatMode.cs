namespace House.ChatDesktop.Services;

/// <summary>
/// Host chat mode from <c>GET /api/personas/mode</c>. Missing storage on the Host is <see cref="One"/>.
/// </summary>
public static class PersonaChatMode
{
    public const string One = "one";
    public const string Many = "many";

    public static bool IsMany(string? mode) =>
        string.Equals(mode?.Trim(), Many, StringComparison.Ordinal);

    public static string Normalize(string? mode) => IsMany(mode) ? Many : One;

    public static string Label(string? mode) => IsMany(mode) ? "Many" : "One";
}

/// <summary>
/// What one One/Many control is showing. The radial and Settings each own a copy and apply the same board.
/// </summary>
public sealed class PersonaModeControlState
{
    public string ShownMode { get; private set; } = PersonaChatMode.One;

    public string ShownLabel => PersonaChatMode.Label(ShownMode);

    public bool OneSelected => !PersonaChatMode.IsMany(ShownMode);

    public bool ManySelected => PersonaChatMode.IsMany(ShownMode);

    public void Apply(string? mode) => ShownMode = PersonaChatMode.Normalize(mode);
}

/// <summary>
/// Shared mode for the radial chip and Settings → System.
/// Until a GET succeeds, <see cref="Displayed"/> stays <see cref="PersonaChatMode.One"/>.
/// </summary>
public sealed class PersonaModeBoard
{
    public string Displayed { get; private set; } = PersonaChatMode.One;

    public string Label => PersonaChatMode.Label(Displayed);

    public event Action? Changed;

    public void ApplyGet(bool ok, string? mode)
    {
        Displayed = ok ? PersonaChatMode.Normalize(mode) : PersonaChatMode.One;
        Changed?.Invoke();
    }

    /// <summary>Optimistic choice. The caller POSTs, then <see cref="Confirm"/> or <see cref="Restore"/>.</summary>
    public bool TryBeginChoice(string? mode, out string previous)
    {
        previous = Displayed;
        var next = PersonaChatMode.Normalize(mode);
        if (string.Equals(next, Displayed, StringComparison.Ordinal))
            return false;

        Displayed = next;
        Changed?.Invoke();
        return true;
    }

    public void Restore(string? previous)
    {
        Displayed = PersonaChatMode.Normalize(previous);
        Changed?.Invoke();
    }

    public void Confirm(string? mode)
    {
        Displayed = PersonaChatMode.Normalize(mode);
        Changed?.Invoke();
    }
}

/// <summary>
/// Which persona a chat.send carries. Sending never activates a persona.
/// </summary>
public readonly record struct ChatSendPin(bool Send, string? PersonaId)
{
    public bool Activate => false;

    public static ChatSendPin Resolve(string? mode, string? foregroundId, string? openThreadId)
    {
        var foreground = NormalizeId(foregroundId);
        var open = NormalizeId(openThreadId);
        if (PersonaChatMode.IsMany(mode))
        {
            if (open is null)
                return new ChatSendPin(false, null);
            return new ChatSendPin(true, open);
        }

        if (foreground is null)
            return new ChatSendPin(false, null);
        if (open is not null && !string.Equals(open, foreground, StringComparison.OrdinalIgnoreCase))
            return new ChatSendPin(false, null);
        return new ChatSendPin(true, foreground);
    }

    private static string? NormalizeId(string? id)
    {
        var trimmed = (id ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

/// <summary>
/// Focusing a thread may still activate it. A background thread in Many does not.
/// </summary>
public readonly record struct ThreadFocus(bool Activate)
{
    public static ThreadFocus Resolve(string? mode, string? foregroundId, string? focusedId)
    {
        var foreground = NormalizeId(foregroundId);
        var focused = NormalizeId(focusedId);
        if (foreground is not null
            && focused is not null
            && string.Equals(foreground, focused, StringComparison.OrdinalIgnoreCase))
        {
            return new ThreadFocus(false);
        }

        if (PersonaChatMode.IsMany(mode))
            return new ThreadFocus(false);

        return new ThreadFocus(true);
    }

    private static string? NormalizeId(string? id)
    {
        var trimmed = (id ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

/// <summary>
/// HOUSE and the viewport name the foreground persona.
/// Focusing a background thread in Many does not rename them.
/// </summary>
public sealed class ForegroundChromeNames
{
    public ForegroundChromeNames(string? name = null) => ApplyForeground(name);

    public string House { get; private set; } = "Companion";

    public string Viewport { get; private set; } = "Companion";

    public void ApplyForeground(string? name)
    {
        var shown = string.IsNullOrWhiteSpace(name) ? "Companion" : name.Trim();
        House = shown;
        Viewport = shown;
    }

    public (string House, string Viewport) NamesAfterFocus(
        string? mode,
        string? focusedId,
        string? foregroundId,
        string? focusedName)
    {
        _ = focusedName;
        var focus = ThreadFocus.Resolve(mode, foregroundId, focusedId);
        if (focus.Activate)
            return (House, Viewport);

        return (House, Viewport);
    }

    public static string HouseTitle(string? foregroundName)
    {
        var name = string.IsNullOrWhiteSpace(foregroundName) ? "Companion" : foregroundName.Trim();
        return "HOUSE · " + name;
    }

    public static string ViewportTitle(string? foregroundName)
    {
        var name = string.IsNullOrWhiteSpace(foregroundName) ? "Companion" : foregroundName.Trim();
        return name + " · " + ViewportStill.Label;
    }
}
