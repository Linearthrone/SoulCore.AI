namespace SoulCore.Adapters.Ws;

/// <summary>
/// Waist-up / phone-call camera on Victoria's avatar (UE <c>call_capture</c>).
/// Distinct from <see cref="IUnrealEyeCaptureClient"/> (outward eye view).
/// </summary>
public interface IUnrealCallCameraClient
{
    /// <summary>
    /// Capture one PNG/JPEG from Victoria's front-facing call SceneCapture (waist-up).
    /// </summary>
    Task<EyeFrame?> CaptureCallFrameAsync(CancellationToken cancellationToken = default);
}
