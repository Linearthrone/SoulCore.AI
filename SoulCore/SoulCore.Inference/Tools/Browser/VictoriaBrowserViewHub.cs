namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// In-memory near-live view of Victoria's Playwright browser (FED-196).
/// Loopback Host serves this — not persisted to the desktop screenshot gallery.
/// </summary>
public interface IVictoriaBrowserViewHub
{
    void Publish(byte[] jpegOrPng, string? url, string? title, string? lastAction, string? waitingOnYou = null);
    VictoriaBrowserViewSnapshot GetSnapshot();
    bool TryGetImageBytes(out byte[]? bytes, out string contentType);
}

public sealed class VictoriaBrowserViewSnapshot
{
    public bool HasImage { get; init; }
    public string? Url { get; init; }
    public string? Title { get; init; }
    public string? LastAction { get; init; }
    public string? WaitingOnYou { get; init; }
    public string Backend { get; init; } = "playwright";
    public DateTimeOffset? UpdatedUtc { get; init; }
}

public sealed class VictoriaBrowserViewHub : IVictoriaBrowserViewHub
{
    private readonly object _gate = new();
    private byte[]? _bytes;
    private string _contentType = "image/jpeg";
    private string? _url;
    private string? _title;
    private string? _lastAction;
    private string? _waiting;
    private DateTimeOffset? _updated;

    public void Publish(byte[] jpegOrPng, string? url, string? title, string? lastAction, string? waitingOnYou = null)
    {
        if (jpegOrPng is null || jpegOrPng.Length == 0)
            return;
        lock (_gate)
        {
            _bytes = jpegOrPng;
            _contentType = jpegOrPng.Length >= 3 && jpegOrPng[0] == 0xFF && jpegOrPng[1] == 0xD8
                ? "image/jpeg"
                : "image/png";
            if (url is not null) _url = url;
            if (title is not null) _title = title;
            if (lastAction is not null) _lastAction = lastAction;
            if (waitingOnYou is not null) _waiting = waitingOnYou;
            _updated = DateTimeOffset.UtcNow;
        }
    }

    public VictoriaBrowserViewSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new VictoriaBrowserViewSnapshot
            {
                HasImage = _bytes is { Length: > 0 },
                Url = _url,
                Title = _title,
                LastAction = _lastAction,
                WaitingOnYou = _waiting,
                UpdatedUtc = _updated
            };
        }
    }

    public bool TryGetImageBytes(out byte[]? bytes, out string contentType)
    {
        lock (_gate)
        {
            bytes = _bytes;
            contentType = _contentType;
            return bytes is { Length: > 0 };
        }
    }
}
