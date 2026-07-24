using System.Text.Json;
using SoulCore.Protocol;

namespace SoulCore.Protocol.Tests;

public class SoulCoreFrameRoundTripTests
{
    [Fact]
    public void ChatSend_Create_ToJson_TryParse_RoundTrips()
    {
        var original = SoulCoreFrame.Create(
            SoulCoreFrameTypes.ChatSend,
            new { Text = "hello soul", SessionId = "sess-1" },
            id: "id-send-1");

        var json = original.ToJson();
        Assert.Contains("\"type\":\"chat.send\"", json);
        Assert.Contains("\"text\":\"hello soul\"", json);
        Assert.Contains("\"sessionId\":\"sess-1\"", json);

        Assert.True(SoulCoreFrame.TryParse(json, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(SoulCoreFrameTypes.ChatSend, parsed!.Type);
        Assert.Equal("id-send-1", parsed.Id);
        Assert.Equal(SoulCoreFrame.ProtocolVersion, parsed.V);
        Assert.True(parsed.Payload.HasValue);
        Assert.Equal("hello soul", parsed.Payload!.Value.GetProperty("text").GetString());
        Assert.Equal("sess-1", parsed.Payload.Value.GetProperty("sessionId").GetString());
    }

    [Fact]
    public void ChatDone_Create_ToJson_TryParse_RoundTrips()
    {
        var original = SoulCoreFrame.Create(
            SoulCoreFrameTypes.ChatDone,
            new { Text = "full reply", Stub = false, Provider = "ollama" },
            id: "id-done-1");

        var json = original.ToJson();
        Assert.Contains("\"type\":\"chat.done\"", json);
        Assert.Contains("\"provider\":\"ollama\"", json);

        Assert.True(SoulCoreFrame.TryParse(json, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(SoulCoreFrameTypes.ChatDone, parsed!.Type);
        Assert.Equal("id-done-1", parsed.Id);
        Assert.True(parsed.Payload.HasValue);

        using var doc = JsonDocument.Parse(parsed.Payload!.Value.GetRawText());
        Assert.Equal("full reply", doc.RootElement.GetProperty("text").GetString());
        Assert.False(doc.RootElement.GetProperty("stub").GetBoolean());
        Assert.Equal("ollama", doc.RootElement.GetProperty("provider").GetString());
    }
}
