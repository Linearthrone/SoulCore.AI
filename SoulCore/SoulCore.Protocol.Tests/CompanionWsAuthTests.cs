using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SoulCore.Config;
using SoulCore.Host.Ws;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// BED-155: fail-closed companion token gate for /ws upgrades.
/// </summary>
public class CompanionWsAuthTests
{
    private const string TestToken = "companion-test-token-32chars-min!!";

    [Fact]
    public void ResolveConfiguredToken_Unset_ReturnsNull()
    {
        var previous = Environment.GetEnvironmentVariable(SecretNames.CompanionApiToken);
        try
        {
            Environment.SetEnvironmentVariable(SecretNames.CompanionApiToken, null);
            var emptyConfig = new ConfigurationBuilder().Build();
            Assert.Null(CompanionWsAuth.ResolveConfiguredToken(emptyConfig));
            Assert.False(CompanionWsAuth.IsTokenConfigured(null));
            Assert.False(CompanionWsAuth.IsTokenConfigured("   "));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SecretNames.CompanionApiToken, previous);
        }
    }

    [Fact]
    public void ResolveConfiguredToken_FromEnv_Wins()
    {
        var previous = Environment.GetEnvironmentVariable(SecretNames.CompanionApiToken);
        try
        {
            Environment.SetEnvironmentVariable(SecretNames.CompanionApiToken, "  env-token-value-at-least-32chars  ");
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [SecretNames.CompanionApiToken] = "config-should-not-win"
                })
                .Build();

            Assert.Equal("env-token-value-at-least-32chars", CompanionWsAuth.ResolveConfiguredToken(config));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SecretNames.CompanionApiToken, previous);
        }
    }

    [Fact]
    public void ResolveConfiguredToken_FromConfig_WhenEnvUnset()
    {
        var previous = Environment.GetEnvironmentVariable(SecretNames.CompanionApiToken);
        try
        {
            Environment.SetEnvironmentVariable(SecretNames.CompanionApiToken, null);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["COMPANION_API_TOKEN"] = "from-config-stripped-key-32chars!!"
                })
                .Build();

            Assert.Equal("from-config-stripped-key-32chars!!", CompanionWsAuth.ResolveConfiguredToken(config));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SecretNames.CompanionApiToken, previous);
        }
    }

    [Fact]
    public void Evaluate_TokenUnset_NotRequired_EvenWithoutHeaders()
    {
        var request = MakeRequest();
        Assert.Equal(CompanionWsAuth.AuthOutcome.NotRequired, CompanionWsAuth.Evaluate(request, null));
        Assert.Equal(CompanionWsAuth.AuthOutcome.NotRequired, CompanionWsAuth.Evaluate(request, "  "));
    }

    [Fact]
    public void Evaluate_TokenSet_MissingHeader_IsMissing()
    {
        var request = MakeRequest();
        Assert.Equal(CompanionWsAuth.AuthOutcome.Missing, CompanionWsAuth.Evaluate(request, TestToken));
        Assert.Equal("none", CompanionWsAuth.DescribeHeaderSource(request));
    }

    [Fact]
    public void Evaluate_TokenSet_WrongBearer_IsInvalid()
    {
        var request = MakeRequest();
        request.Headers.Authorization = "Bearer wrong-token-value-not-matching!!!!";
        Assert.Equal(CompanionWsAuth.AuthOutcome.Invalid, CompanionWsAuth.Evaluate(request, TestToken));
        Assert.Equal(CompanionWsAuth.AuthorizationHeader, CompanionWsAuth.DescribeHeaderSource(request));
    }

    [Fact]
    public void Evaluate_TokenSet_CorrectBearer_IsAuthorized()
    {
        var request = MakeRequest();
        request.Headers.Authorization = $"Bearer {TestToken}";
        Assert.Equal(CompanionWsAuth.AuthOutcome.Authorized, CompanionWsAuth.Evaluate(request, TestToken));
    }

    [Fact]
    public void Evaluate_TokenSet_BearerCaseInsensitivePrefix()
    {
        var request = MakeRequest();
        request.Headers.Authorization = $"bearer {TestToken}";
        Assert.Equal(CompanionWsAuth.AuthOutcome.Authorized, CompanionWsAuth.Evaluate(request, TestToken));
    }

    [Fact]
    public void Evaluate_TokenSet_CorrectXApiKey_IsAuthorized()
    {
        var request = MakeRequest();
        request.Headers[CompanionWsAuth.ApiKeyHeader] = TestToken;
        Assert.Equal(CompanionWsAuth.AuthOutcome.Authorized, CompanionWsAuth.Evaluate(request, TestToken));
        Assert.Equal(CompanionWsAuth.ApiKeyHeader, CompanionWsAuth.DescribeHeaderSource(request));
    }

    [Fact]
    public void Evaluate_TokenSet_WrongXApiKey_IsInvalid()
    {
        var request = MakeRequest();
        request.Headers[CompanionWsAuth.ApiKeyHeader] = "definitely-not-the-companion-token!!";
        Assert.Equal(CompanionWsAuth.AuthOutcome.Invalid, CompanionWsAuth.Evaluate(request, TestToken));
    }

    [Fact]
    public void Evaluate_EmptyBearer_FallsThroughToMissingWhenNoApiKey()
    {
        var request = MakeRequest();
        request.Headers.Authorization = "Bearer ";
        Assert.Equal(CompanionWsAuth.AuthOutcome.Missing, CompanionWsAuth.Evaluate(request, TestToken));
    }

    [Fact]
    public void Evaluate_EmptyBearer_UsesXApiKeyAlias()
    {
        var request = MakeRequest();
        request.Headers.Authorization = "Bearer ";
        request.Headers[CompanionWsAuth.ApiKeyHeader] = TestToken;
        Assert.Equal(CompanionWsAuth.AuthOutcome.Authorized, CompanionWsAuth.Evaluate(request, TestToken));
    }

    [Fact]
    public void FormatLogSafe_NeverContainsToken()
    {
        var safe = CompanionWsAuth.FormatLogSafe(CompanionWsAuth.AuthOutcome.Invalid, CompanionWsAuth.AuthorizationHeader);
        Assert.Contains("outcome=Invalid", safe);
        Assert.Contains("header=Authorization", safe);
        Assert.DoesNotContain(TestToken, safe);
        Assert.DoesNotContain("Bearer", safe);
    }

    [Fact]
    public void RedactHeaderValue_AuthAndApiKey_AreRedacted()
    {
        Assert.Equal("[REDACTED]", CompanionWsAuth.RedactHeaderValue("Authorization", $"Bearer {TestToken}"));
        Assert.Equal("[REDACTED]", CompanionWsAuth.RedactHeaderValue("X-Api-Key", TestToken));
        Assert.Equal("[REDACTED]", CompanionWsAuth.RedactHeaderValue("authorization", "secret"));
        Assert.Equal("text/plain", CompanionWsAuth.RedactHeaderValue("Content-Type", "text/plain"));
    }

    [Fact]
    public void RecommendedMinLength_IsAtLeast32()
    {
        Assert.True(CompanionWsAuth.RecommendedMinLength >= 32);
        Assert.True(TestToken.Length >= CompanionWsAuth.RecommendedMinLength);
    }

    [Fact]
    public void SecretNames_CompanionApiToken_IsDocumentedName()
    {
        Assert.Equal("SOULCORE_COMPANION_API_TOKEN", SecretNames.CompanionApiToken);
    }

    private static HttpRequest MakeRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }
}
