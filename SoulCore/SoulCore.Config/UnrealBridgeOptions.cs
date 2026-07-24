namespace SoulCore.Config;

/// <summary>
/// Outbound Unreal Engine WS client knobs. SoulCore connects to UE's server (default :8888).
/// Canonical UE project for :8888 is still an open product freeze — keep defaults documented.
/// </summary>
public sealed class UnrealBridgeOptions
{
    public const string SectionName = "UnrealBridge";

    /// <summary>When false, Host registers a no-op client (verbs are logged / ignored).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>UE WS server. Loopback default; LAN/Tailscale override later via config.</summary>
    public string WsUrl { get; set; } = "ws://127.0.0.1:8888";

    /// <summary>Attempt connect on Host start. Failures must not crash Host.</summary>
    public bool ConnectOnStartup { get; set; } = true;

    public int ConnectTimeoutSeconds { get; set; } = 2;
}
