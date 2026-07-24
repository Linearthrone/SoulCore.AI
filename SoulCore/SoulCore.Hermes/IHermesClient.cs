namespace SoulCore.Hermes;

/// <summary>
/// Hermes OpenAI-compatible tool-loop client.
/// Secrets (if any) via env / user-secrets only — never committed config.
/// </summary>
public interface IHermesClient
{
    /// <param name="systemPreamble">Optional system message (e.g. emotion influence). No secrets.</param>
    Task<string> ChatAsync(
        string message,
        string? systemPreamble = null,
        CancellationToken cancellationToken = default);

    /// <summary>Optional health probe (HermesHttpClient). Null stub returns empty.</summary>
    Task<string> GetHealthAsync(CancellationToken cancellationToken = default);
}
