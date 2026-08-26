namespace House.ChatDesktop.Services;

/// <summary>
/// Resolves <c>SOULCORE_COMPANION_API_TOKEN</c> for Host /ws auth when the token is set.
/// Never logs the value.
/// </summary>
public static class CompanionToken
{
    public const string EnvName = "SOULCORE_COMPANION_API_TOKEN";

    /// <summary>
    /// Load SOULCORE_* keys from SoulCore/.env into the process.
    /// <b>.env wins</b> over stale Process/User-inherited values — same footgun as Host
    /// (HTTP /health looks "up" while /ws 401s with the wrong Bearer).
    /// </summary>
    /// <returns>Count of keys set or updated.</returns>
    public static int TryLoadFromEnvFile()
    {
        var envPath = FindSoulCoreEnvFile();
        if (envPath is null)
            return 0;

        var applied = 0;
        foreach (var line in File.ReadLines(envPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && trimmed[0] == '\uFEFF')
                trimmed = trimmed[1..].TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;
            var eq = trimmed.IndexOf('=');
            if (eq < 1)
                continue;
            var key = trimmed[..eq].Trim();
            if (!key.StartsWith("SOULCORE_", StringComparison.Ordinal))
                continue;

            var value = Unquote(trimmed[(eq + 1)..].Trim());
            if (string.IsNullOrEmpty(value))
            {
                Environment.SetEnvironmentVariable(key, null);
                applied++;
                continue;
            }

            var existing = Environment.GetEnvironmentVariable(key);
            if (string.Equals(existing, value, StringComparison.Ordinal))
                continue;

            Environment.SetEnvironmentVariable(key, value);
            applied++;
        }

        return applied;
    }

    public static string? Resolve()
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvName);
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv.Trim();
    }

    /// <summary>Safe for UI / logs — never includes the secret.</summary>
    public static string DescribePresence()
    {
        var token = Resolve();
        return token is null
            ? "tokenPresent=false tokenLen=0"
            : $"tokenPresent=true tokenLen={token.Length}";
    }

    private static string Unquote(string raw)
    {
        if (raw.Length >= 2
            && ((raw[0] == '"' && raw[^1] == '"') || (raw[0] == '\'' && raw[^1] == '\'')))
        {
            return raw[1..^1];
        }

        return raw;
    }

    private static string? FindSoulCoreEnvFile()
    {
        // bin/{config}/net8.0 → House.ChatDesktop → House → repo root → SoulCore/.env
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "SoulCore", ".env");
            if (File.Exists(candidate))
                return candidate;
            candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate) && dir.Name.Equals("SoulCore", StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        // Also walk from cwd (dotnet run from repo root).
        try
        {
            dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "SoulCore", ".env");
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
