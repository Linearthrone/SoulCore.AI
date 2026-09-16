using System.IO;
using System.Text.Json;
using House.ChatDesktop.Services;
using Xunit;

namespace House.ChatDesktop.Tests;

public class LocalUiSettingsLayoutTests
{
    [Fact]
    public void Normalize_ClampsPaneAndWindowSizes()
    {
        var s = new LocalUiSettings
        {
            WindowWidth = 100,
            WindowHeight = 50,
            SideColumnWidth = 10,
            SightRowHeight = 5,
            DisplayName = "  "
        };

        s.Normalize();

        Assert.Equal("Victoria", s.DisplayName);
        Assert.Equal(LocalUiSettings.MinWindowWidth, s.WindowWidth);
        Assert.Equal(LocalUiSettings.MinWindowHeight, s.WindowHeight);
        Assert.Equal(LocalUiSettings.MinSideColumnWidth, s.SideColumnWidth);
        Assert.Equal(LocalUiSettings.MinSightRowHeight, s.SightRowHeight);
    }

    [Fact]
    public void RoundTrip_PersistsLayoutFields()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hv-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ui-settings.json");
        try
        {
            var original = new LocalUiSettings
            {
                DisplayName = "Victoria",
                WindowWidth = 1400,
                WindowHeight = 900,
                WindowX = 40,
                WindowY = 60,
                WindowMaximized = true,
                SideColumnWidth = 320,
                SightRowHeight = 240
            };
            original.Normalize();
            File.WriteAllText(path, JsonSerializer.Serialize(original, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));

            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<LocalUiSettings>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            Assert.NotNull(loaded);
            loaded!.Normalize();
            Assert.Equal(1400, loaded.WindowWidth);
            Assert.Equal(900, loaded.WindowHeight);
            Assert.Equal(40, loaded.WindowX);
            Assert.Equal(60, loaded.WindowY);
            Assert.True(loaded.WindowMaximized);
            Assert.Equal(320, loaded.SideColumnWidth);
            Assert.Equal(240, loaded.SightRowHeight);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }
}
