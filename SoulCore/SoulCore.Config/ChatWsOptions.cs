namespace SoulCore.Config;

/// <summary>
/// Chat WebSocket handler knobs (Presence → SoulCore protocol path).
/// </summary>
public sealed class ChatWsOptions
{
    public const string SectionName = "ChatWs";

    /// <summary>Path on the Host Kestrel listener (same port as /health). Default /ws.</summary>
    public string Path { get; set; } = "/ws";

    /// <summary>Prefer Hermes over Ollama when both Enabled.</summary>
    public bool PreferHermes { get; set; } = false;

    /// <summary>
    /// When models are down / throw, reply with a stub chat.delta + chat.done.
    /// Default false: emit <c>error</c> frame with code <c>chat.model_down</c> instead of pretending success.
    /// </summary>
    public bool StubWhenModelDown { get; set; } = false;

    /// <summary>
    /// When <c>true</c> (default), <c>HandleChatSendAsync</c> routes the chat
    /// turn through the agent tool-loop (<c>IInferenceClient.CompleteWithToolsAsync</c>
    /// or <c>IHermesClient.CompleteWithToolsAsync</c> when <see cref="PreferHermes"/>
    /// + Hermes.Enabled), with <c>tools</c> built from <c>IToolRegistry</c>.
    /// When <c>false</c>, falls back to the single-shot <c>CompleteAsync</c> /
    /// <c>ChatAsync</c> path + keyword detectors (pre-tool-loop behavior) —
    /// no regression, useful for debug/kill-switch.
    /// <para>
    /// With no tools registered, the tool-loop path behaves like single-shot
    /// (empty <c>tools[]</c> → model returns text in one round-trip), so the
    /// default <c>true</c> is safe even before Phase B tools ship (BED-131+).
    /// </para>
    /// </summary>
    public bool UseToolLoop { get; set; } = true;

    /// <summary>
    /// Max messages retained per client <c>sessionId</c> for multi-turn tool/chat
    /// history (BED-158 / ISSUE-004). Oldest messages are trimmed first. Must be
    /// ≥ 2. Default 40 ≈ ~10–20 turns including tool traces.
    /// </summary>
    public int MaxSessionHistoryMessages { get; set; } = 40;
}
