using SoulCore.Core.Persona;
using SoulCore.Memory;

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

    /// <summary>
    /// Compose context for a persona the caller already pinned.
    /// Does not read <see cref="SoulCore.Core.Persona.IPersonaSession.GetActive"/>.
    /// </summary>
    Task<ChatContext> BuildPinnedAsync(
        string userText,
        bool useToolLoop,
        string? desktopTargetWindowTitle,
        PersonaPack pinnedPack,
        IMemoryStore pinnedMemory,
        bool foregroundTurn,
        CancellationToken cancellationToken);
}
