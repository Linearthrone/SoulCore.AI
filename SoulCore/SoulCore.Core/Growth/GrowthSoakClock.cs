namespace SoulCore.Core.Growth;

/// <summary>
/// PROP-20.37 soak: 14-day display-only wall clock from soak start; open only when
/// ≥3 distinct contact days fall inside that window.
/// </summary>
public static class GrowthSoakClock
{
    public static readonly TimeSpan DisplayOnlyDuration = TimeSpan.FromDays(14);

    public const int MinContactDaysToOpen = 3;

    public static readonly TimeSpan FirstWeekAfterOpen = TimeSpan.FromDays(7);

    public enum SoakPhase
    {
        /// <summary>Counters visible; no prompt/routing change from bond.</summary>
        DisplayOnly = 0,

        /// <summary>14d elapsed but contact-day gate failed — still closed.</summary>
        GateClosed = 1,

        /// <summary>Open for counter-driven nudges on growth traits.</summary>
        Open = 2,
    }

    public readonly record struct SoakSnapshot(
        SoakPhase Phase,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset UtcNow,
        int DaysElapsed,
        int DaysRemaining,
        int ContactDaysInWindow,
        bool ContactGateMet,
        bool IsFirstWeekAfterOpen,
        bool MayChangePrompt);

    public static SoakSnapshot Evaluate(
        DateTimeOffset startedAtUtc,
        DateTimeOffset utcNow,
        int contactDaysInWindow)
    {
        if (utcNow < startedAtUtc)
            utcNow = startedAtUtc;

        var elapsed = utcNow - startedAtUtc;
        var daysElapsed = (int)Math.Floor(elapsed.TotalDays);
        var daysRemaining = Math.Max(0, (int)Math.Ceiling((DisplayOnlyDuration - elapsed).TotalDays));
        var gateMet = contactDaysInWindow >= MinContactDaysToOpen;
        var wallComplete = elapsed >= DisplayOnlyDuration;

        SoakPhase phase;
        if (!wallComplete)
            phase = SoakPhase.DisplayOnly;
        else if (!gateMet)
            phase = SoakPhase.GateClosed;
        else
            phase = SoakPhase.Open;

        var mayChangePrompt = phase == SoakPhase.Open;
        var firstWeek = mayChangePrompt
            && (utcNow - startedAtUtc.Add(DisplayOnlyDuration)) < FirstWeekAfterOpen;

        return new SoakSnapshot(
            phase,
            startedAtUtc,
            utcNow,
            daysElapsed,
            daysRemaining,
            contactDaysInWindow,
            gateMet,
            firstWeek,
            mayChangePrompt);
    }

    public static string PhaseLabel(SoakPhase phase) => phase switch
    {
        SoakPhase.DisplayOnly => "display_only",
        SoakPhase.GateClosed => "gate_closed",
        SoakPhase.Open => "open",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };
}
