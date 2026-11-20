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

    /// <summary>
    /// PROP-22: bump when the saved shell geometry means something different.
    /// Files written by the wide (pre-glass) layout are reset on load.
    /// </summary>
    public const int CurrentLayoutVersion = 2;

    /// <summary>PROP-22.4: Galaxy S26 Ultra slab, 19.5:9 portrait.</summary>
    public const double DefaultWindowWidth = 460;
    public const double DefaultWindowHeight = 996;
    public const double MinWindowWidth = 360;
    public const double MinWindowHeight = 640;

    /// <summary>Height / width. The shell locks to this ratio when the operator resizes.</summary>
    public const double AspectRatio = 19.5 / 9.0;

    public const string VmDrawerLeft = "left";
    public const string VmDrawerRight = "right";

    /// <summary>Legacy wide-layout pane sizes — kept so old files still deserialize.</summary>
    public const double DefaultSideColumnWidth = 520;
    public const double DefaultSightRowHeight = 200;
    public const double MinChatWidth = 320;
    public const double MinSideColumnWidth = 200;
    /// <summary>Soft cap when persisting; star layout can grow larger while the window is wide.</summary>
    public const double MaxSideColumnWidth = 2400;
    public const double MinSightRowHeight = 120;
    public const double MinBrowserRowHeight = 120;

    public string DisplayName { get; set; } = "Victoria";

    public NotificationSettings Notifications { get; set; } = new();

    /// <summary>Layout generation this file was written for (see <see cref="CurrentLayoutVersion"/>).</summary>
    public int LayoutVersion { get; set; } = CurrentLayoutVersion;

    /// <summary>PROP-12.1: restored Presence shell geometry.</summary>
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
    public bool WindowMaximized { get; set; }

    /// <summary>PROP-22.6: which edge the VM drawer popup docks to — <c>left</c> | <c>right</c>.</summary>
    public string VmDrawerSide { get; set; } = VmDrawerRight;

    /// <summary>
    /// PROP-22.9: optional client-side Host port override (this PC only; the Host bind stays
    /// 127.0.0.1). Env <c>HOUSE_SOULCORE_PORT</c> still wins. Applies on next Presence start.
    /// </summary>
    public int? ClientPort { get; set; }

    /// <summary>Legacy right column width (Her browser + What she saw).</summary>
    public double? SideColumnWidth { get; set; }

    /// <summary>Legacy sight panel row height inside the right column.</summary>
    public double? SightRowHeight { get; set; }

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

    /// <summary>
    /// PROP-18.3: UTC ISO-8601 when Presence last closed (while-you-were-gone <c>since</c>).
    /// Empty → Host default window (12h).
    /// </summary>
    public string? LastPresenceSeenAtUtc { get; set; }

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

            loaded.MigrateLegacyLayout();
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

    /// <summary>
    /// Wide-layout geometry (e.g. 1180×780 + side column) would stretch the 19.5:9 slab to
    /// nonsense. Old files carry no <c>layoutVersion</c> (deserializes as the current default),
    /// so detect them by shape: a landscape window or any side-column value.
    /// </summary>
    internal void MigrateLegacyLayout()
    {
        var landscape = WindowWidth is double w && WindowHeight is double h && w > h;
        var legacyPane = SideColumnWidth is not null || SightRowHeight is not null;
        if (LayoutVersion >= CurrentLayoutVersion && !landscape && !legacyPane)
            return;

        WindowWidth = null;
        WindowHeight = null;
        WindowX = null;
        WindowY = null;
        WindowMaximized = false;
        SideColumnWidth = null;
        SightRowHeight = null;
        LayoutVersion = CurrentLayoutVersion;
    }

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
            DisplayName = "Victoria";

        VmDrawerSide = NormalizeVmDrawerSide(VmDrawerSide);

        if (ClientPort is int port && port is < 1024 or > 65535)
            ClientPort = null;

        if (WindowWidth is double w)
            WindowWidth = Clamp(w, MinWindowWidth, 4000);
        if (WindowHeight is double h)
            WindowHeight = Clamp(h, MinWindowHeight, 3000);
        if (SideColumnWidth is double side)
            SideColumnWidth = Clamp(side, MinSideColumnWidth, MaxSideColumnWidth);
        if (SightRowHeight is double sight)
            SightRowHeight = Clamp(sight, MinSightRowHeight, 800);
    }

    /// <summary>Any value other than <c>left</c> resolves to <c>right</c> (the default).</summary>
    public static string NormalizeVmDrawerSide(string? side) =>
        string.Equals(side?.Trim(), VmDrawerLeft, StringComparison.OrdinalIgnoreCase)
            ? VmDrawerLeft
            : VmDrawerRight;

    public bool VmDrawerOnLeft() =>
        NormalizeVmDrawerSide(VmDrawerSide) == VmDrawerLeft;

    public double ResolvedWindowWidth() =>
        Clamp(WindowWidth ?? DefaultWindowWidth, MinWindowWidth, 4000);

    public double ResolvedWindowHeight() =>
        Clamp(WindowHeight ?? DefaultWindowHeight, MinWindowHeight, 3000);

    public double ResolvedSideColumnWidth() =>
        Clamp(SideColumnWidth ?? DefaultSideColumnWidth, MinSideColumnWidth, MaxSideColumnWidth);

    public double ResolvedSightRowHeight() =>
        Clamp(SightRowHeight ?? DefaultSightRowHeight, MinSightRowHeight, 800);

    /// <summary>Height that matches <paramref name="width"/> on the locked 19.5:9 ratio.</summary>
    public static double HeightForWidth(double width) =>
        Math.Round(width * AspectRatio);

    /// <summary>Width that matches <paramref name="height"/> on the locked 19.5:9 ratio.</summary>
    public static double WidthForHeight(double height) =>
        Math.Round(height / AspectRatio);

    private static double Clamp(double value, double min, double max) =>
        Math.Min(max, Math.Max(min, value));
}
