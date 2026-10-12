using SoulCore.Memory;

namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-17.3: when nothing runnable <c>in_progress</c>/<c>todo</c> has a live surface,
/// enter R&amp;R. Suspended rows never block.
/// </summary>
public static class LeisureEligibility
{
    /// <param name="runningId">Current assigned-work pointer, if any.</param>
    /// <param name="runningTask">Row for <paramref name="runningId"/>, if loaded.</param>
    /// <param name="ledger">All tasks (or at least todo/in_progress); suspended may be present.</param>
    /// <param name="surfaceUp">Returns true when that surface can run released work.</param>
    /// <param name="utcNow">Clock for parked probe due checks.</param>
    public static bool CanEnterRestAndResearch(
        long? runningId,
        VictoriaTask? runningTask,
        IReadOnlyList<VictoriaTask> ledger,
        Func<string, bool> surfaceUp,
        DateTimeOffset? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(surfaceUp);
        var now = utcNow ?? DateTimeOffset.UtcNow;

        if (runningId is > 0 && runningTask is not null)
        {
            if (string.Equals(runningTask.Status, "in_progress", StringComparison.OrdinalIgnoreCase)
                && surfaceUp(NormalizeSurface(runningTask.Surface)))
                return false;

            if (string.Equals(runningTask.Status, "parked", StringComparison.OrdinalIgnoreCase)
                && IsParkProbeDue(runningTask, now))
                return false;
        }

        foreach (var row in ledger)
        {
            if (!VictoriaTaskRules.IsSchedulable(row.Status))
                continue;
            if (surfaceUp(NormalizeSurface(row.Surface)))
                return false;
        }

        return true;
    }

    public static bool IsParkProbeDue(VictoriaTask task, DateTimeOffset utcNow)
    {
        if (!string.Equals(task.Status, "parked", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(task.ParkUntil))
            return false;
        if (!DateTimeOffset.TryParse(task.ParkUntil, out var until))
            return false;
        return until <= utcNow;
    }

    private static string NormalizeSurface(string? surface) =>
        string.IsNullOrWhiteSpace(surface) ? "local" : surface.Trim().ToLowerInvariant();
}
