using SoulCore.Host.Companion;

namespace SoulCore.Protocol.Tests;

public class CompanionCallSessionStoreTests
{
    [Fact]
    public void Start_CreatesFramesModeSession()
    {
        var store = new CompanionCallSessionStore();
        var s = store.Start("victoria");
        Assert.StartsWith("call_", s.SessionId);
        Assert.Equal("victoria", s.ContactId);
        Assert.Equal("frames", s.Mode);
        Assert.False(s.WebrtcAvailable);
        Assert.True(store.TryGet(s.SessionId, out var got));
        Assert.Equal(s.SessionId, got.SessionId);
    }

    [Fact]
    public void End_RemovesSession()
    {
        var store = new CompanionCallSessionStore();
        var s = store.Start(null);
        Assert.True(store.End(s.SessionId));
        Assert.False(store.TryGet(s.SessionId, out _));
        Assert.False(store.End(s.SessionId));
    }
}
