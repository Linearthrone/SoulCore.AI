using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

/// <summary>BED-188: hard-scope desktop ops to a VM/window title substring.</summary>
public class ScopedDesktopControlBackendTests
{
    private const string VmTitle = "victoria-sandbox [Running] - Oracle VirtualBox";
    private const string Scope = "victoria-sandbox";

    private static string ListContent(params (string title, int x, int y, int w, int h)[] windows)
    {
        var lines = new List<string> { "open desktop windows:" };
        for (var i = 0; i < windows.Length; i++)
        {
            var (title, x, y, w, h) = windows[i];
            var cx = x + w / 2;
            var cy = y + h / 2;
            lines.Add($"[{i}] {title} bounds=({x},{y} {w}x{h}) center=({cx},{cy})");
        }
        return string.Join("\n", lines);
    }

    private static ScopedDesktopControlBackend MakeScoped(RecordingBackend inner)
    {
        inner.ListWindowsResult = new DesktopOpResult(
            true,
            ListContent(
                ("Notepad", 0, 0, 400, 300),
                (VmTitle, 100, 50, 1280, 800),
                ("Slack", 50, 50, 200, 200)),
            null);
        return new ScopedDesktopControlBackend(inner, Scope);
    }

    [Fact]
    public void ParseWindows_ReadsBoundsLines()
    {
        var hits = ScopedDesktopControlBackend.ParseWindows(ListContent((VmTitle, 10, 20, 30, 40)));
        Assert.Single(hits);
        Assert.Equal(VmTitle, hits[0].Title);
        Assert.Equal(10, hits[0].X);
        Assert.Equal(20, hits[0].Y);
        Assert.Equal(30, hits[0].Width);
        Assert.Equal(40, hits[0].Height);
    }

    [Fact]
    public async Task ListWindows_FiltersToScopeOnly()
    {
        var inner = new RecordingBackend();
        var scoped = MakeScoped(inner);

        var result = await scoped.ListWindowsAsync();

        Assert.True(result.Success);
        Assert.Contains(VmTitle, result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Notepad", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Slack", result.Content, StringComparison.Ordinal);
        Assert.Contains("scoped", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Click_InsideBounds_Dispatches()
    {
        var inner = new RecordingBackend();
        var scoped = MakeScoped(inner);

        var result = await scoped.ClickAsync(740, 450, "left");

        Assert.True(result.Success);
        Assert.Single(inner.ClickCalls);
        Assert.Equal((740, 450, "left", 1), inner.ClickCalls[0]);
        Assert.Contains(VmTitle, inner.FocusCalls);
    }

    [Fact]
    public async Task Click_OutsideBounds_Refused()
    {
        var inner = new RecordingBackend();
        var scoped = MakeScoped(inner);

        var result = await scoped.ClickAsync(10, 10, "left");

        Assert.False(result.Success);
        Assert.Contains("outside", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(inner.ClickCalls);
    }

    [Fact]
    public async Task OpenApp_BlockedWhenScoped()
    {
        var inner = new RecordingBackend();
        var scoped = MakeScoped(inner);

        var result = await scoped.OpenAppAsync("chrome", "https://example.com");

        Assert.False(result.Success);
        Assert.Contains("blocked", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(inner.OpenAppCalls);
    }

    [Fact]
    public async Task Key_AltTab_Blocked()
    {
        var inner = new RecordingBackend();
        var scoped = MakeScoped(inner);

        var result = await scoped.KeyAsync("Alt+Tab");

        Assert.False(result.Success);
        Assert.Contains("refused", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(inner.KeyCalls);
    }

    [Fact]
    public async Task Focus_OtherWindow_Refused()
    {
        var inner = new RecordingBackend();
        var scoped = MakeScoped(inner);

        var result = await scoped.FocusWindowAsync("Notepad");

        Assert.False(result.Success);
        Assert.Contains("refused", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(inner.FocusCalls);
    }

    [Fact]
    public async Task MissingVm_RefusesClick()
    {
        var inner = new RecordingBackend
        {
            ListWindowsResult = new DesktopOpResult(
                true, ListContent(("Notepad", 0, 0, 100, 100)), null)
        };
        var scoped = new ScopedDesktopControlBackend(inner, Scope);

        var result = await scoped.ClickAsync(50, 50, "left");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(inner.ClickCalls);
    }

    [Fact]
    public async Task EmptyScope_PassThrough()
    {
        var inner = new RecordingBackend();
        var scoped = new ScopedDesktopControlBackend(inner, "");

        var result = await scoped.OpenAppAsync("chrome");

        Assert.True(result.Success);
        Assert.Single(inner.OpenAppCalls);
    }

    [Fact]
    public void Guidance_ScopedBlock_MentionsTitle()
    {
        var block = ComputerUseGuidance.ScopedBlock("victoria-sandbox");
        Assert.Contains("victoria-sandbox", block, StringComparison.Ordinal);
        Assert.Contains("DESKTOP SCOPE", block, StringComparison.Ordinal);
        Assert.Contains("BLOCKED", block, StringComparison.Ordinal);
    }

    private sealed class RecordingBackend : IDesktopControlBackend
    {
        public List<(int x, int y, string button, int clicks)> ClickCalls { get; } = new();
        public List<(string app, string? args)> OpenAppCalls { get; } = new();
        public List<string> KeyCalls { get; } = new();
        public List<string> FocusCalls { get; } = new();
        public DesktopOpResult? ListWindowsResult { get; set; }

        public Task<DesktopOpResult> ScreenshotAsync(int monitor, CancellationToken ct = default)
            => Task.FromResult(new DesktopOpResult(true, $"shot {monitor}", null));

        public Task<DesktopOpResult> ClickAsync(
            int x, int y, string button, int clicks = 1, CancellationToken ct = default)
        {
            ClickCalls.Add((x, y, button, clicks));
            return Task.FromResult(new DesktopOpResult(true, $"clicked at ({x},{y})", null));
        }

        public Task<DesktopOpResult> DragAsync(
            int x1, int y1, int x2, int y2, string button, CancellationToken ct = default)
            => Task.FromResult(new DesktopOpResult(true, "dragged", null));

        public Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new DesktopOpResult(true, "typed", null));

        public Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default)
        {
            KeyCalls.Add(key);
            return Task.FromResult(new DesktopOpResult(true, $"key {key}", null));
        }

        public Task<DesktopOpResult> ScrollAsync(
            int x, int y, int deltaY, int deltaX = 0, CancellationToken ct = default)
            => Task.FromResult(new DesktopOpResult(true, "scrolled", null));

        public Task<DesktopOpResult> OpenAppAsync(
            string app, string? args = null, CancellationToken ct = default)
        {
            OpenAppCalls.Add((app, args));
            return Task.FromResult(new DesktopOpResult(true, $"opened {app}", null));
        }

        public Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default)
            => Task.FromResult(ListWindowsResult ?? new DesktopOpResult(true, "open desktop windows:", null));

        public Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default)
        {
            FocusCalls.Add(title);
            return Task.FromResult(new DesktopOpResult(true, $"focused {title}", null));
        }
    }
}
