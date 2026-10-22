using House.ChatDesktop.Services;
using Xunit;

namespace House.ChatDesktop.Tests;

public class LocalStackControlTests
{
    [Fact]
    public void LooksLikeRepoRoot_RequiresAllstartOrSoulCore()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hv-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.False(LocalStackControl.LooksLikeRepoRoot(dir));
            File.WriteAllText(Path.Combine(dir, "ALLSTART.ps1"), "# test");
            Assert.True(LocalStackControl.LooksLikeRepoRoot(dir));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void ResolveRepoRoot_PrefersConfiguredPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hv-repo-cfg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "ALLSTART.ps1"), "# test");
        try
        {
            var resolved = LocalStackControl.ResolveRepoRoot(dir);
            Assert.Equal(Path.GetFullPath(dir), resolved);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void ResolveRepoRoot_IgnoresMissingConfiguredPath()
    {
        var missing = Path.Combine(Path.GetTempPath(), "hv-missing-" + Guid.NewGuid().ToString("N"));
        // May still find a real checkout via walk/env — just assert configured missing does not throw
        // and returned path (if any) looks like a repo.
        var resolved = LocalStackControl.ResolveRepoRoot(missing);
        if (resolved is not null)
            Assert.True(LocalStackControl.LooksLikeRepoRoot(resolved));
    }

    [Fact]
    public void EnumerateRepoRootCandidates_IncludesConfiguredFirst()
    {
        var configured = @"C:\Users\test\Soul_Core";
        var first = LocalStackControl.EnumerateRepoRootCandidates(configured).First();
        Assert.Equal(configured, first);
    }

    [Fact]
    public void LocalUiSettings_PersistsRepoRootAndAutoStart()
    {
        var s = new LocalUiSettings
        {
            SoulCoreRepoRoot = @"C:\Users\test\Soul_Core",
            AutoStartStack = false
        };
        s.Normalize();
        Assert.Equal(@"C:\Users\test\Soul_Core", s.SoulCoreRepoRoot);
        Assert.False(s.AutoStartStack);
    }

    [Fact]
    public void FormatPathForDisplay_RewritesUserProfilePrefix()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.False(string.IsNullOrWhiteSpace(profile));

        var under = Path.Combine(profile, "Soul_Core", "nested");
        var display = LocalStackControl.FormatPathForDisplay(under);

        Assert.Equal(
            "%USERPROFILE%" + Path.DirectorySeparatorChar + "Soul_Core" + Path.DirectorySeparatorChar + "nested",
            display);
        Assert.DoesNotContain(profile, display, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatPathForDisplay_ExactUserProfile()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.Equal("%USERPROFILE%", LocalStackControl.FormatPathForDisplay(profile));
    }

    [Fact]
    public void FormatPathForDisplay_LeavesNonProfilePathsUnchanged()
    {
        var outside = OperatingSystem.IsWindows()
            ? @"D:\Repos\Soul_Core"
            : "/opt/Soul_Core";
        Assert.Equal(outside, LocalStackControl.FormatPathForDisplay(outside));
    }

    [Fact]
    public void FormatPathForDisplay_DoesNotRewritePartialProfilePrefix()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var siblingish = profile + "Extra" + Path.DirectorySeparatorChar + "Soul_Core";
        Assert.Equal(siblingish, LocalStackControl.FormatPathForDisplay(siblingish));
    }

    [Fact]
    public void FormatPathForDisplay_NullOrEmpty()
    {
        Assert.Equal("", LocalStackControl.FormatPathForDisplay(null));
        Assert.Equal("", LocalStackControl.FormatPathForDisplay(""));
    }
}
