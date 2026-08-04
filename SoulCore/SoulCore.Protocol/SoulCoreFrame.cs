using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoulCore.Protocol;

/// <summary>
/// Canonical SoulCore WebSocket JSON frame envelope for Presence chat (:7700/ws).
/// Outbound Unreal body verbs use a separate UE <c>command</c> envelope via <c>UeVerbWireMapper</c>, not this shape.
/// </summary>
/// <remarks>
/// <para>
/// <b>JSON naming policy:</b> envelope fields use explicit <c>[JsonPropertyName]</c>
/// (<c>v</c>/<c>type</c>/<c>id</c>/<c>ts</c>/<c>payload</c>). Payload objects serialized via
/// <see cref="Create"/> use <see cref="JsonNamingPolicy.CamelCase"/> (single policy for all peers).
/// </para>
/// Envelope:
/// <code>
/// {
///   "v": 1,
///   "type": "chat.send" | "chat.delta" | "chat.done" | "emotion.snapshot" | "emotion.correct" | "presence.status" | "loop.tick" | "loop.tick.ok" | "loop.want" | "error" | "ping" | "pong",
///   "id": "client-or-server-correlation-id",
///   "ts": "2026-07-22T21:00:00.000Z",
///   "payload": { ... }
/// }
/// </code>
/// chat.send payload: { "text": "...", "sessionId": "optional" }
/// chat.delta payload: { "text": "partial" }
/// chat.done payload: { "text": "full reply optional", "proactive"?: bool, "contactId"?: "victoria", "hasMedia"?: bool, "mediaId"?: "…" }
/// emotion.snapshot payload: { "valence", "arousal", "dominance", "focus", "label", "note?", "revision" }
/// emotion.correct payload (client → Host): { "valence": -1..1, "arousal": 0..1, "dominance": 0..1, "focus": 0..1, "note?" }
/// presence.status payload: { "alive": true, "warm": true, "phase": 1 }
/// loop.tick payload (client → Host, tests): {} — invokes ISoulLoop.TickAsync when SoulLoop:Enabled
/// loop.tick.ok payload (Host → client, ack): { "ok": true } — correlates to loop.tick; does not carry want
/// loop.want payload (Host → client): { "want", "emotionLabel", "valence", "arousal", "episodicCount" } — sole want shape (hub broadcast)
/// error payload: { "code": "ws.unavailable", "message": "..." }
/// </remarks>
public sealed class SoulCoreFrame
{
    public const int ProtocolVersion = 1;

    [JsonPropertyName("v")]
    public int V { get; set; } = ProtocolVersion;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("ts")]
    public string Ts { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }

    /// <summary>
    /// Shared serializer: camelCase for payload property names; nulls omitted when writing.
    /// Envelope names remain fixed via <see cref="JsonPropertyNameAttribute"/>.
    /// </summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static SoulCoreFrame Create(string type, object? payload = null, string? id = null)
    {
        JsonElement? element = null;
        if (payload is not null)
        {
            element = JsonSerializer.SerializeToElement(payload, SerializerOptions);
        }

        return new SoulCoreFrame
        {
            V = ProtocolVersion,
            Type = type,
            Id = id ?? Guid.NewGuid().ToString("N"),
            Ts = DateTimeOffset.UtcNow.ToString("O"),
            Payload = element
        };
    }

    public string ToJson() =>
        JsonSerializer.Serialize(this, SerializerOptions);

    public static bool TryParse(string json, out SoulCoreFrame? frame)
    {
        try
        {
            frame = JsonSerializer.Deserialize<SoulCoreFrame>(json, SerializerOptions);
            return frame is not null && !string.IsNullOrWhiteSpace(frame.Type);
        }
        catch (JsonException)
        {
            frame = null;
            return false;
        }
    }
}

public static class SoulCoreFrameTypes
{
    public const string ChatSend = "chat.send";
    public const string ChatDelta = "chat.delta";
    public const string ChatDone = "chat.done";
    public const string EmotionSnapshot = "emotion.snapshot";
    /// <summary>Client → Host: user correction of felt emotion → persist + echo emotion.snapshot.</summary>
    public const string EmotionCorrect = "emotion.correct";
    public const string PresenceStatus = "presence.status";
    /// <summary>Client → Host: explicit loop tick for tests (no-op when SoulLoop:Enabled=false).</summary>
    public const string LoopTick = "loop.tick";
    /// <summary>Host → client: ack for loop.tick (no want payload; want arrives via loop.want broadcast).</summary>
    public const string LoopTickOk = "loop.tick.ok";
    /// <summary>Host → client: proposed want string from scaffold (session notify; full schema only).</summary>
    public const string LoopWant = "loop.want";
    public const string Error = "error";
    public const string Ping = "ping";
    public const string Pong = "pong";
}
