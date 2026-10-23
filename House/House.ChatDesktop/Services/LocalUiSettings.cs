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
    public const double DefaultSideColumnWidth = 260;
    public const double DefaultSightRowHeight = 200;
    public const double MinWindowWidth = 900;
    public const double MinWindowHeight = 600;
    public const double MinChatWidth = 320;
    public const double MinSideColumnWidth = 200;
    public const double MinSightRowHeight = 120;
    public const double MinBrowserRowHeight = 120;

    public string DisplayName { get; set; } = "Companion";

    public NotificationSettings Notifications { get; set; } = new();

    /// <summary>PROP-12.1: restored Presence shell geometry.</summary>
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
    public bool WindowMaximized { get; set; }

    /// <summary>Right column width (Her browser + What she saw).</summary>
    public double? SideColumnWidth { get; set; }

    /// <summary>Sight panel row height inside the right column.</summary>
    public double? SightRowHeight { get; set; }

    /// <summary>
    /// PROP-4.2 optional update feed override (GitHub repo URL or HTTP/file path).
    /// Empty → GitHub Releases for House-VictoriAI/SoulCore.AI_CreatorEdition.
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

    /// <summary>PROP-16.1: "Light" or "Dark".</summary>
    public string Theme { get; set; } = GlassChrome.DarkTheme;

    /// <summary>Chrome background alpha. Floor is <see cref="GlassChrome.OpacityFloor"/>.</summary>
    public double ChromeOpacity { get; set; } = GlassChrome.DefaultOpacity;

    /// <summary>
    /// PROP-16.2 viewport guest alpha. Independent of <see cref="ChromeOpacity"/>.
    /// 1 keeps the guest fully opaque. Missing JSON stays 1, not the chrome floor.
    /// </summary>
    public double ViewOpacity { get; set; } = ViewportInputPolicy.DefaultViewOpacity;

    /// <summary>Radial favorites. At most six, unique display names.</summary>
    public List<FavoriteSlot> Favorites { get; set; } = new();

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
        if (string.IsNullOrWhiteSpace(DisplayName))
            DisplayName = "Companion";

        if (WindowWidth is double w)
            WindowWidth = Clamp(w, MinWindowWidth, 4000);
        if (WindowHeight is double h)
            WindowHeight = Clamp(h, MinWindowHeight, 3000);
        if (SideColumnWidth is double side)
            SideColumnWidth = Clamp(side, MinSideColumnWidth, 900);
        if (SightRowHeight is double sight)
            SightRowHeight = Clamp(sight, MinSightRowHeight, 800);

        Theme = GlassChrome.IsLightTheme(Theme) ? GlassChrome.LightTheme : GlassChrome.DarkTheme;
        ChromeOpacity = GlassChrome.ClampOpacity(ChromeOpacity);
        ViewOpacity = ViewportInputPolicy.ClampViewOpacity(ViewOpacity);
        Favorites = FavoriteRoster.Normalize(Favorites);
    }

    public double ResolvedWindowWidth() =>
        Clamp(WindowWidth ?? DefaultWindowWidth, MinWindowWidth, 4000);

    public double ResolvedWindowHeight() =>
        Clamp(WindowHeight ?? DefaultWindowHeight, MinWindowHeight, 3000);

    public double ResolvedSideColumnWidth() =>
        Clamp(SideColumnWidth ?? DefaultSideColumnWidth, MinSideColumnWidth, 900);

    public double ResolvedSightRowHeight() =>
        Clamp(SightRowHeight ?? DefaultSightRowHeight, MinSightRowHeight, 800);

    private static double Clamp(double value, double min, double max) =>
        Math.Min(max, Math.Max(min, value));
}
