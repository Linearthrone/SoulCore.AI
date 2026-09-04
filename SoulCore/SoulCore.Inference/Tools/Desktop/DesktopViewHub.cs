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

    /// <summary>Temp gallery root where every capture is written (BED-186).</summary>
    string GalleryDirectory { get; }

    /// <summary>Load a gallery file by basename only (path traversal rejected).</summary>
    byte[]? TryGetGalleryImageBytes(string fileName);
}

/// <summary>One persisted capture in the temp Presence gallery.</summary>
public sealed record DesktopViewGalleryEntry(
    string FileName,
    string Path,
    string Source,
    string Format,
    int Width,
    int Height,
    DateTimeOffset CapturedAt,
    string? Action);

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
    string Source,
    string? GalleryDir = null,
    IReadOnlyList<DesktopViewGalleryEntry>? Recent = null);

/// <summary>
/// In-memory hub for the latest frame + ring-buffer gallery on disk for Presence.
/// </summary>
public sealed class DesktopViewHub : IDesktopViewHub
{
    public const string SourceDesktop = "desktop";
    public const string SourceEyes = "eyes";
    public const string SourceBrowser = "browser";

    /// <summary>Temp gallery root where every capture is written (BED-186).</summary>
    public const int MaxGalleryItems = 48;

    /// <summary>
    /// PROP-4: memory-bound stills live here (copies). Presence Folder button must never open this.
    /// </summary>
    public static string DefaultMemorySightDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoulCore",
            "memory",
            "sight");

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
    private readonly string _galleryDir;
    private readonly List<DesktopViewGalleryEntry> _gallery = new();
    private long _gallerySeq;

    public DesktopViewHub(Func<bool>? softCursor = null, string? galleryDirectory = null)
    {
        _softCursor = softCursor ?? (() => true);
        _galleryDir = string.IsNullOrWhiteSpace(galleryDirectory)
            ? DefaultGalleryDirectory()
            : galleryDirectory.Trim();
    }

    public string GalleryDirectory => _galleryDir;

    public static string DefaultGalleryDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoulCore",
            "scratch",
            "presence-gallery");

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

        var fmt = string.IsNullOrWhiteSpace(format) ? "bmp" : format.Trim().ToLowerInvariant();
        var src = NormalizeSource(source);
        var act = !string.IsNullOrWhiteSpace(action)
            ? action.Trim()
            : $"capture ({src})";
        var capturedAt = DateTimeOffset.UtcNow;

        var galleryPath = TryPersistGallery(imageBytes, fmt, src, capturedAt);
        var diskPath = galleryPath ?? path;

        lock (_gate)
        {
            _imageBytes = imageBytes;
            _format = fmt;
            _width = width;
            _height = height;
            _path = diskPath;
            _source = src;
            _updatedAt = capturedAt;
            _lastAction = act;

            if (!string.IsNullOrWhiteSpace(galleryPath))
            {
                var entry = new DesktopViewGalleryEntry(
                    FileName: Path.GetFileName(galleryPath),
                    Path: galleryPath,
                    Source: src,
                    Format: fmt,
                    Width: width,
                    Height: height,
                    CapturedAt: capturedAt,
                    Action: act);
                _gallery.Insert(0, entry);
                PruneGalleryLocked();
            }
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
                Source: _source,
                GalleryDir: _galleryDir,
                Recent: _gallery.ToArray());
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

    public byte[]? TryGetGalleryImageBytes(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name)
            || name.Contains("..", StringComparison.Ordinal)
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return null;

        string full;
        lock (_gate)
        {
            var hit = _gallery.FirstOrDefault(g =>
                string.Equals(g.FileName, name, StringComparison.OrdinalIgnoreCase));
            full = hit?.Path ?? Path.Combine(_galleryDir, name);
        }

        // Must stay under gallery root.
        var rootFull = Path.GetFullPath(_galleryDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(full);
        if (!candidate.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                Path.GetDirectoryName(candidate),
                Path.GetFullPath(_galleryDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            return null;

        if (!File.Exists(candidate))
            return null;

        try
        {
            return File.ReadAllBytes(candidate);
        }
        catch
        {
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

    private string? TryPersistGallery(
        byte[] bytes,
        string format,
        string source,
        DateTimeOffset capturedAt)
    {
        try
        {
            Directory.CreateDirectory(_galleryDir);
            var ext = format switch
            {
                "png" => "png",
                "jpg" or "jpeg" => "jpg",
                "webp" => "webp",
                _ => "bmp"
            };
            var seq = Interlocked.Increment(ref _gallerySeq);
            var name = $"{capturedAt.UtcDateTime:yyyyMMdd-HHmmssfff}_{seq:D4}_{source}.{ext}";
            var full = Path.Combine(_galleryDir, name);
            File.WriteAllBytes(full, bytes);
            return full;
        }
        catch
        {
            return null;
        }
    }

    private void PruneGalleryLocked()
    {
        while (_gallery.Count > MaxGalleryItems)
        {
            var oldest = _gallery[^1];
            _gallery.RemoveAt(_gallery.Count - 1);
            try
            {
                if (File.Exists(oldest.Path))
                    File.Delete(oldest.Path);
            }
            catch
            {
                // best-effort cleanup
            }
        }
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
