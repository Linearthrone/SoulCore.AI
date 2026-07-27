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

    /// <summary>
    /// OpenAI <c>tool_choice</c> field sent with every tool-loop request.
    /// <para>
    /// Values: <c>"auto"</c> (model decides, default), <c>"none"</c> (model
    /// must not call tools — equivalent to a text-only turn), or
    /// <c>{ "type":"function", "function":{ "name":"..." } }</c> to force a
    /// specific tool (BED-127 documents this; the host may also override per
    /// call by passing a different value — not exposed in the method signature
    /// to keep it byte-compatible with <c>IInferenceClient.CompleteWithToolsAsync</c>).
    /// </para>
    /// <para>
    /// When the tool-loop is invoked with an empty <c>tools[]</c> array, the
    /// client omits <c>tool_choice</c> entirely (OpenAI rejects
    /// <c>tool_choice</c> without <c>tools</c>).
    /// </para>
    /// </summary>
    public string ToolChoice { get; set; } = "auto";
}
