namespace SoulCore.Core.Abstractions;

/// <summary>
/// Emotion vector persistence surface. Implemented by <c>SqliteMemoryStore</c> (Host DI).
/// Schema owned by DBD-01 (<c>emotion_state</c> singleton).
/// </summary>
public interface IEmotionState
{
    Task<IReadOnlyDictionary<string, double>> GetAsync(CancellationToken cancellationToken = default);

    Task SetAsync(IReadOnlyDictionary<string, double> components, CancellationToken cancellationToken = default);

    /// <summary>Reads <c>emotion_state.revision</c> for the singleton row (id=1).</summary>
    Task<long> GetRevisionAsync(CancellationToken cancellationToken = default);
}
