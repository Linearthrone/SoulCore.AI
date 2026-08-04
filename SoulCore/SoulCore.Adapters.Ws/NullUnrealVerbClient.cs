namespace SoulCore.Adapters.Ws;

/// <summary>No-op Unreal client when <c>UnrealBridge:Enabled=false</c>.</summary>
public sealed class NullUnrealVerbClient : IUnrealVerbClient
{
    public bool IsConnected => false;

    public string TargetUrl => "disabled";

    public Task EnsureConnectedAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> SetEmotionAsync(object emotionPayload, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> SpeakAsync(string text, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> SpeakAsync(object speakPayload, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> PlayAnimationAsync(string animationName, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> LocoAsync(object locoPayload, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> MoveToAsync(object moveToPayload, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> StopAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> LookAsync(object lookPayload, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
