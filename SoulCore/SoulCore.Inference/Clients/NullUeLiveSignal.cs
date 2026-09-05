namespace SoulCore.Inference.Clients;

/// <summary>Default: UE not live (use full tool / embed models).</summary>
public sealed class NullUeLiveSignal : IUeLiveSignal
{
    public bool IsUeLive => false;
}
