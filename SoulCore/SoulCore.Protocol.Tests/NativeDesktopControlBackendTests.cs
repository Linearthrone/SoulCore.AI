using System.Diagnostics;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

/// <summary>PROP-6.1: NativeDesktopControlBackend drag uses cancellable Task.Delay, not Thread.Sleep.</summary>
public class NativeDesktopControlBackendTests
{
    private static readonly string BackendSourcePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "SoulCore.Inference", "Tools", "Desktop", "NativeDesktopControlBackend.cs"));

    [Fact]
    public void DragAsync_SourceHasNoThreadSleep()
    {
        var source = File.ReadAllText(BackendSourcePath);
        Assert.DoesNotContain("Thread.Sleep", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DragAsync_SourceUsesCancellableTaskDelay()
    {
        var source = File.ReadAllText(BackendSourcePath);
        Assert.Contains("await Task.Delay(15, ct)", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DragAsync_HonorsCancellationBeforeStart()
    {
        var backend = new NativeDesktopControlBackend();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            backend.DragAsync(0, 0, 100, 100, "left", cts.Token));
    }

    [Fact]
    public async Task DragAsync_OnNonWindows_CompletesWithoutBlocking()
    {
        if (OperatingSystem.IsWindows())
            return;

        var backend = new NativeDesktopControlBackend();
        var sw = Stopwatch.StartNew();
        var result = await backend.DragAsync(0, 0, 100, 100, "left");
        sw.Stop();

        Assert.False(result.Success);
        Assert.Contains("requires Windows", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"expected immediate non-Windows stub, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task DragAsync_ReturnsCompletedTaskWithoutSyncBlock_OnNonWindows()
    {
        if (OperatingSystem.IsWindows())
            return;

        var backend = new NativeDesktopControlBackend();
        var dragTask = backend.DragAsync(0, 0, 50, 50, "left");

        // A sync-blocking ~300ms drag would prevent this yield from running first.
        var raced = await Task.WhenAny(dragTask, Task.Delay(5));
        Assert.Same(dragTask, raced);

        var result = await dragTask;
        Assert.False(result.Success);
    }
}
