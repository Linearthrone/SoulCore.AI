namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-17.3: self-started research thread (no task, no release).
/// Living interest is not timer-killed — only human/SMS abort or quiet model.
/// Presence band only claims "Reading about…" while a recent leisure pass is fresh.
/// </summary>
public interface ILeisureSession
{
    string? CurrentInterest { get; }
    string? LastUntrustedSummary { get; }
    bool IsActive { get; }
    DateTimeOffset? LastPassUtc { get; }
    void BeginOrContinue(string interest);
    void NoteUntrustedSummary(string summary);
    void NotePassCompleted();
    void Clear();
    string ActivityLine(DateTimeOffset? nowUtc = null);
}

public sealed class LeisureSession : ILeisureSession
{
    /// <summary>After this, Presence shows quiet even if an interest is still held for the next pass.</summary>
    public static readonly TimeSpan FreshPassWindow = TimeSpan.FromMinutes(2);

    private readonly object _gate = new();
    private string? _interest;
    private string? _summary;
    private bool _active;
    private DateTimeOffset? _lastPassUtc;

    public string? CurrentInterest
    {
        get { lock (_gate) return _interest; }
    }

    public string? LastUntrustedSummary
    {
        get { lock (_gate) return _summary; }
    }

    public bool IsActive
    {
        get { lock (_gate) return _active; }
    }

    public DateTimeOffset? LastPassUtc
    {
        get { lock (_gate) return _lastPassUtc; }
    }

    public void BeginOrContinue(string interest)
    {
        var topic = (interest ?? "").Trim();
        if (topic.Length == 0)
            topic = LeisurePrompt.DefaultInterest;
        if (topic.Length > 80)
            topic = topic[..77] + "…";
        lock (_gate)
        {
            _interest = topic;
            _active = true;
        }
    }

    public void NoteUntrustedSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return;
        lock (_gate)
            _summary = summary.Trim();
    }

    public void NotePassCompleted()
    {
        lock (_gate)
            _lastPassUtc = DateTimeOffset.UtcNow;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _interest = null;
            _summary = null;
            _active = false;
            _lastPassUtc = null;
        }
    }

    public string ActivityLine(DateTimeOffset? nowUtc = null)
    {
        lock (_gate)
        {
            if (!_active || string.IsNullOrWhiteSpace(_interest))
                return "Between talks · quiet";

            var now = nowUtc ?? DateTimeOffset.UtcNow;
            if (_lastPassUtc is null || now - _lastPassUtc.Value > FreshPassWindow)
                return "Between talks · quiet";

            return $"Reading about {_interest} · local";
        }
    }
}
