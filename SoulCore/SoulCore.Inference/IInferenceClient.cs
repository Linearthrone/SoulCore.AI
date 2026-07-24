namespace SoulCore.Inference;

/// <summary>
/// Local LLM client (Ollama). Real calls via <c>OllamaInferenceClient</c>; null stub when disabled.
/// </summary>
public interface IInferenceClient
{
    /// <param name="systemPreamble">Optional Ollama <c>system</c> field (e.g. emotion influence). No secrets.</param>
    Task<string> CompleteAsync(
        string prompt,
        string? systemPreamble = null,
        CancellationToken cancellationToken = default);
}
