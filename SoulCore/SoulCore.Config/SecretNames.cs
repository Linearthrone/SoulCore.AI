namespace SoulCore.Config;

/// <summary>
/// Env / user-secret key names only — never store values in committed files.
/// </summary>
public static class SecretNames
{
    public const string A2eApiToken = "SOULCORE_A2E_TOKEN";
    public const string HermesApiKey = "SOULCORE_HERMES_API_KEY";
    public const string HuggingFaceToken = "SOULCORE_HF_TOKEN";
}
