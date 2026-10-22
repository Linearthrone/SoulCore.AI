using House.ChatDesktop.Services;
using Xunit;

namespace House.ChatDesktop.Tests;

public class BuildCurrencyStatusTests
{
    [Fact]
    public void PresenceCurrencyLabel_Unpackaged_WhenNotInstalled()
    {
        var label = BuildCurrencyStatus.PresenceCurrencyLabel(
            PresenceUpdateCheckResult.Kind.UpToDate,
            availableVersion: null,
            isInstalled: false);
        Assert.Equal("Unpackaged", label);
    }

    [Fact]
    public void PresenceCurrencyLabel_UpdateAvailable_IncludesVersion()
    {
        var label = BuildCurrencyStatus.PresenceCurrencyLabel(
            PresenceUpdateCheckResult.Kind.Available,
            availableVersion: "0.3.1",
            isInstalled: true);
        Assert.Equal("Update available: v0.3.1", label);
    }

    [Fact]
    public void PresenceCurrencyLabel_UpToDate()
    {
        var label = BuildCurrencyStatus.PresenceCurrencyLabel(
            PresenceUpdateCheckResult.Kind.UpToDate,
            availableVersion: null,
            isInstalled: true);
        Assert.Equal("Up to date", label);
    }

    [Theory]
    [InlineData(false, null, null, true)]
    [InlineData(null, false, null, true)]
    [InlineData(null, null, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, null, false)]
    [InlineData(true, null, true, false)]
    [InlineData(null, true, true, false)]
    [InlineData(null, null, null, false)]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, true, true)]
    public void IsHostOutdatedForPersonas_AnyFailMeansOutdated(
        bool? apiOk,
        bool? healthHasPersona,
        bool? healthCapabilitiesPersonas,
        bool expected)
    {
        Assert.Equal(
            expected,
            BuildCurrencyStatus.IsHostOutdatedForPersonas(apiOk, healthHasPersona, healthCapabilitiesPersonas));
    }

    [Fact]
    public void HostCurrencyLabel_Outdated_MentionsCreatorEditionRestart()
    {
        var label = BuildCurrencyStatus.HostCurrencyLabel(
            personaApiOk: false,
            healthHasPersona: null,
            hostBehindPresence: false);
        Assert.Contains("Host outdated", label, StringComparison.Ordinal);
        Assert.Contains("CreatorEdition", label, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatTitleChrome_IncludesPresenceHostAndFlags()
    {
        var text = BuildCurrencyStatus.FormatTitleChrome(
            "0.2.1",
            "0.1.5",
            "Up to date",
            "Host outdated");
        Assert.Equal("Presence 0.2.1 · Up to date · Host 0.1.5 · Host outdated", text);
    }

    [Theory]
    [InlineData("0.1.5", "0.2.0", true)]
    [InlineData("0.2.0", "0.2.0", false)]
    [InlineData("0.3.0", "0.2.0", false)]
    [InlineData("0.1.5+git", "0.2.0", true)]
    [InlineData(null, "0.2.0", false)]
    public void IsHostBehindPresence_ComparesSemVer(string? host, string? presence, bool expected)
    {
        Assert.Equal(expected, BuildCurrencyStatus.IsHostBehindPresence(host, presence));
    }

    [Fact]
    public void DefaultUpdateFeed_PointsAtCreatorEdition()
    {
        Assert.Contains(
            "House-VictoriAI/SoulCore.AI_CreatorEdition",
            PresenceUpdateService.DefaultGithubRepoUrl,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Linearthrone/SoulCore.AI",
            PresenceUpdateService.DefaultGithubRepoUrl,
            StringComparison.OrdinalIgnoreCase);
    }
}
