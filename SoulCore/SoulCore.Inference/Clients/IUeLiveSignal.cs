namespace SoulCore.Inference.Clients;

/// <summary>
/// True when Unreal body / PIE is live (shadow VRAM contended).
/// Host wires this to <c>IUnrealVerbClient.IsConnected</c>.
/// </summary>
public interface IUeLiveSignal
{
    bool IsUeLive { get; }
}
