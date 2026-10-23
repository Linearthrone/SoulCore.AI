namespace SoulCore.Host.Persona;

/// <summary>
/// Cancels in-flight foreground tool loops when the foreground persona changes.
/// Background chats do not register, so a foreground change does not cancel them.
/// </summary>
public interface IForegroundToolLoopGate
{
    CancellationTokenSource Register(string personaId, CancellationToken callerToken);

    void Release(string personaId, CancellationTokenSource loop);

    void Cancel(string personaId);
}

/// <summary>Process-wide gate. One Host, one foreground tool owner.</summary>
public sealed class ForegroundToolLoopGate : IForegroundToolLoopGate
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<CancellationTokenSource>> _loops =
        new(StringComparer.OrdinalIgnoreCase);

    public CancellationTokenSource Register(string personaId, CancellationToken callerToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        var loop = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        lock (_gate)
        {
            if (!_loops.TryGetValue(personaId, out var list))
            {
                list = new List<CancellationTokenSource>();
                _loops[personaId] = list;
            }

            list.Add(loop);
        }

        return loop;
    }

    public void Release(string personaId, CancellationTokenSource loop)
    {
        ArgumentNullException.ThrowIfNull(loop);
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(personaId)
                && _loops.TryGetValue(personaId, out var list))
            {
                list.Remove(loop);
                if (list.Count == 0)
                    _loops.Remove(personaId);
            }
        }

        loop.Dispose();
    }

    public void Cancel(string personaId)
    {
        if (string.IsNullOrWhiteSpace(personaId))
            return;

        List<CancellationTokenSource> copy;
        lock (_gate)
        {
            if (!_loops.TryGetValue(personaId, out var list) || list.Count == 0)
                return;
            copy = list.ToList();
        }

        foreach (var loop in copy)
        {
            try
            {
                loop.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The turn finished and released the source between the copy and Cancel.
            }
        }
    }
}
