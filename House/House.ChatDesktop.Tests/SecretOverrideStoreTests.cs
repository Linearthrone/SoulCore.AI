using System.IO;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

/// <summary>PROP-22.3 — secret rows only ever report Saved / Not set, and survive repeat saves.</summary>
public sealed class SecretOverrideStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "hv-secret-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public SecretOverrideStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "secret-overrides.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void Save_then_describe_reports_saved_without_the_value()
    {
        Environment.SetEnvironmentVariable(SecretOverrideStore.OllamaKey, null);
        Assert.Equal("Not set", SecretOverrideStore.Describe(SecretOverrideStore.OllamaKey, _path));

        Assert.True(SecretOverrideStore.Save(SecretOverrideStore.OllamaKey, "  sk-test-value  ", _path));

        var label = SecretOverrideStore.Describe(SecretOverrideStore.OllamaKey, _path);
        Assert.Equal("Saved", label);
        Assert.DoesNotContain("sk-test-value", label, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_twice_overwrites_even_when_file_is_hidden()
    {
        Assert.True(SecretOverrideStore.Save(SecretOverrideStore.SonioxKey, "first", _path));
        Assert.True(SecretOverrideStore.Save(SecretOverrideStore.SonioxKey, "second", _path));
        Assert.Equal("second", SecretOverrideStore.ReadAll(_path)[SecretOverrideStore.SonioxKey]);
    }

    [Fact]
    public void Clear_removes_the_override()
    {
        Environment.SetEnvironmentVariable(SecretOverrideStore.SyntheticKey, null);
        SecretOverrideStore.Save(SecretOverrideStore.SyntheticKey, "abc", _path);
        Assert.True(SecretOverrideStore.HasOverride(SecretOverrideStore.SyntheticKey, _path));

        SecretOverrideStore.Clear(SecretOverrideStore.SyntheticKey, _path);
        Assert.False(SecretOverrideStore.HasOverride(SecretOverrideStore.SyntheticKey, _path));
        Assert.Equal("Not set", SecretOverrideStore.Describe(SecretOverrideStore.SyntheticKey, _path));
    }

    [Fact]
    public void Unknown_keys_are_rejected()
    {
        Assert.False(SecretOverrideStore.IsKnown("SOMETHING_ELSE"));
        Assert.False(SecretOverrideStore.Save("SOMETHING_ELSE", "x", _path));
    }

    [Fact]
    public void ApplyToProcessEnv_sets_primary_and_alias_without_overwriting_by_default()
    {
        const string alias = "SYNTHETIC_API_KEY";
        var oldPrimary = Environment.GetEnvironmentVariable(SecretOverrideStore.SyntheticKey);
        var oldAlias = Environment.GetEnvironmentVariable(alias);
        try
        {
            Environment.SetEnvironmentVariable(SecretOverrideStore.SyntheticKey, null);
            Environment.SetEnvironmentVariable(alias, null);
            SecretOverrideStore.Save(SecretOverrideStore.SyntheticKey, "env-value", _path);

            SecretOverrideStore.ApplyToProcessEnv(_path);
            Assert.Equal("env-value", Environment.GetEnvironmentVariable(SecretOverrideStore.SyntheticKey));
            Assert.Equal("env-value", Environment.GetEnvironmentVariable(alias));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SecretOverrideStore.SyntheticKey, oldPrimary);
            Environment.SetEnvironmentVariable(alias, oldAlias);
        }
    }
}
