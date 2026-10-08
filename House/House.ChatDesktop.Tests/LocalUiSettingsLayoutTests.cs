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
    public void Normalize_ClampsSideColumnToMax()
    {
        var s = new LocalUiSettings { SideColumnWidth = 5000 };
        s.Normalize();
        Assert.Equal(LocalUiSettings.MaxSideColumnWidth, s.SideColumnWidth);
    }

    [Fact]
    public void ResolvedSideColumnWidth_DefaultsWideEnoughForVmEmbed()
    {
        Assert.True(LocalUiSettings.DefaultSideColumnWidth >= 480);
        Assert.Equal(
            LocalUiSettings.DefaultSideColumnWidth,
            new LocalUiSettings().ResolvedSideColumnWidth());
    }

    [Fact]
    public void StarWeightsForSideWidth_GivesSideRoomToGrow()
    {
        var (chat, side) = PresencePaneLayout.StarWeightsForSideWidth(
            sideWidth: 520,
            windowWidth: 1180);
        Assert.True(side > 0);
        Assert.True(chat > 0);
        // ~520 of ~1174 usable → side share roughly 40%+
        Assert.InRange(side / (chat + side), 0.35, 0.55);
    }

    [Fact]
    public void MainWindow_axaml_uses_star_side_column_for_vm_resize()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? path = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "House", "House.ChatDesktop", "MainWindow.axaml");
            if (File.Exists(candidate))
            {
                path = candidate;
                break;
            }

            dir = dir.Parent;
        }

        Assert.True(path is not null, "MainWindow.axaml not found for layout assert");
        var axaml = File.ReadAllText(path!);
        Assert.Contains("ColumnDefinitions=\"*,6,*\"", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"*,4,520\"", axaml, StringComparison.Ordinal);
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
