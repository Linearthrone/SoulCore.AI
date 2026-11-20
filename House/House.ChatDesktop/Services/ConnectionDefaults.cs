namespace House.ChatDesktop.Services;

/// <summary>
/// Localhost-only defaults. No secrets — endpoints only.
/// Override via env HOUSE_SOULCORE_HOST / HOUSE_SOULCORE_PORT if needed.
/// PROP-22.9: Settings → Advanced can also pin a client port in ui-settings.json
/// (<see cref="LocalUiSettings.ClientPort"/>); env still wins. The bind host is always loopback.
/// </summary>
public static class ConnectionDefaults
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 7710;
    public const string HealthPath = "/health";
    public const string WsPath = "/ws";

    public static string Host { get; } =
        Environment.GetEnvironmentVariable("HOUSE_SOULCORE_HOST") is { Length: > 0 } h
            ? h
            : DefaultHost;

    private static int _port = ResolvePort();

    /// <summary>Current client port. Changes at runtime only through <see cref="UseClientPort"/>.</summary>
    public static int Port => _port;

    /// <summary>True when <c>HOUSE_SOULCORE_PORT</c> pins the port (env always wins).</summary>
    public static bool PortPinnedByEnvironment =>
        int.TryParse(Environment.GetEnvironmentVariable("HOUSE_SOULCORE_PORT"), out var env) && env is > 0 and < 65536;

    /// <summary>Apply a new client port for this process (Settings → Advanced). Does not touch the Host.</summary>
    public static bool UseClientPort(int port)
    {
        if (port is < 1 or > 65535 || PortPinnedByEnvironment)
            return false;
        _port = port;
        return true;
    }

    /// <summary>Reject non-loopback binds — thin client must not target remote hosts by default.</summary>
    public static bool IsLocalLoopback(string host) =>
        host is "127.0.0.1" or "localhost" or "::1";

    public static Uri HealthUri =>
        new($"http://{Host}:{Port}{HealthPath}");

    public static Uri WsUri =>
        new($"ws://{Host}:{Port}{WsPath}");

    public static string DisplayEndpoint => $"{Host}:{Port}";

    /// <summary>Base URL for Host REST routes (no trailing slash).</summary>
    public static string HttpBase => $"http://{Host}:{Port}";

    private static int ResolvePort()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("HOUSE_SOULCORE_PORT"), out var env)
            && env is > 0 and < 65536)
        {
            return env;
        }

        try
        {
            if (LocalUiSettings.Load().ClientPort is int saved && saved is > 0 and < 65536)
                return saved;
        }
        catch
        {
            // Local prefs must never block startup.
        }

        return DefaultPort;
    }
}
