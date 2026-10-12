namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-17.3: self-started research thread (no task, no release).
/// Living interest is not timer-killed — only human/SMS abort or quiet model.
/// </summary>
public interface ILeisureSession
{
    string? CurrentInterest { get; }
    string? LastUntrustedSummary { get; }
    bool IsActive { get; }
    void BeginOrContinue(string interest);
    void NoteUntrustedSummary(string summary);
    void Clear();
    string ActivityLine();
}

public sealed class LeisureSession : ILeisureSession
{
    private readonly object _gate = new();
    private string? _interest;
    private string? _summary;
    private bool _active;

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

    public void Clear()
    {
        lock (_gate)
        {
            _interest = null;
            _summary = null;
            _active = false;
        }
    }

    public string ActivityLine()
    {
        lock (_gate)
        {
            if (!_active || string.IsNullOrWhiteSpace(_interest))
                return "Between talks · quiet";
            return $"Reading about {_interest} · local";
        }
    }
}
