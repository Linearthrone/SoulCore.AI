namespace SoulCore.Core.Abstractions;

/// <summary>
/// Continuous self loop (want → act). Phase-2 scaffold: tick may propose a <b>want</b> string only.
/// No Executor / browser / MT4 / email / file acts. High-agency paths stay off.
/// </summary>
public interface ISoulLoop
{
    /// <summary>Mirrors <c>SoulLoop:Enabled</c> (default false — kill switch).</summary>
    bool IsEnabled { get; }

    /// <summary>Last proposed want after a successful enabled tick; null if never ticked or disabled.</summary>
    string? LastWant { get; }

    /// <summary>
    /// One loop tick. When disabled: no emotion/episodic read and no want emission.
    /// When enabled: read emotion + recent episodic → propose want (log / optional WS notify).
    /// </summary>
    Task TickAsync(CancellationToken cancellationToken = default);
}
