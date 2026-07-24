namespace SoulCore.Config;

/// <summary>
/// Ollama / local LLM client knobs (non-secret). Base URL defaults to quarry loopback :11434.
/// </summary>
public sealed class InferenceOptions
{
    public const string SectionName = "Inference";

    /// <summary>When false, Host registers <c>NullInferenceClient</c>.</summary>
    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";

    public string Model { get; set; } = "gemma4:latest";

    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Maximum number of tokens Ollama may generate (<c>num_predict</c>).
    /// Prevents unbounded generation that causes HttpClient timeouts.
    /// </summary>
    public int MaxTokens { get; set; } = 256;
}
