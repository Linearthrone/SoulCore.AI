namespace SoulCore.Host.Ws;

/// <summary>
/// Single owner of prompt section order for chat turns.
/// </summary>
public interface IChatContextBuilder
{
    /// <summary>
    /// Loads independent context pieces (parallel when safe) and composes the
    /// system preamble for the model.
    /// </summary>
    Task<ChatContext> BuildAsync(
        string userText,
        bool useToolLoop,
        string? desktopTargetWindowTitle,
        CancellationToken cancellationToken);
}
