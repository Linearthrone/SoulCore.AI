namespace SoulCore.Config;

/// <summary>
/// Chat WebSocket handler knobs (Presence → SoulCore protocol path).
/// </summary>
public sealed class ChatWsOptions
{
    public const string SectionName = "ChatWs";
    public string Path { get; set; } = "/ws";
    public bool StubWhenModelDown { get; set; } = false;
    public bool UseToolLoop { get; set; } = true;
    public int MaxSessionHistoryMessages { get; set; } = 40;
}
