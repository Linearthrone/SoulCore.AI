namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-4: honest "doing now" for Presence HUD — never SoulLoop want slogans.
/// </summary>
public interface IPresenceActivityHub
{
    void NoteChat(string side);

    void NoteTool(string phrase);

    PresenceActivitySnapshot GetSnapshot();
}

public sealed record PresenceActivitySnapshot(
    string Phrase,
    string Source,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Prefers recent chat, then recent desktop/tool act from <see cref="Tools.Desktop.IDesktopViewHub"/>,
/// then a short life line when Kurt is silent.
/// </summary>
public sealed class PresenceActivityHub : IPresenceActivityHub
{
    private readonly Tools.Desktop.IDesktopViewHub _desktop;
    private readonly object _gate = new();
    private string? _chatPhrase;
    private DateTimeOffset? _chatAt;
    private string? _toolPhrase;
    private DateTimeOffset? _toolAt;

    public PresenceActivityHub(Tools.Desktop.IDesktopViewHub desktop)
    {
        _desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
    }

    public void NoteChat(string side)
    {
        var phrase = side.Equals("user", StringComparison.OrdinalIgnoreCase)
            ? "Listening to Kurt"
            : "In conversation";
        lock (_gate)
        {
            _chatPhrase = phrase;
            _chatAt = DateTimeOffset.UtcNow;
        }
    }

    public void NoteTool(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return;
        lock (_gate)
        {
            _toolPhrase = phrase.Trim();
            _toolAt = DateTimeOffset.UtcNow;
        }
    }

    public PresenceActivitySnapshot GetSnapshot()
    {
        string? chatPhrase;
        DateTimeOffset? chatAt;
        string? toolPhrase;
        DateTimeOffset? toolAt;
        lock (_gate)
        {
            chatPhrase = _chatPhrase;
            chatAt = _chatAt;
            toolPhrase = _toolPhrase;
            toolAt = _toolAt;
        }

        var now = DateTimeOffset.UtcNow;
        if (chatAt is { } c && now - c < TimeSpan.FromMinutes(3) && !string.IsNullOrWhiteSpace(chatPhrase))
            return new PresenceActivitySnapshot(chatPhrase!, "chat", c);

        if (toolAt is { } t && now - t < TimeSpan.FromMinutes(5) && !string.IsNullOrWhiteSpace(toolPhrase))
            return new PresenceActivitySnapshot(toolPhrase!, "tool", t);

        var desk = _desktop.GetSnapshot();
        if (desk.UpdatedAt is { } u
            && now - u < TimeSpan.FromMinutes(5)
            && !string.IsNullOrWhiteSpace(desk.LastAction))
        {
            return new PresenceActivitySnapshot(
                HumanizeDesktop(desk.Source, desk.LastAction!),
                desk.Source ?? "desktop",
                u);
        }

        if (chatAt is { } c2 && now - c2 < TimeSpan.FromMinutes(30))
            return new PresenceActivitySnapshot("With herself", "life", c2);

        return new PresenceActivitySnapshot("With herself", "life", null);
    }

    public static string HumanizeDesktop(string? source, string lastAction)
    {
        var src = (source ?? "desktop").Trim().ToLowerInvariant();
        var act = lastAction.Trim();
        if (act.Length > 80)
            act = act[..77] + "…";

        return src switch
        {
            "eyes" or "eye" => string.IsNullOrWhiteSpace(act) ? "Looking around" : $"Looking — {act}",
            "browser" => string.IsNullOrWhiteSpace(act) ? "Browsing" : $"Browsing — {act}",
            _ => string.IsNullOrWhiteSpace(act) ? "Using the desktop" : act
        };
    }
}
