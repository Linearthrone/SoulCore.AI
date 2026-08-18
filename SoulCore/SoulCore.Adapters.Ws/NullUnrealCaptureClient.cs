namespace SoulCore.Adapters.Ws;

/// <summary>No-op capture client when Unreal bridge is disabled.</summary>
public sealed class NullUnrealCaptureClient : IUnrealEyeCaptureClient, IUnrealCallCameraClient
{
    public Task<EyeFrame?> CaptureEyeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<EyeFrame?>(null);

    public Task<EyeFrame?> CaptureCallFrameAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<EyeFrame?>(null);
}
