namespace House.ChatDesktop.Services;

/// <summary>
/// Host stack button enablement while a service action is in flight.
/// Stop stays available during start/restart so the operator can force-kill.
/// </summary>
public static class HostStackBusyPolicy
{
    public static bool IsHostStopEnabled(bool busy, string? activeTag) =>
        !busy || activeTag is "host-start" or "host-restart";
}
