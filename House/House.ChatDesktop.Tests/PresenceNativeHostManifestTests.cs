using System.Xml.Linq;
using House.ChatDesktop.Controls;

namespace House.ChatDesktop.Tests;

/// <summary>
/// Presence 0.1.5 cold-started with NativeControlHost in the visual tree (even IsVisible=False).
/// Avalonia Win32 CreateWindowEx then fatal'd without app.manifest supportedOS.
/// </summary>
public sealed class PresenceNativeHostManifestTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "House", "House.ChatDesktop", "House.ChatDesktop.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
        }
    }

    private static string ChatDesktopDir =>
        Path.Combine(RepoRoot, "House", "House.ChatDesktop");

    [Fact]
    public void AppManifest_lists_supported_OS_and_dpi_awareness()
    {
        var path = Path.Combine(ChatDesktopDir, "app.manifest");
        Assert.True(File.Exists(path), "app.manifest missing — NativeControlHost will crash on Windows");

        var xml = File.ReadAllText(path);
        Assert.Contains("{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}", xml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("supportedOS", xml, StringComparison.Ordinal);
        Assert.Contains("dpiAware", xml, StringComparison.Ordinal);
        Assert.Contains("dpiAwareness", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Csproj_wires_ApplicationManifest_and_semver()
    {
        var path = Path.Combine(ChatDesktopDir, "House.ChatDesktop.csproj");
        var doc = XDocument.Load(path);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var manifest = doc.Descendants(ns + "ApplicationManifest").Select(e => e.Value).FirstOrDefault();
        Assert.Equal("app.manifest", manifest);

        var version = doc.Descendants(ns + "Version").Select(e => e.Value).FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.Matches(@"^\d+\.\d+\.\d+$", version!);

        var info = doc.Descendants(ns + "InformationalVersion").Select(e => e.Value).FirstOrDefault();
        Assert.Equal(version, info);
    }

    [Fact]
    public void MainWindow_axaml_defers_NativeControlHost_out_of_cold_start_tree()
    {
        var path = Path.Combine(ChatDesktopDir, "MainWindow.axaml");
        var axaml = File.ReadAllText(path);
        Assert.Contains("VictoriaBrowserEmbedSlot", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<controls:VictoriaBrowserEmbedHost",
            axaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EmbedHost_exposes_NativeHostUnavailable_for_soft_fail()
    {
        var host = new VictoriaBrowserEmbedHost();
        Assert.False(host.NativeHostUnavailable);
        // Bind(0) must stay quiet — no throw, stays invisible path for JPEG fallback.
        host.Bind(0);
        Assert.False(host.IsVisible);
    }
}
