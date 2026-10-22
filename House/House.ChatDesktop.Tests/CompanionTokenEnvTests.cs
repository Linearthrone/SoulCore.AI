using House.ChatDesktop.Services;
using Xunit;

namespace House.ChatDesktop.Tests;

public class CompanionTokenEnvTests
{
    [Fact]
    public void TryLoadFromEnvFile_UsesConfiguredRepoRootOutsideBaseDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "hv-env-" + Guid.NewGuid().ToString("N"));
        var soul = Path.Combine(root, "SoulCore");
        Directory.CreateDirectory(soul);
        File.WriteAllText(Path.Combine(root, "ALLSTART.ps1"), "# test");
        var token = "unit-test-companion-token-32chars!!";
        File.WriteAllText(
            Path.Combine(soul, ".env"),
            $"SOULCORE_COMPANION_API_TOKEN={token}\n");

        var previous = Environment.GetEnvironmentVariable(CompanionToken.EnvName);
        try
        {
            Environment.SetEnvironmentVariable(CompanionToken.EnvName, null);
            var applied = CompanionToken.TryLoadFromEnvFile(root);
            Assert.True(applied >= 1);
            Assert.Equal(token, CompanionToken.Resolve());
            Assert.NotNull(CompanionToken.ResolvedEnvFilePath(root));
            Assert.Contains("SoulCore", CompanionToken.ResolvedEnvFilePath(root)!, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CompanionToken.EnvName, previous);
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void EnumerateEnvFileCandidates_PrefersConfiguredRepo()
    {
        var configured = @"C:\Users\LinearThrone\Soul_Core";
        var first = CompanionToken.EnumerateEnvFileCandidates(configured).First();
        Assert.Equal(Path.Combine(configured, "SoulCore", ".env"), first);
    }
}
