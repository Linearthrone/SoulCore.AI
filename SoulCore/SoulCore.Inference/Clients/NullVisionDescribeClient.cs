namespace SoulCore.Inference.Clients;

/// <summary>No-op when <c>Inference:VisionBaseUrl</c> / model / key are unset.</summary>
public sealed class NullVisionDescribeClient : IVisionDescribeClient
{
    public bool IsConfigured => false;

    public Task<string?> DescribeScreenshotAsync(
        string toolName,
        IReadOnlyList<string> base64Images,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
