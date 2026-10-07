using SoulCore.Inference.Tools.Desktop;
using Xunit;

namespace SoulCore.Protocol.Tests;

public class VictoriaVirtualBoxWindowLocatorTests
{
    [Theory]
    [InlineData(@"C:\Program Files\Oracle\VirtualBox\VirtualBoxVM.exe", true)]
    [InlineData(@"C:\Program Files\Oracle\VirtualBox\VirtualBox.exe", true)]
    [InlineData(@"C:\Program Files\Oracle\VirtualBox\VBoxSVC.exe", false)]
    [InlineData(@"C:\Program Files\Google\Chrome\Application\chrome.exe", false)]
    [InlineData(@"C:\Users\kurt\AppData\Local\ms-playwright\chromium-1148\chrome-win\chrome.exe", false)]
    public void IsVirtualBoxUiProcess_FiltersExe(string path, bool expected) =>
        Assert.Equal(expected, VictoriaVirtualBoxWindowLocator.IsVirtualBoxUiProcess(path));

    [Fact]
    public void TryFind_OnNonWindowsOrNoVm_ReturnsNullSafely()
    {
        // CI/Linux: no VirtualBox UI - must not throw.
        var found = VictoriaVirtualBoxWindowLocator.TryFind("victoria-sandbox");
        if (!OperatingSystem.IsWindows())
            Assert.Null(found);
    }

    [Fact]
    public void ClearCache_DoesNotThrow()
    {
        VictoriaVirtualBoxWindowLocator.ClearCache();
        VictoriaVirtualBoxWindowLocator.ClearCache();
    }
}
