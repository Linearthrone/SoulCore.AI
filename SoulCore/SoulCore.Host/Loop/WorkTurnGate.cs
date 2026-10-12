namespace SoulCore.Host.Loop;

/// <summary>
/// One piece of model work at a time. Chat and the assignment runner share it
/// so they never drive the desktop together.
/// </summary>
public sealed class WorkTurnGate
{
    private readonly SemaphoreSlim _sem = new(1, 1);

    public Task WaitAsync(CancellationToken cancellationToken) => _sem.WaitAsync(cancellationToken);

    public void Release() => _sem.Release();
}
