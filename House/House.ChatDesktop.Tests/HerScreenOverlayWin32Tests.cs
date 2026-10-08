using House.ChatDesktop.Controls;
using Xunit;

namespace House.ChatDesktop.Tests;

public class HerScreenOverlayWin32Tests
{
    [Fact]
    public void WithClickThrough_SetsTransparentLayeredNoActivateToolWindow()
    {
        var next = HerScreenOverlayWin32.WithClickThrough(0);
        Assert.Equal(
            HerScreenOverlayWin32.WsExTransparent
            | HerScreenOverlayWin32.WsExLayered
            | HerScreenOverlayWin32.WsExNoActivate
            | HerScreenOverlayWin32.WsExToolWindow,
            next);
    }

    [Fact]
    public void WithClickThrough_PreservesExistingBits()
    {
        const int existing = 0x00000008; // WS_EX_TOPMOST-ish placeholder bit
        var next = HerScreenOverlayWin32.WithClickThrough(existing);
        Assert.Equal(existing, next & existing);
        Assert.True((next & HerScreenOverlayWin32.WsExTransparent) != 0);
    }
}
