using System.Globalization;
using System.Text;

namespace House.ChatDesktop.Services;

/// <summary>
/// PROP-22.4 — in-memory Presence performance log. Stack / system notices land here
/// (Settings → Performance log) instead of the chat transcript. Not persisted.
/// </summary>
public sealed class PerformanceLog
{
    public const int MaxEntries = 1000;

    private readonly object _gate = new();
    private readonly List<PerformanceLogEntry> _entries = new();

    public event EventHandler? Changed;

    public int Count
    {
        get
        {
            lock (_gate)
                return _entries.Count;
        }
    }

    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        lock (_gate)
        {
            _entries.Add(new PerformanceLogEntry(DateTimeOffset.Now, text.Trim()));
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<PerformanceLogEntry> Snapshot()
    {
        lock (_gate)
            return _entries.ToArray();
    }

    public void Clear()
    {
        lock (_gate)
            _entries.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Text shown in the Performance log screen; empty log says so plainly.</summary>
    public string Render()
    {
        var rows = Snapshot();
        if (rows.Count == 0)
            return "Nothing logged yet this session.";

        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            sb.Append(row.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            sb.Append("  ");
            sb.AppendLine(row.Text);
        }

        return sb.ToString().TrimEnd();
    }
}

public sealed record PerformanceLogEntry(DateTimeOffset At, string Text);
