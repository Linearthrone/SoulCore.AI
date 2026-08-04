namespace SoulCore.Adapters.Ws;

/// <summary>Optional eye-camera capture on the Unreal bridge client.</summary>
public interface IUnrealEyeCaptureClient
{
    Task<EyeFrame?> CaptureEyeAsync(CancellationToken cancellationToken = default);
}

/// <summary>One captured eye-camera frame.</summary>
public sealed record EyeFrame(byte[] Bytes, string Format, int Width, int Height);
