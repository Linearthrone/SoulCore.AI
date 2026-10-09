namespace SoulCore.Inference.Clients;

/// <summary>
/// Optional OpenAI-compat vision sidecar (e.g. Synthetic <c>syn:large:vision</c>).
/// Describes screenshot bytes as text for the local tool-loop model.
/// </summary>
public interface IVisionDescribeClient
{
    bool IsConfigured { get; }

    /// <summary>
    /// Describe screenshot image(s) (raw base64, no data-URI prefix).
    /// Returns null when disabled or the call fails (caller keeps local vision path).
    /// </summary>
    Task<string?> DescribeScreenshotAsync(
        string toolName,
        IReadOnlyList<string> base64Images,
        CancellationToken cancellationToken = default);
}
