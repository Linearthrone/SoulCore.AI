namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-17.3 / 17.1: whether a task surface can run released work.
/// Default treats <c>local</c> as always up; other surfaces default up unless
/// a probe marks them down (injected).
/// </summary>
public interface ISurfaceAvailability
{
    bool IsUp(string surface);
    void MarkDown(string surface, string? reason = null);
    void MarkUp(string surface);
}

public sealed class SurfaceAvailability : ISurfaceAvailability
{
    private readonly object _gate = new();
    private readonly HashSet<string> _down = new(StringComparer.OrdinalIgnoreCase);

    public bool IsUp(string surface)
    {
        var s = string.IsNullOrWhiteSpace(surface) ? "local" : surface.Trim();
        lock (_gate)
            return !_down.Contains(s);
    }

    public void MarkDown(string surface, string? reason = null)
    {
        var s = string.IsNullOrWhiteSpace(surface) ? "local" : surface.Trim();
        // local is never marked down — R&R / notes always can run locally.
        if (string.Equals(s, "local", StringComparison.OrdinalIgnoreCase))
            return;
        lock (_gate)
            _down.Add(s);
    }

    public void MarkUp(string surface)
    {
        var s = string.IsNullOrWhiteSpace(surface) ? "local" : surface.Trim();
        lock (_gate)
            _down.Remove(s);
    }
}
