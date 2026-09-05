namespace SoulCore.Memory;

/// <summary>Runs short SQLite command sections under a shared path gate.</summary>
internal static class SqliteDbGate
{
    public static async Task RunAsync(
        SemaphoreSlim gate,
        bool disposed,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, typeof(SqliteMemorySession));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, typeof(SqliteMemorySession));
            await work(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public static async Task<T> RunAsync<T>(
        SemaphoreSlim gate,
        bool disposed,
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, typeof(SqliteMemorySession));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, typeof(SqliteMemorySession));
            return await work(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
