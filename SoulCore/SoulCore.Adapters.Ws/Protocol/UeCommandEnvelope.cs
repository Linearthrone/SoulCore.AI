using System.Text.Json;
using System.Text.Json.Serialization;
using SoulCore.Protocol;

namespace SoulCore.Adapters.Ws.Protocol;

/// <summary>
/// Native HouseVictoriaBridge wire frame (MyProject plugin).
/// Parsed by <c>ParseWebSocketMessage</c>: verb from <c>payload.name</c> when <c>type=="command"</c>.
/// </summary>
public sealed class UeCommandEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "command";

    [JsonPropertyName("payload")]
    public UeCommandPayload Payload { get; set; } = new();

    public string ToJson() =>
        JsonSerializer.Serialize(this, SoulCoreFrame.SerializerOptions);
}

public sealed class UeCommandPayload
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public JsonElement? Args { get; set; }
}
