namespace House.ChatDesktop.Services;

/// <summary>
/// Localhost-only defaults. No secrets — endpoints only.
/// Override via env HOUSE_SOULCORE_HOST / HOUSE_SOULCORE_PORT if needed.
/// </summary>
public static class ConnectionDefaults
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 7700;
    public const string HealthPath = "/health";
    public const string WsPath = "/ws";

    public static string Host { get; } =
        Environment.GetEnvironmentVariable("HOUSE_SOULCORE_HOST") is { Length: > 0 } h
            ? h
            : DefaultHost;

    public static int Port { get; } =
        int.TryParse(Environment.GetEnvironmentVariable("HOUSE_SOULCORE_PORT"), out var p) && p is > 0 and < 65536
            ? p
            : DefaultPort;

    /// <summary>Reject non-loopback binds — thin client must not target remote hosts by default.</summary>
    public static bool IsLocalLoopback(string host) =>
        host is "127.0.0.1" or "localhost" or "::1";

    public static Uri HealthUri =>
        new($"http://{Host}:{Port}{HealthPath}");

    public static Uri WsUri =>
        new($"ws://{Host}:{Port}{WsPath}");

    public static string DisplayEndpoint => $"{Host}:{Port}";
}
