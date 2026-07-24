namespace SoulCore.Config;

/// <summary>
/// Non-secret Memory SQLite knobs. Default DB lives under local app data — never LLMOD Data/.
/// </summary>
public sealed class MemoryOptions
{
    public const string SectionName = "Memory";

    /// <summary>
    /// Absolute or relative path to the SQLite file. Empty = %LOCALAPPDATA%/SoulCore/memory/soulcore_memory.db
    /// </summary>
    public string DbPath { get; set; } = string.Empty;

    public static string ResolveDefaultDbPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "SoulCore", "memory", "soulcore_memory.db");
    }

    public string ResolveDbPath()
    {
        if (string.IsNullOrWhiteSpace(DbPath))
            return ResolveDefaultDbPath();

        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(DbPath.Trim()));
    }
}
