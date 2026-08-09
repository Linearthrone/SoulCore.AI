namespace SoulCore.Config;

/// <summary>
/// Autonomy loop (want→act) scaffold knobs. Kill switch: <see cref="Enabled"/> defaults false.
/// </summary>
public sealed class SoulLoopOptions
{
    public const string SectionName = "SoulLoop";

    /// <summary>
    /// When false (default), <c>ISoulLoop.TickAsync</c> is a no-op and the hosted timer does not run.
    /// Set true only for local scaffold / evidence — no high-agency external acts.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Background tick interval when <see cref="Enabled"/> is true.
    /// Explicit WS <c>loop.tick</c> still works for tests regardless of this interval.
    /// </summary>
    public int TickIntervalSeconds { get; set; } = 60;

    /// <summary>How many recent episodic rows to summarize into the want proposal.</summary>
    public int EpisodicRecallLimit { get; set; } = 3;

    /// <summary>
    /// Write an episodic self-reflection memory every Nth tick (default 5, ~5 min at 60s ticks).
    /// Set to 0 to disable loop-authored reflection writes entirely.
    /// </summary>
    public int ReflectionIntervalTicks { get; set; } = 5;

    /// <summary>
    /// When true, emit an unsolicited <c>chat.done</c> to companion WS clients on a throttle
    /// (see <see cref="ProactiveChatIntervalTicks"/>). Defaults <c>false</c>: phrase-bank
    /// pings are not real model speech and spam the transcript / phone. Re-enable only when
    /// proactive text is model-authored, not scaffold category lines.
    /// </summary>
    public bool ProactiveChatEnabled { get; set; } = false;

    /// <summary>
    /// Push a proactive chat message every Nth tick when <see cref="ProactiveChatEnabled"/>.
    /// Default 0 (off). Historical scaffold used 5 (~5 min at 60s ticks).
    /// </summary>
    public int ProactiveChatIntervalTicks { get; set; } = 0;
}
