namespace SoulCore.Inference;

/// <summary>
/// Stub — embeddings disabled; no network.
/// </summary>
public sealed class NullEmbeddingClient : IEmbeddingClient
{
    public bool IsEnabled => false;

    public string Model => string.Empty;

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<float>());
}
