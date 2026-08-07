namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Latest capture Victoria actually took (desktop / eyes / browser) for Presence UI.
/// </summary>
public interface IDesktopViewHub
{
    void RecordScreenshot(
        byte[] imageBytes,
        string format,
        int width,
        int height,
        string? path,
        string? source = null,
        string? action = null);

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
    bool SoftCursorRestore,
    string Source);

/// <summary>In-memory hub — last real capture for <c>GET /desktop/view</c>.</summary>
public sealed class DesktopViewHub : IDesktopViewHub
{
    public const string SourceDesktop = "desktop";
    public const string SourceEyes = "eyes";
    public const string SourceBrowser = "browser";

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
    private string _source = SourceDesktop;
    private readonly Func<bool> _softCursor;

    public DesktopViewHub(Func<bool>? softCursor = null)
    {
        _softCursor = softCursor ?? (() => true);
    }

    public void RecordScreenshot(
        byte[] imageBytes,
        string format,
        int width,
        int height,
        string? path,
        string? source = null,
        string? action = null)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
            return;

        lock (_gate)
        {
            _imageBytes = imageBytes;
            _format = string.IsNullOrWhiteSpace(format) ? "bmp" : format.Trim().ToLowerInvariant();
            _width = width;
            _height = height;
            _path = path;
            _source = NormalizeSource(source);
            _updatedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(action))
                _lastAction = action.Trim();
            else if (string.IsNullOrWhiteSpace(_lastAction))
                _lastAction = $"capture ({_source})";
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
                SoftCursorRestore: _softCursor(),
                Source: _source);
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

    /// <summary>
    /// Pull image bytes from tool Data (anonymous object / JsonElement) and record them.
    /// Returns true when Presence was updated with a real frame.
    /// </summary>
    public static bool TryRecordFromToolData(
        IDesktopViewHub? hub,
        object? data,
        string source,
        string action,
        string? pathHint = null)
    {
        if (hub is null || data is null)
            return false;

        if (!TryExtractImage(data, out var bytes, out var format, out var width, out var height, out var path))
            return false;

        hub.RecordScreenshot(
            bytes,
            format,
            width,
            height,
            path ?? pathHint,
            source,
            action);
        return true;
    }

    private static bool TryExtractImage(
        object data,
        out byte[] bytes,
        out string format,
        out int width,
        out int height,
        out string? path)
    {
        bytes = Array.Empty<byte>();
        format = "png";
        width = 0;
        height = 0;
        path = null;

        try
        {
            if (data is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object)
                return TryExtractFromJson(je, out bytes, out format, out width, out height, out path);

            var type = data.GetType();
            var bytesProp = type.GetProperty("bytes") ?? type.GetProperty("Bytes");
            if (bytesProp?.GetValue(data) is byte[] arr && arr.Length > 0)
            {
                bytes = arr;
            }
            else if (bytesProp?.GetValue(data) is string b64 && !string.IsNullOrWhiteSpace(b64))
            {
                bytes = Convert.FromBase64String(b64);
            }
            else
            {
                // Path-only payload: load file if present.
                var pathProp = type.GetProperty("path") ?? type.GetProperty("Path")
                    ?? type.GetProperty("screenshot_path") ?? type.GetProperty("screenshotPath");
                path = pathProp?.GetValue(data) as string;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    bytes = File.ReadAllBytes(path);
            }

            if (bytes.Length == 0)
                return false;

            var fmtProp = type.GetProperty("format") ?? type.GetProperty("Format");
            if (fmtProp?.GetValue(data) is string fmt && !string.IsNullOrWhiteSpace(fmt))
                format = fmt;

            width = ReadIntProp(type, data, "width", "Width");
            height = ReadIntProp(type, data, "height", "Height");
            if (string.IsNullOrWhiteSpace(path))
            {
                var pathProp = type.GetProperty("path") ?? type.GetProperty("Path");
                path = pathProp?.GetValue(data) as string;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryExtractFromJson(
        System.Text.Json.JsonElement je,
        out byte[] bytes,
        out string format,
        out int width,
        out int height,
        out string? path)
    {
        bytes = Array.Empty<byte>();
        format = "png";
        width = 0;
        height = 0;
        path = null;

        if (je.TryGetProperty("bytes", out var b))
        {
            if (b.ValueKind == System.Text.Json.JsonValueKind.String)
                bytes = Convert.FromBase64String(b.GetString()!);
            else if (b.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                // rare numeric array — skip
            }
        }

        if (je.TryGetProperty("path", out var p) || je.TryGetProperty("screenshot_path", out p))
            path = p.GetString();

        if (bytes.Length == 0 && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            bytes = File.ReadAllBytes(path);

        if (bytes.Length == 0)
            return false;

        if (je.TryGetProperty("format", out var f) && f.ValueKind == System.Text.Json.JsonValueKind.String)
            format = f.GetString() ?? format;
        if (je.TryGetProperty("width", out var w) && w.TryGetInt32(out var wi))
            width = wi;
        if (je.TryGetProperty("height", out var h) && h.TryGetInt32(out var hi))
            height = hi;
        return true;
    }

    private static int ReadIntProp(Type type, object data, params string[] names)
    {
        foreach (var name in names)
        {
            var prop = type.GetProperty(name);
            if (prop?.GetValue(data) is int i)
                return i;
            if (prop?.GetValue(data) is long l)
                return (int)l;
        }

        return 0;
    }

    private static string NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return SourceDesktop;
        var s = source.Trim().ToLowerInvariant();
        return s switch
        {
            "eye" or "eyes" or "unreal" or "body" => SourceEyes,
            "browser" or "tab" => SourceBrowser,
            "desktop" or "screen" or "cua" or "native" or "hermes" => SourceDesktop,
            _ => s
        };
    }
}
