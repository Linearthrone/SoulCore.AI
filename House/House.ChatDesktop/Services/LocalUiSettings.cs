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

    public string DisplayName { get; set; } = "Victoria";

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

            if (string.IsNullOrWhiteSpace(loaded.DisplayName))
            {
                loaded.DisplayName = "Victoria";
            }

            return loaded;
        }
        catch
        {
            return new LocalUiSettings();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(StorePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            DisplayName = "Victoria";
        }

        File.WriteAllText(StorePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
