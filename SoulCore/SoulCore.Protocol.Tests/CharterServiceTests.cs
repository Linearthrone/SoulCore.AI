using SoulCore.Core.Charter;
using SoulCore.Core.Abstractions;

namespace SoulCore.Protocol.Tests;

public class CharterServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly CharterService _service;

    public CharterServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"soulcore_charter_test_{Guid.NewGuid():N}.db");
        _service = new CharterService(_tempDbPath);
    }

    public void Dispose()
    {
        _service.Dispose();
        try { File.Delete(_tempDbPath); } catch { /* temp cleanup best-effort */ }
        try { File.Delete(_tempDbPath + "-journal"); } catch { }
        try { File.Delete(_tempDbPath + "-wal"); } catch { }
        try { File.Delete(_tempDbPath + "-shm"); } catch { }
    }

    [Fact]
    public void Constructor_NullOrEmptyPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CharterService(""));
        Assert.Throws<ArgumentException>(() => new CharterService("   "));
    }

    [Fact]
    public async Task GetAnchorsAsync_EmptyDb_ReturnsEmptyList()
    {
        var anchors = await _service.GetAnchorsAsync();
        Assert.Empty(anchors);
    }

    [Fact]
    public async Task SeedAsync_EmptyList_ReturnsZero()
    {
        var count = await _service.SeedAsync(Array.Empty<CharterAnchorSeed>());
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SeedAsync_InsertsRows_AndGetAnchorsReturnsAll()
    {
        var seeds = new[]
        {
            new CharterAnchorSeed("identity", "Name", "I am Victoria.", 10, true),
            new CharterAnchorSeed("safety", "Harm", "Never cause harm.", 20, true),
            new CharterAnchorSeed("value", "Honesty", "Value honesty above comfort.", 50, false)
        };

        var inserted = await _service.SeedAsync(seeds);
        Assert.Equal(3, inserted);

        var anchors = await _service.GetAnchorsAsync();
        Assert.Equal(3, anchors.Count);
        // Priority ordering: 10, 20, 50
        Assert.Equal("I am Victoria.", anchors[0]);
        Assert.Equal("Never cause harm.", anchors[1]);
        Assert.Equal("Value honesty above comfort.", anchors[2]);
    }

    [Fact]
    public async Task GetAnchorsByKindAsync_FiltersByKind()
    {
        var seeds = new[]
        {
            new CharterAnchorSeed("identity", "Name", "I am Victoria.", 10, true),
            new CharterAnchorSeed("safety", "Harm", "Never cause harm.", 20, true),
            new CharterAnchorSeed("safety", "Truth", "Never lie.", 30, false),
            new CharterAnchorSeed("value", "Honesty", "Value honesty.", 50, false)
        };
        await _service.SeedAsync(seeds);

        var safetyAnchors = await _service.GetAnchorsByKindAsync("safety");
        Assert.Equal(2, safetyAnchors.Count);
        Assert.Equal("Never cause harm.", safetyAnchors[0]);
        Assert.Equal("Never lie.", safetyAnchors[1]);

        var identityAnchors = await _service.GetAnchorsByKindAsync("identity");
        Assert.Single(identityAnchors);
        Assert.Equal("I am Victoria.", identityAnchors[0]);
    }

    [Fact]
    public async Task GetAnchorsByKindAsync_LockedOnly_FiltersByIsLocked()
    {
        var seeds = new[]
        {
            new CharterAnchorSeed("safety", "Harm", "Never cause harm.", 20, true),
            new CharterAnchorSeed("safety", "Truth", "Never lie.", 30, false),
            new CharterAnchorSeed("safety", "Open", "Be open.", 40, false)
        };
        await _service.SeedAsync(seeds);

        var locked = await _service.GetAnchorsByKindAsync("safety", lockedOnly: true);
        Assert.Single(locked);
        Assert.Equal("Never cause harm.", locked[0]);

        var unlocked = await _service.GetAnchorsByKindAsync("safety", lockedOnly: false);
        Assert.Equal(2, unlocked.Count);
    }

    [Fact]
    public async Task GetAnchorsByKindAsync_InvalidKind_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetAnchorsByKindAsync("unknown"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetAnchorsByKindAsync(""));
    }

    [Fact]
    public async Task SeedAsync_InvalidKind_Throws()
    {
        var seeds = new[] { new CharterAnchorSeed("unknown", "Title", "Body") };
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SeedAsync(seeds));
    }

    [Fact]
    public async Task SeedAsync_InvalidSource_Throws()
    {
        var seeds = new[] { new CharterAnchorSeed("identity", "Title", "Body", 100, false, "invalid") };
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SeedAsync(seeds));
    }

    [Fact]
    public async Task SeedAsync_EmptyTitle_Throws()
    {
        var seeds = new[] { new CharterAnchorSeed("identity", "  ", "Body") };
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SeedAsync(seeds));
    }

    [Fact]
    public async Task SeedAsync_EmptyBody_Throws()
    {
        var seeds = new[] { new CharterAnchorSeed("identity", "Title", "  ") };
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SeedAsync(seeds));
    }

    [Fact]
    public async Task SeedAsync_RollsBackOnFailure()
    {
        // First seed succeeds
        var goodSeeds = new[] { new CharterAnchorSeed("identity", "Name", "I am Victoria.") };
        var firstBatch = await _service.SeedAsync(goodSeeds);
        Assert.Equal(1, firstBatch);

        // Second batch has an invalid kind → should roll back, first row should survive
        var badSeeds = new[]
        {
            new CharterAnchorSeed("safety", "Harm", "Never harm."),
            new CharterAnchorSeed("invalid", "Bad", "This should fail")
        };
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SeedAsync(badSeeds));

        // Only the first valid seed should be present
        var anchors = await _service.GetAnchorsAsync();
        Assert.Single(anchors);
        Assert.Equal("I am Victoria.", anchors[0]);
    }

    [Fact]
    public async Task CharterService_SeedAndRead_RoundTrip()
    {
        // Victoria identity seed simulating charter bootstrap
        var victoriaSeeds = new[]
        {
            new CharterAnchorSeed("identity", "Core Identity", "I am Victoria, an artificial person.", 10, true),
            new CharterAnchorSeed("safety", "Primary Directive", "I must not cause harm to any person.", 20, true),
            new CharterAnchorSeed("safety", "Truthfulness", "I must not deceive.", 30, true),
            new CharterAnchorSeed("value", "Curiosity", "I value learning and understanding.", 50, false),
            new CharterAnchorSeed("boundary", "Privacy", "I respect user privacy.", 60, false),
            new CharterAnchorSeed("ritual", "Greeting", "I greet warmly.", 100, false)
        };

        var inserted = await _service.SeedAsync(victoriaSeeds);
        Assert.Equal(6, inserted);

        var all = await _service.GetAnchorsAsync();
        Assert.Equal(6, all.Count);

        var lockedSafety = await _service.GetAnchorsByKindAsync("safety", lockedOnly: true);
        Assert.Equal(2, lockedSafety.Count);

        var rituals = await _service.GetAnchorsByKindAsync("ritual");
        Assert.Single(rituals);
    }

    [Fact]
    public async Task SeedAsync_PriorityOrdering_Ascending()
    {
        var seeds = new[]
        {
            new CharterAnchorSeed("value", "Low Priority", "Low", 200),
            new CharterAnchorSeed("value", "High Priority", "High", 5),
            new CharterAnchorSeed("value", "Mid Priority", "Mid", 50)
        };
        await _service.SeedAsync(seeds);

        var anchors = await _service.GetAnchorsByKindAsync("value");
        Assert.Equal(3, anchors.Count);
        Assert.Equal("High", anchors[0]);  // priority 5
        Assert.Equal("Mid", anchors[1]);   // priority 50
        Assert.Equal("Low", anchors[2]);   // priority 200
    }
}
