using System.IO;
using System.Text.Json;

namespace House.ChatDesktop.Services;

/// <summary>Local-only shell preferences (not SoulCore settings store).</summary>
public sealed class LocalUiSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public const double DefaultWindowWidth = 1180;
    public const double DefaultWindowHeight = 780;
    public const double MinWindowWidth = 900;
    public const double MinWindowHeight = 600;

    public string DisplayName { get; set; } = "Victoria";

    public NotificationSettings Notifications { get; set; } = new();

    /// <summary>PROP-12.1: restored Presence shell geometry.</summary>
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
    public bool WindowMaximized { get; set; }

    /// <summary>
    /// PROP-4.2 optional update feed override (GitHub repo URL or HTTP/file path).
    /// Empty → GitHub Releases for Linearthrone/SoulCore.AI.
    /// </summary>
    public string? UpdateFeedUrl { get; set; }

    /// <summary>
    /// Absolute path to the SoulCore checkout (folder with ALLSTART.ps1).
    /// Required when Presence is installed outside the repo (Velopack Setup.exe).
    /// Override also via env HOUSE_SOULCORE_REPO.
    /// </summary>
    public string? SoulCoreRepoRoot { get; set; }

    /// <summary>
    /// When true (default), Presence starts Ollama + Host on open if they are down.
    /// </summary>
    public bool AutoStartStack { get; set; } = true;

    public static string StorePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HouseVictoria",
            "ui-settings.json");

    public static LocalUiSettings Load()
    {
        try
        {
            var path = StorePath;
            if (!File.Exists(path))
            {
                return new LocalUiSettings();
            }

            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<LocalUiSettings>(json, JsonOptions);
            if (loaded is null)
            {
                return new LocalUiSettings();
            }

            loaded.Normalize();
            return loaded;
        }
        catch
        {
            return new LocalUiSettings();
        }
    }

    public void Save()
    {
        Normalize();
        var dir = Path.GetDirectoryName(StorePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(StorePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public void Normalize()
    {
        // The shell header is her name. A stored operator name must not replace it.
        DisplayName = "Victoria";

        if (WindowWidth is double w)
            WindowWidth = Clamp(w, MinWindowWidth, 4000);
        if (WindowHeight is double h)
            WindowHeight = Clamp(h, MinWindowHeight, 3000);
    }

    public double ResolvedWindowWidth() =>
        Clamp(WindowWidth ?? DefaultWindowWidth, MinWindowWidth, 4000);

    public double ResolvedWindowHeight() =>
        Clamp(WindowHeight ?? DefaultWindowHeight, MinWindowHeight, 3000);

    private static double Clamp(double value, double min, double max) =>
        Math.Min(max, Math.Max(min, value));
}
