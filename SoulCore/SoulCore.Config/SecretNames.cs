namespace SoulCore.Config;

/// <summary>
/// Env / user-secret key names only — never store values in committed files.
/// </summary>
public static class SecretNames
{
    public const string A2eApiToken = "SOULCORE_A2E_TOKEN";
    public const string HermesApiKey = "SOULCORE_HERMES_API_KEY";
    public const string HuggingFaceToken = "SOULCORE_HF_TOKEN";

    /// <summary>
    /// Companion phone / remote WS upgrade token (BED-155). When set, Host
    /// fail-closes <c>/ws</c> unless <c>Authorization: Bearer</c> or <c>X-Api-Key</c> matches.
    /// Prefer ≥ 32 random chars. Never commit values.
    /// </summary>
    public const string CompanionApiToken = "SOULCORE_COMPANION_API_TOKEN";
}
