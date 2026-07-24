namespace SoulCore.Config;

/// <summary>
/// Hermes OpenAI-compatible client knobs (non-secret). API key via env / user-secrets only.
/// </summary>
public sealed class HermesOptions
{
    public const string SectionName = "Hermes";

    /// <summary>When false, Host registers <c>NullHermesClient</c>.</summary>
    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "http://127.0.0.1:8642";

    public string Model { get; set; } = "local";

    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Maximum number of tokens Hermes may generate (<c>max_tokens</c>).
    /// Prevents unbounded generation that causes HttpClient timeouts.
    /// </summary>
    public int MaxTokens { get; set; } = 256;

    /// <summary>
    /// Optional config key placeholder — real value must come from
    /// <c>SOULCORE_HERMES_API_KEY</c> / user-secrets, never committed files.
    /// </summary>
    public string? ApiKey { get; set; }
}
