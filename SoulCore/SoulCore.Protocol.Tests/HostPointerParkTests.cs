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

    [Fact]
    public void BeginAwayFromVm_Dispose_DoesNotThrow()
    {
        using var park = HostPointerPark.BeginAwayFromVm();
        Assert.NotNull(park);
    }

    [Fact]
    public void PickParkPoint_SkipsCandidatesInsideExclude()
    {
        var (prefX, prefY) = HostPointerPark.PickParkPoint(Array.Empty<HostPointerPark.Rect>());
        var excludes = new[]
        {
            new HostPointerPark.Rect(prefX - 2, prefY - 2, prefX + 3, prefY + 3)
        };
        var (x, y) = HostPointerPark.PickParkPoint(excludes);
        Assert.False(HostPointerPark.IsInsideAny(x, y, excludes));
        Assert.False(x == prefX && y == prefY);
    }

    [Fact]
    public void IsInsideAny_RespectsHalfOpenBounds()
    {
        var r = new HostPointerPark.Rect(10, 20, 30, 40);
        Assert.True(r.Contains(10, 20));
        Assert.False(r.Contains(30, 20));
        Assert.False(r.Contains(10, 40));
        Assert.True(HostPointerPark.IsInsideAny(15, 25, new[] { r }));
        Assert.False(HostPointerPark.IsInsideAny(0, 0, new[] { r }));
    }

    [Fact]
    public void BeginAwayFrom_WithExcludes_Dispose_DoesNotThrow()
    {
        var excludes = new[] { new HostPointerPark.Rect(0, 0, 100, 100) };
        using var park = HostPointerPark.BeginAwayFrom(excludes);
        Assert.NotNull(park);
    }
}
