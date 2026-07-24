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
}
