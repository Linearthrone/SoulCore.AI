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
    public void MinWindowWidthForSide_FitsChatSplitterSideAndEastGrip()
    {
        var min = PresencePaneLayout.MinWindowWidthForSide(520);
        Assert.True(min >= LocalUiSettings.MinWindowWidth);
        Assert.True(min >= LocalUiSettings.MinChatWidth + 6 + 520 + PresencePaneLayout.EastGripWidth);
    }

    [Fact]
    public void Defaults_are_portrait_glass_slab_with_right_drawer()
    {
        var s = new LocalUiSettings();
        s.Normalize();
        Assert.Equal(460, s.ResolvedWindowWidth());
        Assert.Equal(996, s.ResolvedWindowHeight());
        Assert.Equal(LocalUiSettings.VmDrawerRight, s.VmDrawerSide);
        Assert.False(s.VmDrawerOnLeft());
    }

    [Fact]
    public void AspectRatio_is_19_5_by_9_and_height_follows_width()
    {
        Assert.Equal(19.5 / 9.0, LocalUiSettings.AspectRatio, 6);
        Assert.Equal(997.0, LocalUiSettings.HeightForWidth(460), 0);
        Assert.Equal(460.0, LocalUiSettings.WidthForHeight(996), 0);
    }

    [Fact]
    public void VmDrawerSide_normalizes_unknown_values_to_right()
    {
        var s = new LocalUiSettings { VmDrawerSide = "sideways" };
        s.Normalize();
        Assert.Equal(LocalUiSettings.VmDrawerRight, s.VmDrawerSide);

        s.VmDrawerSide = "LEFT";
        s.Normalize();
        Assert.True(s.VmDrawerOnLeft());
    }

    [Fact]
    public void MainWindow_axaml_is_glass_shell_without_old_side_column()
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
        Assert.Contains("x:Name=\"StatusOrbButton\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"VmDrawerTab\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TranscriptList\"", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("HerScreenEastResizeGrip", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"*,6,520,10\"", axaml, StringComparison.Ordinal);
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
