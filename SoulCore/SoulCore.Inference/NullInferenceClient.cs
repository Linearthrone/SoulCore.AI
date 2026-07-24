namespace SoulCore.Inference;

/// <summary>
/// Stub — returns empty; no network / no LLM.
/// </summary>
public sealed class NullInferenceClient : IInferenceClient
{
    public Task<string> CompleteAsync(
        string prompt,
        string? systemPreamble = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);
}
