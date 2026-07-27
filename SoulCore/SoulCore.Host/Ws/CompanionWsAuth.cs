using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SoulCore.Config;

namespace SoulCore.Host.Ws;

/// <summary>
/// Fail-closed companion token gate for <c>/ws</c> upgrades (BED-155 / SEC-152).
/// When <c>SOULCORE_COMPANION_API_TOKEN</c> is set, require
/// <c>Authorization: Bearer</c> or <c>X-Api-Key</c>. When unset, loopback desktop
/// keeps the historical no-header trust model.
/// </summary>
public static class CompanionWsAuth
{
    public const string AuthorizationHeader = "Authorization";
    public const string ApiKeyHeader = "X-Api-Key";
    public const string BearerPrefix = "Bearer ";

    /// <summary>Recommended minimum length (≥ 32 random chars). Guidance only — not enforced at runtime.</summary>
    public const int RecommendedMinLength = 32;

    public enum AuthOutcome
    {
        /// <summary>No companion token configured — gate is open.</summary>
        NotRequired,

        /// <summary>Token configured and header matched.</summary>
        Authorized,

        /// <summary>Token configured but neither header present / empty.</summary>
        Missing,

        /// <summary>Token configured but presented value did not match.</summary>
        Invalid
    }

    /// <summary>
    /// Resolve configured token from process env (DotEnv / shell) then configuration
    /// (user-secrets / env after <c>SOULCORE_</c> strip). Whitespace-only → unset.
    /// </summary>
    public static string? ResolveConfiguredToken(IConfiguration? configuration = null)
    {
        var fromEnv = Environment.GetEnvironmentVariable(SecretNames.CompanionApiToken);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        if (configuration is not null)
        {
            // user-secrets may store the full SOULCORE_* name or the stripped key.
            var fromConfig = configuration[SecretNames.CompanionApiToken]
                ?? configuration["COMPANION_API_TOKEN"];
            if (!string.IsNullOrWhiteSpace(fromConfig))
                return fromConfig.Trim();
        }

        return null;
    }

    public static bool IsTokenConfigured(string? configuredToken) =>
        !string.IsNullOrWhiteSpace(configuredToken);

    /// <summary>
    /// Evaluate upgrade headers against <paramref name="configuredToken"/>.
    /// Never throws; never returns the raw presented token.
    /// </summary>
    public static AuthOutcome Evaluate(HttpRequest request, string? configuredToken)
    {
        if (!IsTokenConfigured(configuredToken))
            return AuthOutcome.NotRequired;

        var presented = ExtractPresentedToken(request, out var sourceHeader);
        if (string.IsNullOrEmpty(presented))
            return AuthOutcome.Missing;

        return TokensEqual(presented, configuredToken!)
            ? AuthOutcome.Authorized
            : AuthOutcome.Invalid;
    }

    /// <summary>
    /// Which header supplied a candidate (for safe logs only — never the value).
    /// Empty when neither header had a usable token.
    /// </summary>
    public static string DescribeHeaderSource(HttpRequest request)
    {
        ExtractPresentedToken(request, out var source);
        return string.IsNullOrEmpty(source) ? "none" : source;
    }

    /// <summary>
    /// Safe log fragment: never includes secret values.
    /// Example: <c>outcome=Missing header=none</c>.
    /// </summary>
    public static string FormatLogSafe(AuthOutcome outcome, string headerSource) =>
        $"outcome={outcome} header={headerSource}";

    /// <summary>
    /// Redact Authorization / X-Api-Key values for any diagnostic dump.
    /// </summary>
    public static string RedactHeaderValue(string headerName, string? rawValue)
    {
        if (string.IsNullOrEmpty(rawValue))
            return string.Empty;

        if (headerName.Equals(AuthorizationHeader, StringComparison.OrdinalIgnoreCase)
            || headerName.Equals(ApiKeyHeader, StringComparison.OrdinalIgnoreCase)
            || headerName.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase))
        {
            return "[REDACTED]";
        }

        return rawValue;
    }

    internal static string? ExtractPresentedToken(HttpRequest request, out string sourceHeader)
    {
        sourceHeader = string.Empty;

        // Preferred: Authorization: Bearer <token>
        if (request.Headers.TryGetValue(AuthorizationHeader, out var authValues))
        {
            var auth = authValues.ToString();
            if (auth.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = auth[BearerPrefix.Length..].Trim();
                if (token.Length > 0)
                {
                    sourceHeader = AuthorizationHeader;
                    return token;
                }
            }
        }

        // Alias: X-Api-Key: <token> (FED-149 / LLMOD parity)
        if (request.Headers.TryGetValue(ApiKeyHeader, out var apiKeyValues))
        {
            var apiKey = apiKeyValues.ToString().Trim();
            if (apiKey.Length > 0)
            {
                sourceHeader = ApiKeyHeader;
                return apiKey;
            }
        }

        return null;
    }

    private static bool TokensEqual(string presented, string expected)
    {
        var a = Encoding.UTF8.GetBytes(presented);
        var b = Encoding.UTF8.GetBytes(expected);
        if (a.Length != b.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
