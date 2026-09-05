using SoulCore.Inference;

namespace SoulCore.Protocol.Tests;

/// <summary>PROP-8.1: ring-buffer session history — no RemoveAt(0) trim loop.</summary>
public class ChatSessionHistoryStoreTests
{
    [Fact]
    public void AppendTurn_TrimsOldest_WhenOverMax()
    {
        var store = new ChatSessionHistoryStore(maxMessages: 4);

        store.AppendTurn("s1", new[]
        {
            Msg("user", "m1"),
            Msg("assistant", "m2"),
            Msg("user", "m3"),
        });

        store.AppendTurn("s1", new[]
        {
            Msg("user", "m4"),
            Msg("assistant", "m5"),
        });

        var snapshot = store.GetMessages("s1");
        Assert.Equal(4, snapshot.Count);
        Assert.Equal("m2", snapshot[0].Content);
        Assert.Equal("m5", snapshot[^1].Content);
    }

    [Fact]
    public void GetMessages_ReturnsCopy_NotLiveView()
    {
        var store = new ChatSessionHistoryStore(maxMessages: 4);
        store.AppendTurn("s1", new[] { Msg("user", "hello") });

        var first = store.GetMessages("s1");
        store.AppendTurn("s1", new[] { Msg("assistant", "hi") });
        var second = store.GetMessages("s1");

        Assert.Single(first);
        Assert.Equal(2, second.Count);
    }

    [Fact]
    public void Clear_RemovesSession()
    {
        var store = new ChatSessionHistoryStore();
        store.AppendTurn("s1", new[] { Msg("user", "x") });
        store.Clear("s1");
        Assert.Empty(store.GetMessages("s1"));
    }

    private static ChatMessage Msg(string role, string content) =>
        new() { Role = role, Content = content };
}
