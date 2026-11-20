namespace SoulCore.Host.Companion;

/// <summary>
/// Soft-depend PROP-20.4 / 20.27 / 20.30: shared mind-block builder (floors + resting +
/// earned Compass + Host-selected context traits). When unregistered, SMS channel
/// text stands alone beside memory — private journal books are never copied into
/// the SMS preamble.
/// </summary>
public interface IVictoriaMindBlockBuilder
{
    /// <summary>
    /// Returns mind-block text, or null/empty when unavailable.
    /// <paramref name="matchText"/> (user SMS / leisure interest) drives Compass
    /// and context-trait retrieve; omit or leave blank for floors + resting only.
    /// Context traits also require a &lt;24h newest chat/SMS contact (Host gate).
    /// </summary>
    Task<string?> TryBuildAsync(
        string? matchText = null,
        CancellationToken cancellationToken = default);
}
