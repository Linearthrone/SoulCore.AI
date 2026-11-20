using House.ChatDesktop.Services;
using Xunit;

namespace House.ChatDesktop.Tests;

[Collection("CompanionTokenState")]
public class CompanionTokenStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    private readonly string? _previousToken;

    public CompanionTokenStoreTests()
    {
        _previousToken = Environment.GetEnvironmentVariable(CompanionToken.EnvName);
        Environment.SetEnvironmentVariable(CompanionToken.EnvName, null);
        _dir = Path.Combine(Path.GetTempPath(), "hv-tok-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "companion-api-token.txt");
        // Redirect store by writing via Save after swapping — we test through public API on real path is risky.
        // Instead test Read/Save against a temp file by exercising CompanionToken.Resolve priority
        // with Save to the real store path under a unique LocalAppData override is hard.
        // Unit-test Save/Clear/Read on the real StorePath with cleanup.
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(CompanionToken.EnvName, _previousToken);
        try
        {
            if (File.Exists(CompanionTokenStore.StorePath))
            {
                // Only delete if we created a test token in this fixture — Clear() in tests.
            }
        }
        catch { /* ignore */ }
    }

    [Fact]
    public void Save_Read_Clear_RoundTrip()
    {
        var previousSaved = CompanionTokenStore.Read();
        try
        {
            CompanionTokenStore.Save("settings-token-value-for-unit-test-xx");
            Assert.True(CompanionTokenStore.HasSavedToken());
            Assert.Equal("settings-token-value-for-unit-test-xx", CompanionTokenStore.Read());
            Assert.Equal("settings-token-value-for-unit-test-xx", CompanionToken.Resolve());
            Assert.Contains("source=settings", CompanionToken.DescribePresence(), StringComparison.Ordinal);

            CompanionTokenStore.Clear();
            Assert.False(CompanionTokenStore.HasSavedToken());
            Environment.SetEnvironmentVariable(CompanionToken.EnvName, "env-only-token-value-for-unit-test");
            Assert.Equal("env-only-token-value-for-unit-test", CompanionToken.Resolve());
            Assert.Contains("source=env", CompanionToken.DescribePresence(), StringComparison.Ordinal);
        }
        finally
        {
            if (previousSaved is null)
                CompanionTokenStore.Clear();
            else
                CompanionTokenStore.Save(previousSaved);
            Environment.SetEnvironmentVariable(CompanionToken.EnvName, _previousToken);
        }
    }

    [Fact]
    public void SettingsToken_WinsOverEnv()
    {
        var previousSaved = CompanionTokenStore.Read();
        try
        {
            Environment.SetEnvironmentVariable(CompanionToken.EnvName, "from-env-should-lose");
            CompanionTokenStore.Save("from-settings-should-win");
            Assert.Equal("from-settings-should-win", CompanionToken.Resolve());
        }
        finally
        {
            if (previousSaved is null)
                CompanionTokenStore.Clear();
            else
                CompanionTokenStore.Save(previousSaved);
            Environment.SetEnvironmentVariable(CompanionToken.EnvName, _previousToken);
        }
    }
}
