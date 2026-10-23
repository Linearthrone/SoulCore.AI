namespace SoulCore.Host.Persona;

/// <summary>
/// Host chat mode for PROP-16.6. <see cref="One"/> is the only persona that
/// chats. <see cref="Many"/> lets several personas chat; tools stay with the
/// foreground. Missing storage reads as <see cref="One"/>.
/// </summary>
public interface IHostChatMode
{
    public const string One = "one";
    public const string Many = "many";

    /// <summary>
    /// <see cref="One"/> or <see cref="Many"/>. Synchronous so a chat turn can
    /// copy it before any await.
    /// </summary>
    string Current { get; }

    /// <summary>Persist <paramref name="mode"/>. Only <see cref="One"/> or <see cref="Many"/>.</summary>
    Task SetAsync(string mode, CancellationToken cancellationToken = default);
}

/// <summary>
/// File-backed mode beside the persona root (<c>chat-mode.txt</c>).
/// A missing or unrecognized file reads as <see cref="IHostChatMode.One"/>
/// and does not create the file.
/// </summary>
public sealed class HostChatMode : IHostChatMode
{
    private readonly string _path;
    private readonly object _gate = new();
    private string? _cached;

    public HostChatMode(string personasRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personasRoot);
        _path = global::System.IO.Path.Combine(
            global::System.IO.Path.GetFullPath(personasRoot),
            "chat-mode.txt");
    }

    public string Current
    {
        get
        {
            lock (_gate)
            {
                if (_cached is not null)
                    return _cached;

                _cached = ReadStoredOrOne();
                return _cached;
            }
        }
    }

    public Task SetAsync(string mode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = (mode ?? string.Empty).Trim();
        if (normalized is not (IHostChatMode.One or IHostChatMode.Many))
            throw new ArgumentException("mode must be \"one\" or \"many\".", nameof(mode));

        lock (_gate)
        {
            var dir = global::System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_path, normalized + Environment.NewLine);
            _cached = normalized;
        }

        return Task.CompletedTask;
    }

    private string ReadStoredOrOne()
    {
        if (!File.Exists(_path))
            return IHostChatMode.One;

        var text = File.ReadAllText(_path).Trim();
        return text is IHostChatMode.One or IHostChatMode.Many
            ? text
            : IHostChatMode.One;
    }
}
