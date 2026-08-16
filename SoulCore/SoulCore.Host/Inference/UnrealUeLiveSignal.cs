using SoulCore.Adapters.Ws;
using SoulCore.Inference;

namespace SoulCore.Host.Inference;

/// <summary>
/// UE-live = Unreal body WebSocket connected (PIE / bridge up on shadow).
/// Gates small tool + embed fallback while VRAM is contended.
/// </summary>
public sealed class UnrealUeLiveSignal : IUeLiveSignal
{
    private readonly IUnrealVerbClient _unreal;

    public UnrealUeLiveSignal(IUnrealVerbClient unreal)
    {
        _unreal = unreal ?? throw new ArgumentNullException(nameof(unreal));
    }

    public bool IsUeLive => _unreal.IsConnected;
}
