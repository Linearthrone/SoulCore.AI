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
    public async Task OpenApp_BlockedWhenScoped_WithoutGuestLauncher()
    {
        var inner = new RecordingBackend();
        var scoped = MakeScoped(inner);

        var result = await scoped.OpenAppAsync("chrome", "https://example.com");

        Assert.False(result.Success);
        Assert.Contains("BLOCKED", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(inner.OpenAppCalls);
    }

    [Fact]
    public async Task OpenApp_UsesGuestLauncher_NotHost()
    {
        var inner = new RecordingBackend();
        var guest = new RecordingGuestLauncher();
        inner.ListWindowsResult = new DesktopOpResult(
            true,
            ListContent((VmTitle, 100, 50, 1280, 800)),
            null);
        var scoped = new ScopedDesktopControlBackend(inner, Scope, guest);

        var result = await scoped.OpenAppAsync("chrome", "https://example.com");

        Assert.True(result.Success);
        Assert.Empty(inner.OpenAppCalls);
        Assert.Single(guest.Calls);
        Assert.Equal(("chrome", "https://example.com"), guest.Calls[0]);
        Assert.Empty(inner.FocusCalls);
    }

    [Fact]
    public async Task OpenApp_GuestLauncher_DoesNotRequireCuaWindow()
    {
        var inner = new RecordingBackend
        {
            ListWindowsResult = new DesktopOpResult(
                true, ListContent(("Notepad", 0, 0, 100, 100)), null)
        };
        var guest = new RecordingGuestLauncher();
        var scoped = new ScopedDesktopControlBackend(inner, Scope, guest);

        var result = await scoped.OpenAppAsync("chrome");

        Assert.True(result.Success);
        Assert.Single(guest.Calls);
        Assert.Empty(inner.OpenAppCalls);
        Assert.Empty(inner.FocusCalls);
    }

    [Fact]
    public async Task Click_UsesWin32FallbackWhenCuaOmitsVm()
    {
        var cua = new RecordingBackend
        {
            ListWindowsResult = new DesktopOpResult(
                true, ListContent(("Notepad", 0, 0, 100, 100)), null)
        };
        var win32 = new RecordingBackend
        {
            ListWindowsResult = new DesktopOpResult(
                true, ListContent((VmTitle, 100, 50, 1280, 800)), null)
        };
        var scoped = new ScopedDesktopControlBackend(cua, Scope, guestApps: null, win32Windows: win32);

        var result = await scoped.ClickAsync(740, 450, "left");

        Assert.True(result.Success);
        Assert.Single(cua.ClickCalls);
        Assert.Contains(VmTitle, win32.FocusCalls);
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

    [Theory]
    [InlineData("chrome", "firefox")]
    [InlineData("edge", "firefox")]
    [InlineData("notepad", "text editor")]
    [InlineData("explorer", "files")]
    [InlineData("powershell", "terminal")]
    public void GuestSearch_MapsWindowsAliasesToUbuntu(string alias, string search)
    {
        Assert.Equal(search, VirtualBoxGuestAppLauncher.MapGuestSearch(alias));
    }

    [Fact]
    public void GuestExe_MapsFirefoxWithUrl()
    {
        Assert.True(VirtualBoxGuestAppLauncher.TryMapGuestExe("chrome", "https://example.com", out var exe, out var args));
        Assert.Equal("/usr/bin/firefox", exe);
        Assert.Equal(new[] { "https://example.com" }, args);
    }

    [Fact]
    public void GuestHeartbeat_ParsesLoggedInUsersNotList()
    {
        const string sample =
            "/VirtualBox/GuestInfo/OS/LoggedInUsersList = 'victoria' @ 2026-08-17T11:54:15.340Z TRANSIENT\n" +
            "/VirtualBox/GuestInfo/OS/LoggedInUsers     = '1'                            @ 2026-08-17T11:54:15.337Z TRANSIENT, TRANSRESET\n";
        Assert.True(VirtualBoxGuestAppLauncher.TryParseLoggedInHeartbeat(sample, out var ts));
        Assert.Equal(2026, ts.UtcDateTime.Year);
        Assert.Equal(11, ts.UtcDateTime.Hour);
        Assert.Equal(54, ts.UtcDateTime.Minute);
    }

    [Fact]
    public void XdotoolKey_MapsCtrlL()
    {
        Assert.True(VirtualBoxGuestAppLauncher.TryMapXdotoolKey("Ctrl+L", out var xd, out _));
        Assert.Equal("ctrl+l", xd);
        Assert.True(VirtualBoxGuestAppLauncher.TryMapXdotoolKey("Enter", out xd, out _));
        Assert.Equal("Return", xd);
    }

    [Fact]
    public void Wmctrl_ParsesBoundsLine()
    {
        Assert.True(VirtualBoxGuestAppLauncher.TryParseWmctrlLine(
            "0x04600007  0 10   52   1260 720  victoria-sandbox Firefox",
            out var x, out var y, out var w, out var h, out var title));
        Assert.Equal(10, x);
        Assert.Equal(52, y);
        Assert.Equal(1260, w);
        Assert.Equal(720, h);
        Assert.Equal("Firefox", title);
    }

    [Fact]
    public void GuestControlArgs_UsePasswordFileNotInlinePassword()
    {
        var argv = VirtualBoxGuestAppLauncher.BuildGuestControl(
            "victoria-sandbox",
            "start",
            "victoria",
            @"C:\temp\pass.txt",
            "/usr/bin/firefox",
            new[] { "DISPLAY=:0" },
            new[] { "https://example.com" },
            waitOutput: false);
        Assert.Contains("--passwordfile", argv);
        Assert.DoesNotContain(argv, a => a.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("--profile", argv);
        Assert.Contains("/usr/bin/firefox", argv);
    }

    [Fact]
    public async Task Click_GuestDesktop_DoesNotNeedHostWindow()
    {
        var inner = new RecordingBackend
        {
            ListWindowsResult = new DesktopOpResult(
                true, ListContent(("Notepad", 0, 0, 100, 100)), null)
        };
        var guest = new RecordingGuestDesktop();
        var scoped = new ScopedDesktopControlBackend(inner, Scope, guest);

        var result = await scoped.ClickAsync(40, 120, "left");

        Assert.True(result.Success);
        Assert.Single(guest.ClickCalls);
        Assert.Equal((40, 120, "left", 1), guest.ClickCalls[0]);
        Assert.Empty(inner.ClickCalls);
        Assert.Empty(inner.FocusCalls);
    }

    [Fact]
    public async Task OpenApp_GuestDesktop_DoesNotFocusHost()
    {
        var inner = new RecordingBackend();
        var guest = new RecordingGuestDesktop();
        var scoped = new ScopedDesktopControlBackend(inner, Scope, guest);

        var result = await scoped.OpenAppAsync("chrome");

        Assert.True(result.Success);
        Assert.Single(guest.OpenCalls);
        Assert.Empty(inner.FocusCalls);
        Assert.Empty(inner.OpenAppCalls);
    }

    [Fact]
    public void Guidance_ScopedBlock_MentionsTitle()
    {
        var block = ComputerUseGuidance.ScopedBlock("victoria-sandbox");
        Assert.Contains("victoria-sandbox", block, StringComparison.Ordinal);
        Assert.Contains("DESKTOP SCOPE", block, StringComparison.Ordinal);
        Assert.Contains("BLOCKED", block, StringComparison.Ordinal);
        Assert.Contains("guest framebuffer", block, StringComparison.OrdinalIgnoreCase);
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

    private sealed class RecordingGuestLauncher : IVmGuestAppLauncher
    {
        public List<(string app, string? args)> Calls { get; } = new();

        public Task<DesktopOpResult> OpenAppAsync(
            string app, string? args = null, CancellationToken ct = default)
        {
            Calls.Add((app, args));
            return Task.FromResult(new DesktopOpResult(true, $"guest opened {app}", null));
        }
    }

    private sealed class RecordingGuestDesktop : IVmGuestDesktop
    {
        public List<(string app, string? args)> OpenCalls { get; } = new();
        public List<(int x, int y, string button, int clicks)> ClickCalls { get; } = new();

        public Task<DesktopOpResult> OpenAppAsync(string app, string? args = null, CancellationToken ct = default)
        {
            OpenCalls.Add((app, args));
            return Task.FromResult(new DesktopOpResult(
                true,
                $"Opened firefox in the {VirtualBoxGuestAppLauncher.GuestOpenedMarker} via guestcontrol",
                null));
        }

        public Task<DesktopOpResult> ScreenshotAsync(CancellationToken ct = default)
            => Task.FromResult(new DesktopOpResult(true, "guest shot", null));

        public Task<DesktopOpResult> ClickAsync(
            int x, int y, string button, int clicks = 1, CancellationToken ct = default)
        {
            ClickCalls.Add((x, y, button, clicks));
            return Task.FromResult(new DesktopOpResult(true, $"guest click ({x},{y})", null));
        }

        public Task<DesktopOpResult> DragAsync(
            int x1, int y1, int x2, int y2, string button, CancellationToken ct = default)
            => Task.FromResult(new DesktopOpResult(true, "guest drag", null));

        public Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new DesktopOpResult(true, "guest type", null));

        public Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default)
            => Task.FromResult(new DesktopOpResult(true, "guest key", null));

        public Task<DesktopOpResult> ScrollAsync(
            int x, int y, int deltaY, int deltaX = 0, CancellationToken ct = default)
            => Task.FromResult(new DesktopOpResult(true, "guest scroll", null));

        public Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default)
            => Task.FromResult(new DesktopOpResult(true, "open desktop windows (Ubuntu guest framebuffer):\n[0] Firefox bounds=(0,0 800x600) center=(400,300)", null));

        public Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default)
            => Task.FromResult(new DesktopOpResult(true, $"guest focus {title}", null));
    }
}
