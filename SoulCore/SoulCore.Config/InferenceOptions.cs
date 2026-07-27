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
    /// This is reply length — not the model context window.
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Ollama context window (<c>num_ctx</c>). Caps how much prompt+history the
    /// model can attend to. <c>gemma4:latest</c> reports ~131072 max; values above
    /// the model limit are clamped by Ollama. Default 32768 balances persona +
    /// memory against VRAM. Set 0 to omit (Ollama model default).
    /// </summary>
    public int NumCtx { get; set; } = 32768;

    /// <summary>
    /// When false, Ollama generate sends <c>think: false</c> so thinking models
    /// (e.g. gemma4) do not burn <see cref="MaxTokens"/> on hidden chain-of-thought
    /// and return an empty <c>response</c>. Default false for chat reliability.
    /// </summary>
    public bool ThinkEnabled { get; set; } = false;

    /// <summary>
    /// When true (and <see cref="Enabled"/>), Host registers <c>OllamaEmbeddingClient</c>
    /// for semantic episodic recall. Default true; set false to force recency-only recall.
    /// </summary>
    public bool EmbeddingsEnabled { get; set; } = true;

    /// <summary>Ollama embedding model (e.g. <c>nomic-embed-text</c>).</summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Maximum number of <c>/api/chat</c> round-trips the agent loop may make
    /// per turn (each round-trip = one model call + zero-or-more tool
    /// dispatches + re-prompt). Guards against a model that keeps emitting
    /// <c>tool_calls</c> indefinitely. The loop returns the last assistant
    /// text (or a capped marker when the model emitted only tool calls) once
    /// the cap is hit. Default 8 — enough for a memory + body turn, bounded
    /// to keep latency predictable. Must be ≥ 1.
    /// </summary>
    public int MaxToolIterations { get; set; } = 8;
}
