namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// In-memory near-live view for Presence "Her screen" (FED-196).
/// Playwright Chromium or VirtualBox guest framebuffer — not persisted to the desktop gallery.
/// </summary>
public interface IVictoriaBrowserViewHub
{
    void Publish(
        byte[] jpegOrPng,
        string? url,
        string? title,
        string? lastAction,
        string? waitingOnYou = null,
        string? backend = null,
        int? frameWidth = null,
        int? frameHeight = null);

    /// <summary>
    /// Soft cursor for Presence overlay (VM HWND / guest JPEG). Same palette as PlaywrightClickCursor.
    /// <paramref name="state"/> is <c>idle</c> or <c>click</c>.
    /// </summary>
    void RecordCursor(int x, int y, string state = "idle");

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
    public int? CursorX { get; init; }
    public int? CursorY { get; init; }
    /// <summary><c>idle</c> (pink) or <c>click</c> (teal flash).</summary>
    public string? CursorState { get; init; }
    public DateTimeOffset? CursorAt { get; init; }
    public int FrameWidth { get; init; }
    public int FrameHeight { get; init; }
}

public sealed class VictoriaBrowserViewHub : IVictoriaBrowserViewHub
{
    public const string BackendPlaywright = "playwright";
    public const string BackendVboxGuest = "vbox-guest";
    public const string CursorIdle = "idle";
    public const string CursorClick = "click";

    private readonly object _gate = new();
    private byte[]? _bytes;
    private string _contentType = "image/jpeg";
    private string? _url;
    private string? _title;
    private string? _lastAction;
    private string? _waiting;
    private string _backend = BackendPlaywright;
    private DateTimeOffset? _updated;
    private int? _cursorX;
    private int? _cursorY;
    private string _cursorState = CursorIdle;
    private DateTimeOffset? _cursorAt;
    private int _frameWidth;
    private int _frameHeight;

    public void Publish(
        byte[] jpegOrPng,
        string? url,
        string? title,
        string? lastAction,
        string? waitingOnYou = null,
        string? backend = null,
        int? frameWidth = null,
        int? frameHeight = null)
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
            if (!string.IsNullOrWhiteSpace(backend))
                _backend = backend.Trim();
            if (frameWidth is > 0) _frameWidth = frameWidth.Value;
            if (frameHeight is > 0) _frameHeight = frameHeight.Value;
            _updated = DateTimeOffset.UtcNow;
        }
    }

    public void RecordCursor(int x, int y, string state = CursorIdle)
    {
        lock (_gate)
        {
            _cursorX = x;
            _cursorY = y;
            _cursorState = NormalizeCursorState(state);
            _cursorAt = DateTimeOffset.UtcNow;
            _updated = _cursorAt;
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
                Backend = _backend,
                UpdatedUtc = _updated,
                CursorX = _cursorX,
                CursorY = _cursorY,
                CursorState = _cursorX is null ? null : _cursorState,
                CursorAt = _cursorAt,
                FrameWidth = _frameWidth,
                FrameHeight = _frameHeight
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

    /// <summary>
    /// Publish guest / desktop tool image bytes into Her screen (same pixels as desktop_screenshot).
    /// Hover coords on Presence then match guest framebuffer origin 0,0.
    /// </summary>
    public static bool TryPublishFromToolData(
        IVictoriaBrowserViewHub? hub,
        object? data,
        string lastAction,
        string backend = BackendVboxGuest,
        string? url = null,
        string? title = null)
    {
        if (hub is null || data is null)
            return false;

        if (!Desktop.DesktopViewHub.TryGetImageBytesFromToolData(data, out var bytes))
            return false;

        int? w = null;
        int? h = null;
        if (Desktop.DesktopViewHub.TryGetSizeFromToolData(data, out var sw, out var sh))
        {
            w = sw;
            h = sh;
        }

        hub.Publish(bytes, url, title ?? "victoria-sandbox", lastAction, waitingOnYou: null, backend, w, h);
        // Seed soft-cursor so Her screen shows pink even before the first click/aim.
        var snap = hub.GetSnapshot();
        if (snap.CursorX is null && w is > 0 && h is > 0)
            hub.RecordCursor(w.Value / 2, h.Value / 2, CursorIdle);
        return true;
    }

    public static string NormalizeCursorState(string? state)
    {
        if (string.Equals(state, CursorClick, StringComparison.OrdinalIgnoreCase))
            return CursorClick;
        return CursorIdle;
    }

    /// <summary>Whether Presence should draw the pink/teal soft cursor (not Playwright DOM/burn-in).</summary>
    public static bool WantsPresenceSoftCursor(string? backend, string? embedSurface) =>
        string.Equals(embedSurface, "vm", StringComparison.OrdinalIgnoreCase)
        || string.Equals(backend, BackendVboxGuest, StringComparison.OrdinalIgnoreCase);
}
