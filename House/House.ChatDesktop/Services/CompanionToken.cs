namespace House.ChatDesktop.Services;

/// <summary>
/// Resolves <c>SOULCORE_COMPANION_API_TOKEN</c> for Host /ws auth when the token is set.
/// Never logs the value.
/// </summary>
public static class CompanionToken
{
    public const string EnvName = "SOULCORE_COMPANION_API_TOKEN";

    /// <summary>
    /// If the process env is empty, load SOULCORE_* keys from SoulCore/.env (repo layout).
    /// Does not overwrite non-empty process values.
    /// </summary>
    public static void TryLoadFromEnvFile()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvName)))
            return;

        var envPath = FindSoulCoreEnvFile();
        if (envPath is null)
            return;

        foreach (var line in File.ReadLines(envPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;
            var eq = trimmed.IndexOf('=');
            if (eq < 1)
                continue;
            var key = trimmed[..eq].Trim();
            if (!key.StartsWith("SOULCORE_", StringComparison.Ordinal))
                continue;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                continue;
            var value = trimmed[(eq + 1)..].Trim();
            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                if (value.Length >= 2)
                    value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public static string? Resolve()
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvName);
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv.Trim();
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

        return null;
    }
}
