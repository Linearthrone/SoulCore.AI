using SoulCore.Inference.Tools.Desktop;
using Xunit;

namespace SoulCore.Protocol.Tests;

public class HostPointerParkTests
{
    [Fact]
    public void Begin_Dispose_DoesNotThrow()
    {
        // On Linux CI this is a no-op; on Windows it parks + restores.
        using var park = HostPointerPark.Begin();
        Assert.NotNull(park);
    }
}
