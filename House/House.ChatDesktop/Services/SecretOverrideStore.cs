using System.Text.Json;

namespace House.ChatDesktop.Services;

/// <summary>
/// PROP-22.3 — write path for API keys Presence has no other store for (Ollama, Soniox, Synthetic).
/// Values live in <c>%LOCALAPPDATA%\HouseVictoria\secret-overrides.json</c> and are pushed into the
/// Presence process env so a Host that Presence starts inherits them. Values are never displayed or
/// logged — the UI only gets <see cref="IsSet"/>.
/// <para>
/// Caveat: <c>SoulCore/.env</c> is loaded by the Host after process env and wins for non-empty keys.
/// An override only takes effect for keys that are blank/absent in .env.
/// </para>
/// </summary>
public static class SecretOverrideStore
{
    public const string OllamaKey = "SOULCORE_OLLAMA_API_KEY";
    public const string SonioxKey = "SONIOX_API_KEY";
    public const string SyntheticKey = "SOULCORE_SYNTHETIC_API_KEY";

    /// <summary>Env names that must also be set whenever the primary one is.</summary>
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.Ordinal)
    {
        [OllamaKey] = Array.Empty<string>(),
        [SonioxKey] = new[] { "SOULCORE_Voice__ApiKey" },
        [SyntheticKey] = new[] { "SYNTHETIC_API_KEY" }
    };

    private static readonly object Gate = new();

    public static IReadOnlyCollection<string> KnownKeys => Aliases.Keys;

    public static string StorePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HouseVictoria",
            "secret-overrides.json");

    public static bool IsKnown(string envName) => Aliases.ContainsKey(envName);

    /// <summary>True when a non-empty override is stored for <paramref name="envName"/>.</summary>
    public static bool HasOverride(string envName, string? path = null) =>
        !string.IsNullOrWhiteSpace(ReadAll(path).GetValueOrDefault(envName));

    /// <summary>True when an override is stored or the process env already carries a value.</summary>
    public static bool IsSet(string envName, string? path = null) =>
        HasOverride(envName, path)
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envName));

    /// <summary>UI label — never the value.</summary>
    public static string Describe(string envName, string? path = null) =>
        IsSet(envName, path) ? "Saved" : "Not set";

    public static bool Save(string envName, string? value, string? path = null)
    {
        if (!Aliases.ContainsKey(envName))
            return false;

        var trimmed = value?.Trim();
        lock (Gate)
        {
            var all = ReadAll(path);
            if (string.IsNullOrEmpty(trimmed))
                all.Remove(envName);
            else
                all[envName] = trimmed;

            if (!WriteAll(all, path))
                return false;
        }

        if (path is null)
            ApplyOne(envName, trimmed);
        return true;
    }

    public static bool Clear(string envName, string? path = null) => Save(envName, null, path);

    /// <summary>
    /// Push stored overrides into process env (call before starting the Host). Returns keys applied.
    /// Existing non-empty process values are left alone — an operator-set env var beats the store.
    /// </summary>
    public static int ApplyToProcessEnv(string? path = null, bool overwrite = false)
    {
        var applied = 0;
        foreach (var (name, value) in ReadAll(path))
        {
            if (!Aliases.ContainsKey(name) || string.IsNullOrWhiteSpace(value))
                continue;
            if (!overwrite && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
                continue;
            ApplyOne(name, value);
            applied++;
        }

        return applied;
    }

    private static void ApplyOne(string name, string? value)
    {
        var v = string.IsNullOrWhiteSpace(value) ? null : value;
        Environment.SetEnvironmentVariable(name, v);
        foreach (var alias in Aliases.GetValueOrDefault(name) ?? Array.Empty<string>())
            Environment.SetEnvironmentVariable(alias, v);
    }

    internal static Dictionary<string, string> ReadAll(string? path = null)
    {
        try
        {
            var file = path ?? StorePath;
            if (!File.Exists(file))
                return new Dictionary<string, string>(StringComparer.Ordinal);

            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
            return parsed is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(parsed, StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static bool WriteAll(Dictionary<string, string> all, string? path)
    {
        try
        {
            var file = path ?? StorePath;
            var dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (all.Count == 0)
            {
                if (File.Exists(file))
                    File.Delete(file);
                return true;
            }

            if (File.Exists(file))
                File.SetAttributes(file, FileAttributes.Normal);
            File.WriteAllText(file, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
            try
            {
                if (OperatingSystem.IsWindows())
                    new FileInfo(file).Attributes |= FileAttributes.Hidden;
            }
            catch
            {
                // best effort
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
