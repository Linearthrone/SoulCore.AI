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

    /// <summary>
    /// UE WS server. Production body lives on Shadow (Tailscale MagicDNS: house-victoria).
    /// Override via appsettings / env UnrealBridge__WsUrl for local PIE.
    /// </summary>
    public string WsUrl { get; set; } = "ws://house-victoria:8888";

    /// <summary>Attempt connect on Host start. Failures must not crash Host.</summary>
    public bool ConnectOnStartup { get; set; } = true;

    /// <summary>Tailscale hops need more headroom than loopback.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 10;
}
