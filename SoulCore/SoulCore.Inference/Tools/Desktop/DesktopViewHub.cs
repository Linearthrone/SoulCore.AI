namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Latest desktop capture + Victoria's soft-cursor intent for Presence UI.
/// </summary>
public interface IDesktopViewHub
{
    void RecordScreenshot(byte[] imageBytes, string format, int width, int height, string? path);

    void RecordAction(string action, int? cursorX = null, int? cursorY = null);

    DesktopViewSnapshot GetSnapshot();

    /// <summary>Copy of latest image bytes (null if none).</summary>
    byte[]? TryGetImageBytes();
}

public sealed record DesktopViewSnapshot(
    bool HasImage,
    string? ImagePath,
    string Format,
    int Width,
    int Height,
    int? CursorX,
    int? CursorY,
    string? LastAction,
    DateTimeOffset? UpdatedAt,
    bool SoftCursorRestore);

/// <summary>In-memory hub — last screenshot + soft cursor for <c>GET /desktop/view</c>.</summary>
public sealed class DesktopViewHub : IDesktopViewHub
{
    private readonly object _gate = new();
    private byte[]? _imageBytes;
    private string _format = "bmp";
    private string? _path;
    private int _width;
    private int _height;
    private int? _cursorX;
    private int? _cursorY;
    private string? _lastAction;
    private DateTimeOffset? _updatedAt;
    private readonly Func<bool> _softCursor;

    public DesktopViewHub(Func<bool>? softCursor = null)
    {
        _softCursor = softCursor ?? (() => true);
    }

    public void RecordScreenshot(byte[] imageBytes, string format, int width, int height, string? path)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        lock (_gate)
        {
            _imageBytes = imageBytes;
            _format = string.IsNullOrWhiteSpace(format) ? "bmp" : format.Trim().ToLowerInvariant();
            _width = width;
            _height = height;
            _path = path;
            _updatedAt = DateTimeOffset.UtcNow;
            if (string.IsNullOrWhiteSpace(_lastAction))
                _lastAction = "screenshot";
        }
    }

    public void RecordAction(string action, int? cursorX = null, int? cursorY = null)
    {
        lock (_gate)
        {
            _lastAction = string.IsNullOrWhiteSpace(action) ? _lastAction : action.Trim();
            if (cursorX is not null) _cursorX = cursorX;
            if (cursorY is not null) _cursorY = cursorY;
            _updatedAt = DateTimeOffset.UtcNow;
        }
    }

    public DesktopViewSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new DesktopViewSnapshot(
                HasImage: _imageBytes is { Length: > 0 } || !string.IsNullOrWhiteSpace(_path),
                ImagePath: _path,
                Format: _format,
                Width: _width,
                Height: _height,
                CursorX: _cursorX,
                CursorY: _cursorY,
                LastAction: _lastAction,
                UpdatedAt: _updatedAt,
                SoftCursorRestore: _softCursor());
        }
    }

    public byte[]? TryGetImageBytes()
    {
        lock (_gate)
        {
            if (_imageBytes is { Length: > 0 })
                return _imageBytes.ToArray();
            if (!string.IsNullOrWhiteSpace(_path) && File.Exists(_path))
            {
                try
                {
                    _imageBytes = File.ReadAllBytes(_path);
                    return _imageBytes.ToArray();
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
    }
}
