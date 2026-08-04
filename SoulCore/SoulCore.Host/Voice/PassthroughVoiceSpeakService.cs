using SoulCore.Adapters.Ws;

namespace SoulCore.Host.Voice;

/// <summary>No-op voice when Voice:Enabled=false — forwards text speak to Unreal only.</summary>
public sealed class PassthroughVoiceSpeakService : IVoiceSpeakService
{
    private readonly IUnrealVerbClient _unreal;

    public PassthroughVoiceSpeakService(IUnrealVerbClient unreal)
    {
        _unreal = unreal ?? throw new ArgumentNullException(nameof(unreal));
    }

    public byte[]? LastWav => null;

    public Task SpeakAloudAsync(string text, CancellationToken cancellationToken = default) =>
        _unreal.SpeakAsync(text, cancellationToken);
}
