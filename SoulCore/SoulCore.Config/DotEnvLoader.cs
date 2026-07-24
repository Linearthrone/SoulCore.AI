namespace SoulCore.Config;

/// <summary>
/// Loads local <c>.env</c> into the process environment for <c>SOULCORE_*</c> keys only.
/// Never logs secret values. Does not overwrite existing non-empty process env (shell wins).
/// </summary>
public static class DotEnvLoader
{
    public const string KeyPrefix = "SOULCORE_";

    /// <summary>
    /// Apply <c>SOULCORE_*</c> entries from <paramref name="explicitPath"/> or a resolved <c>.env</c>.
    /// </summary>
    /// <returns>Count of keys newly set on the process environment.</returns>
    public static int TryLoad(string? explicitPath = null)
    {
        var path = explicitPath ?? ResolveEnvFilePath();
        if (path is null || !File.Exists(path))
            return 0;

        var applied = 0;
        foreach (var rawLine in File.ReadLines(path))
        {
            if (!TryParseLine(rawLine, out var key, out var value))
                continue;

            if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
                continue;

            var existing = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(existing))
                continue;

            Environment.SetEnvironmentVariable(key, value);
            applied++;
        }

        return applied;
    }

    /// <summary>
    /// Walks from cwd and base directory upward looking for <c>.env</c>
    /// (or <c>SoulCore/.env</c> when starting from the repo root).
    /// </summary>
    public static string? ResolveEnvFilePath()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(start))
                continue;

            DirectoryInfo? dir;
            try
            {
                dir = new DirectoryInfo(Path.GetFullPath(start));
            }
            catch
            {
                continue;
            }

            while (dir is not null)
            {
                var direct = Path.Combine(dir.FullName, ".env");
                if (File.Exists(direct))
                    return direct;

                var nested = Path.Combine(dir.FullName, "SoulCore", ".env");
                if (File.Exists(nested))
                    return nested;

                dir = dir.Parent;
            }
        }

        return null;
    }

    internal static bool TryParseLine(string rawLine, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(rawLine))
            return false;

        var line = rawLine.Trim();
        if (line.StartsWith('#'))
            return false;

        var eq = line.IndexOf('=');
        if (eq <= 0)
            return false;

        key = line[..eq].Trim();
        if (key.Length == 0)
            return false;

        value = Unquote(line[(eq + 1)..].Trim());
        return true;
    }

    private static string Unquote(string raw)
    {
        if (raw.Length >= 2)
        {
            if ((raw[0] == '"' && raw[^1] == '"') || (raw[0] == '\'' && raw[^1] == '\''))
                return raw[1..^1];
        }

        return raw;
    }
}
