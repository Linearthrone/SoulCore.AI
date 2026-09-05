using System.Collections.Concurrent;

namespace SoulCore.Core.Sqlite;

/// <summary>
/// Process-wide async gate keyed by SQLite file path. Serializes concurrent access when
/// multiple long-lived connections (e.g. <c>SqliteMemoryStore</c> + <c>CharterService</c>)
/// share the same LocalAppData database file.
/// </summary>
public static class SqlitePathGate
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Default busy-timeout for SQLite connections (milliseconds).</summary>
    public const int DefaultBusyTimeoutMs = 5000;

    /// <summary>Returns the shared gate for <paramref name="dbPath"/> (normalized absolute path).</summary>
    public static SemaphoreSlim ForPath(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("dbPath must be non-empty.", nameof(dbPath));

        return Gates.GetOrAdd(NormalizePath(dbPath), _ => new SemaphoreSlim(1, 1));
    }

    internal static string NormalizePath(string dbPath)
        => Path.GetFullPath(dbPath.Trim());
}
