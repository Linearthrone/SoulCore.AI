using System.Net.WebSockets;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

/// <summary>
/// Lightweight coverage for WS auth header choice + error classification (no Host required).
/// Live Host probe: SoulCore/scripts/ws-companion-auth-probe.ps1
/// </summary>
public class SoulCoreWsClientAuthTests
{
    [Fact]
    public void AuthHeaderName_IsXApiKey()
    {
        Assert.Equal("X-Api-Key", SoulCoreWsClient.AuthHeaderName);
    }

    [Fact]
    public void GuessAuthFailure_Detects401()
    {
        var ex = new WebSocketException("The server returned status code '401' when status code '101' was expected.");
        Assert.True(SoulCoreWsClient.GuessAuthFailure(ex));
    }

    [Fact]
    public void GuessAuthFailure_DetectsUnauthorized()
    {
        var ex = new InvalidOperationException("Unauthorized");
        Assert.True(SoulCoreWsClient.GuessAuthFailure(ex));
    }

    [Fact]
    public void GuessAuthFailure_IgnoresGenericDown()
    {
        var ex = new WebSocketException("Unable to connect to the remote server");
        Assert.False(SoulCoreWsClient.GuessAuthFailure(ex));
    }

    [Fact]
    public void CompanionToken_DescribePresence_NeverIncludesSecret()
    {
        var previous = Environment.GetEnvironmentVariable(CompanionToken.EnvName);
        try
        {
            Environment.SetEnvironmentVariable(CompanionToken.EnvName, "super-secret-token-value-do-not-leak!!");
            var desc = CompanionToken.DescribePresence();
            Assert.Contains("tokenPresent=true", desc, StringComparison.Ordinal);
            Assert.Contains("tokenLen=", desc, StringComparison.Ordinal);
            Assert.DoesNotContain("super-secret", desc, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CompanionToken.EnvName, previous);
        }
    }

    [Fact]
    public void SetRequestHeader_XApiKey_DoesNotThrow()
    {
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader(SoulCoreWsClient.AuthHeaderName, "probe-token-not-a-secret");
    }
}
