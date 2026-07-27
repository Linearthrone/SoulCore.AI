namespace SoulCore.Inference;

/// <summary>
/// Text → float embedding vector (Ollama). Null stub when embeddings disabled.
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>True when this client can produce real vectors (not a null stub).</summary>
    bool IsEnabled { get; }

    /// <summary>Configured model name (may be empty for null stub).</summary>
    string Model { get; }

    /// <summary>
    /// Embed <paramref name="text"/> into a float vector. Throws on network/API failure;
    /// callers should fall back to recency recall.
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
