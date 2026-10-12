using House.ChatDesktop.Services;
using Xunit;

namespace House.ChatDesktop.Tests;

public class HostStackBusyPolicyTests
{
    [Theory]
    [InlineData(false, null, true)]
    [InlineData(false, "host-start", true)]
    [InlineData(true, "host-start", true)]
    [InlineData(true, "host-restart", true)]
    [InlineData(true, "host-stop", false)]
    [InlineData(true, "ollama-start", false)]
    [InlineData(true, null, false)]
    public void IsHostStopEnabled_KeepsStopDuringStartOrRestart(bool busy, string? tag, bool expected)
    {
        Assert.Equal(expected, HostStackBusyPolicy.IsHostStopEnabled(busy, tag));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void IsHostStartEnabled_DisabledOnlyWhenBusy(bool busy, bool expected)
    {
        Assert.Equal(expected, HostStackBusyPolicy.IsHostStartEnabled(busy));
        Assert.Equal(expected, HostStackBusyPolicy.IsHostRestartEnabled(busy));
    }
}
