namespace SoulCore.Config;

/// <summary>
/// Non-secret host bind knobs. Default: loopback only (SEC-004).
/// </summary>
public sealed class HostBindOptions
{
    public const string SectionName = "Host";

    /// <summary>Must remain 127.0.0.1 for V1 unless SEC opt-in ticket.</summary>
    public string BindAddress { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 7700;
}
