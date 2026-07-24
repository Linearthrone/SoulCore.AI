namespace SoulCore.Hermes;

public sealed class NullHermesClient : IHermesClient
{
    public Task<string> ChatAsync(
        string message,
        string? systemPreamble = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);

    public Task<string> GetHealthAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);
}
