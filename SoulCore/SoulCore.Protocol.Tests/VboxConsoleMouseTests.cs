using SoulCore.Inference.Tools.Desktop;
using Xunit;

namespace SoulCore.Protocol.Tests;

public class VboxConsoleMouseTests
{
    [Theory]
    [InlineData(0, 0, 1, 1, 1, VboxConsoleMouse.LeftButton)]
    [InlineData(509, 509, 1, 510, 510, VboxConsoleMouse.LeftButton)]
    [InlineData(10, 20, 3, 11, 21, VboxConsoleMouse.RightButton)]
    [InlineData(10, 20, 2, 11, 21, VboxConsoleMouse.MiddleButton)]
    public void PlanAbsolutePress_MapsOriginAndButtons(
        int x, int y, int btn, int expectAx, int expectAy, int expectFlags)
    {
        var (ax, ay, flags) = VboxConsoleMouse.PlanAbsolutePress(x, y, btn);
        Assert.Equal(expectAx, ax);
        Assert.Equal(expectAy, ay);
        Assert.Equal(expectFlags, flags);
    }

    [Fact]
    public void TryClick_OnNonWindows_ReturnsNull()
    {
        if (OperatingSystem.IsWindows())
            return; // CI is Linux — exercise the null path there.

        var result = VboxConsoleMouse.TryClick("victoria-sandbox", 510, 510, 1, 1);
        Assert.Null(result);
    }
}
