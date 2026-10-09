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

    [Theory]
    [InlineData("3\t1\n", 3, 1)]
    [InlineData("0 0", 0, 0)]
    [InlineData("12 4", 12, 4)]
    public void TryParseRevListLeftRight_ParsesBehindAhead(string stdout, int behind, int ahead)
    {
        Assert.True(LocalStackControl.TryParseRevListLeftRight(stdout, out var left, out var right));
        Assert.Equal(behind, left);
        Assert.Equal(ahead, right);
    }

    [Fact]
    public void TryParseRevListLeftRight_RejectsGarbage()
    {
        Assert.False(LocalStackControl.TryParseRevListLeftRight("not-a-count", out _, out _));
        Assert.False(LocalStackControl.TryParseRevListLeftRight("", out _, out _));
    }

    [Fact]
    public void IsPorcelainDirty_WhitespaceOnlyIsClean()
    {
        Assert.False(LocalStackControl.IsPorcelainDirty(""));
        Assert.False(LocalStackControl.IsPorcelainDirty("   \n"));
        Assert.True(LocalStackControl.IsPorcelainDirty(" M House/foo.cs\n"));
    }

    [Fact]
    public void BuildGitStatusDetail_SummarizesState()
    {
        var detail = LocalStackControl.BuildGitStatusDetail(2, 0, true, "origin/main");
        Assert.Contains("behind 2", detail);
        Assert.Contains("dirty", detail);
        Assert.Contains("origin/main", detail);
    }

    [Fact]
    public async Task RestartStackAsync_FailsWhenScriptMissing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hv-repo-rst-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "ALLSTART.ps1"), "# test");
        try
        {
            using var stack = new LocalStackControl(dir);
            var result = await stack.RestartStackAsync();
            Assert.False(result.Ok);
            Assert.Contains("missing script", result.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("restart-stack", result.Detail, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void RestartStackRelativeScript_IsUnderHouseScripts()
    {
        Assert.Equal("House\\scripts\\restart-stack.ps1", LocalStackControl.RestartStackRelativeScript);
    }

    [Fact]
    public void BumpVersionsRelativeScript_IsUnderHouseScripts()
    {
        Assert.Equal("House\\scripts\\bump-versions.ps1", LocalStackControl.BumpVersionsRelativeScript);
    }

    [Theory]
    [InlineData("0.1.6", "0.1.7", true)]
    [InlineData("0.1.6", "0.1.6", false)]
    [InlineData(null, "0.1.7", true)]
    [InlineData("0.1.6", null, false)]
    [InlineData("", "", false)]
    [InlineData("0.1.9", "0.1.10", true)]
    public void HostVersionMoved_DetectsStaleHealth(string? before, string? after, bool expected)
    {
        Assert.Equal(expected, LocalStackControl.HostVersionMoved(before, after));
    }
}
