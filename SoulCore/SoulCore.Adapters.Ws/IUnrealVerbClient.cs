namespace SoulCore.Adapters.Ws;

/// <summary>
/// Outbound Unreal body verbs. Connect is optional — never throw into Host startup if UE is down.
/// </summary>
public interface IUnrealVerbClient
{
    bool IsConnected { get; }

    string TargetUrl { get; }

    Task EnsureConnectedAsync(CancellationToken cancellationToken = default);

    Task<bool> SetEmotionAsync(object emotionPayload, CancellationToken cancellationToken = default);

    Task<bool> SpeakAsync(string text, CancellationToken cancellationToken = default);

    Task<bool> PlayAnimationAsync(string animationName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Relative locomotion — maps to UE plain <c>move_avatar_relative &lt;forward&gt; &lt;right&gt; &lt;up&gt;</c>
    /// (local +X/+Y/+Z cm; empty payload defaults forward=50). UE path-follows to the relative goal (BED-117).
    /// </summary>
    Task<bool> LocoAsync(object locoPayload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Absolute world path-follow — maps to UE plain <c>move_to &lt;x&gt; &lt;y&gt; &lt;z&gt;</c> (BED-117).
    /// Payload: <c>x</c>/<c>y</c>/<c>z</c> world centimeters.
    /// </summary>
    Task<bool> MoveToAsync(object moveToPayload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel in-progress path-follow — maps to UE plain <c>stop</c> (BED-117).
    /// </summary>
    Task<bool> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Look-at verb — maps to UE <c>autonomy</c> / <c>look_at_player</c>.
    /// <paramref name="lookPayload"/> is accepted for API stability but ignored by the mapper.
    /// </summary>
    Task<bool> LookAsync(object lookPayload, CancellationToken cancellationToken = default);
}
