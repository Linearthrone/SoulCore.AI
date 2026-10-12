namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-17.3: exclusive inference lease. Work runner does not tick while held.
/// Short keep-alive; human/SMS abort cancels the in-flight R&amp;R pass.
/// </summary>
public interface IInferenceLease
{
    bool IsHeld { get; }
    string? Holder { get; }
    bool TryAcquire(string holder, TimeSpan keepAlive, out CancellationToken abortToken);
    void Touch(string holder);
    void Release(string holder);
    /// <summary>Abort in-flight holder (human chat / SMS). Idempotent.</summary>
    void Abort(string reason = "human");
    bool IsExpired { get; }
}

public sealed class InferenceLease : IInferenceLease
{
    public const string LeisureHolder = "leisure";
    public const string WorkHolder = "work";

    private readonly object _gate = new();
    private string? _holder;
    private DateTimeOffset _expiresAt;
    private CancellationTokenSource? _abort;

    public bool IsHeld
    {
        get
        {
            lock (_gate)
            {
                ExpireIfNeeded_NoLock();
                return _holder is not null;
            }
        }
    }

    public string? Holder
    {
        get
        {
            lock (_gate)
            {
                ExpireIfNeeded_NoLock();
                return _holder;
            }
        }
    }

    public bool IsExpired
    {
        get
        {
            lock (_gate)
                return _holder is not null && DateTimeOffset.UtcNow >= _expiresAt;
        }
    }

    public bool TryAcquire(string holder, TimeSpan keepAlive, out CancellationToken abortToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(holder);
        if (keepAlive <= TimeSpan.Zero)
            keepAlive = TimeSpan.FromSeconds(90);

        lock (_gate)
        {
            ExpireIfNeeded_NoLock();
            if (_holder is not null)
            {
                abortToken = CancellationToken.None;
                return false;
            }

            _holder = holder.Trim();
            _expiresAt = DateTimeOffset.UtcNow.Add(keepAlive);
            _abort?.Dispose();
            _abort = new CancellationTokenSource();
            abortToken = _abort.Token;
            return true;
        }
    }

    public void Touch(string holder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(holder);
        lock (_gate)
        {
            if (!string.Equals(_holder, holder.Trim(), StringComparison.Ordinal))
                return;
            _expiresAt = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(90));
        }
    }

    public void Release(string holder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(holder);
        lock (_gate)
        {
            if (!string.Equals(_holder, holder.Trim(), StringComparison.Ordinal))
                return;
            Clear_NoLock();
        }
    }

    public void Abort(string reason = "human")
    {
        lock (_gate)
        {
            if (_holder is null)
                return;
            try
            {
                _abort?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // already released
            }

            Clear_NoLock();
        }
    }

    private void ExpireIfNeeded_NoLock()
    {
        if (_holder is null)
            return;
        if (DateTimeOffset.UtcNow < _expiresAt)
            return;
        Clear_NoLock();
    }

    private void Clear_NoLock()
    {
        _holder = null;
        _expiresAt = default;
        var cts = _abort;
        _abort = null;
        cts?.Dispose();
    }
}
