using SoulCore.Config;

namespace SoulCore.Protocol.Tests;

public class DotEnvLoaderTests
{
    [Fact]
    public void TryLoad_OverwritesStaleProcessEnv()
    {
        var key = SecretNames.CompanionApiToken;
        var previous = Environment.GetEnvironmentVariable(key);
        var dir = Path.Combine(Path.GetTempPath(), "soulcore-dotenv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var envFile = Path.Combine(dir, ".env");
        try
        {
            Environment.SetEnvironmentVariable(key, "stale-token-from-windows-user-env!!!!");
            File.WriteAllText(envFile, $"{key}=fresh-token-from-dotenv-file-60chars-abcdefghijklmnop\n");

            var applied = DotEnvLoader.TryLoad(envFile);
            Assert.True(applied >= 1);
            Assert.Equal(
                "fresh-token-from-dotenv-file-60chars-abcdefghijklmnop",
                Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void TryLoad_StripsBomAndQuotesOnCompanionToken()
    {
        var key = SecretNames.CompanionApiToken;
        var previous = Environment.GetEnvironmentVariable(key);
        var dir = Path.Combine(Path.GetTempPath(), "soulcore-dotenv-bom-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var envFile = Path.Combine(dir, ".env");
        try
        {
            Environment.SetEnvironmentVariable(key, "stale");
            // UTF-8 BOM + quoted value — common Notepad / VS save footgun
            File.WriteAllText(envFile, "\uFEFF" + $"{key}='token-with-bom-and-quotes'\n");

            Assert.True(DotEnvLoader.TryLoad(envFile) >= 1);
            Assert.Equal("token-with-bom-and-quotes", Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void TryLoad_EmptyValueClearsProcessEnv()
    {
        var key = SecretNames.CompanionApiToken;
        var previous = Environment.GetEnvironmentVariable(key);
        var dir = Path.Combine(Path.GetTempPath(), "soulcore-dotenv-clear-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var envFile = Path.Combine(dir, ".env");
        try
        {
            Environment.SetEnvironmentVariable(key, "should-be-cleared");
            File.WriteAllText(envFile, $"{key}=\n");

            Assert.True(DotEnvLoader.TryLoad(envFile) >= 1);
            Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }
}
