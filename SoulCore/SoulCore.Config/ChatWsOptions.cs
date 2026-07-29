namespace SoulCore.Config;

/// <summary>
/// Chat WebSocket handler knobs (Presence → SoulCore protocol path).
/// </summary>
public sealed class ChatWsOptions
{
    public const string SectionName = "ChatWs";

    /// <summary>Path on the Host Kestrel listener (same port as /health). Default /ws.</summary>
    public string Path { get; set; } = "/ws";

    /// <summary>
    /// Prefer Hermes MCP readiness for hermes-backend tools when both Hermes and
    /// Inference are enabled.
    /// <para>
    /// BED-164 Avenue B: PreferHermes tool-loop runs on <b>Ollama</b>
    /// (<c>IInferenceClient.CompleteWithToolsAsync</c>). Hermes is <b>MCP-only</b>
    /// via <c>CallMcpToolAsync</c> — PreferHermes turns must <b>never</b> send
    /// <c>tools[]</c> through Hermes <c>CompleteWithToolsAsync</c> (hermes-agent
    /// 0.18.2 is <c>tool_execution: server</c>). Gateway/key failure is fail-fast
    /// before the Ollama loop (no silent PreferHermes bypass).
    /// </para>
    /// </summary>
    public bool PreferHermes { get; set; } = false;

    /// <summary>
    /// When models are down / throw, reply with a stub chat.delta + chat.done.
    /// Default false: emit <c>error</c> frame with code <c>chat.model_down</c> instead of pretending success.
    /// </summary>
    public bool StubWhenModelDown { get; set; } = false;

    /// <summary>
    /// When <c>true</c> (default), <c>HandleChatSendAsync</c> routes the chat
    /// turn through the agent tool-loop (<c>IInferenceClient.CompleteWithToolsAsync</c>
    /// on Ollama; Hermes <c>CompleteWithToolsAsync</c> only as PreferHermes=false
    /// secondary failover), with <c>tools</c> built from <c>IToolRegistry</c>.
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
    /// BED-158: max messages retained per chat session for multi-turn pronouns
    /// / tool history. Values &lt; 2 fall back to 40 at DI wiring time.
    /// </summary>
    public int MaxSessionHistoryMessages { get; set; } = 40;
}
