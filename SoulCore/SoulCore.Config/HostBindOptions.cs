namespace SoulCore.Config;

/// <summary>
/// Non-secret host bind knobs. Default: loopback only (SEC-004).
/// </summary>
public sealed class HostBindOptions
{
    public const string SectionName = "Host";

    /// <summary>Must remain 127.0.0.1 for V1 unless SEC opt-in ticket.</summary>
    public string BindAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// Default 7710 — matches Presence <c>ConnectionDefaults.DefaultPort</c> and
    /// <c>appsettings.json</c>. Legacy docs/examples that say 7700 are stale; do not
    /// reintroduce 7700 as the code default or Presence auto-start will look healthy
    /// while the desk connects to the wrong port.
    /// </summary>
    public int Port { get; set; } = 7710;
}
