using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Hard-scopes desktop ops to a VirtualBox Ubuntu guest (title substring e.g.
/// <c>victoria-sandbox</c>). When an <see cref="IVmGuestDesktop"/> is wired,
/// clicks/keys/screenshots use <b>guest framebuffer coordinates</b> via Guest
/// Additions — the host VirtualBox window does not need to be visible.
/// Pass-through when the substring is empty.
/// </summary>
public sealed class ScopedDesktopControlBackend : IDesktopControlBackend
{
    private static readonly Regex BoundsLine = new(
        @"^\[(\d+)\]\s+(.*?)\s+bounds=\((-?\d+),(-?\d+)\s+(\d+)x(\d+)\)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> BlockedHostKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Alt+Tab", "Alt+Escape", "Alt+F4",
        "LWin", "RWin", "Win", "Meta", "Super",
        "Win+D", "Win+Tab", "Win+E", "Win+R", "Win+L",
        "Ctrl+Alt+Delete", "Ctrl+Shift+Esc",
    };

    private readonly IDesktopControlBackend _inner;
    private readonly string _titleContains;
    private readonly IVmGuestAppLauncher? _guestApps;
    private readonly IVmGuestDesktop? _guestDesktop;
    private readonly IDesktopControlBackend? _win32Windows;

    public ScopedDesktopControlBackend(
        IDesktopControlBackend inner,
        string titleContains,
        IVmGuestAppLauncher? guestApps = null,
        IDesktopControlBackend? win32Windows = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _titleContains = (titleContains ?? string.Empty).Trim();
        _guestApps = guestApps;
        _guestDesktop = guestApps as IVmGuestDesktop;
        _win32Windows = win32Windows;
    }

    public bool IsActive => _titleContains.Length > 0;

    public Task<DesktopOpResult> ScreenshotAsync(int monitor, CancellationToken ct = default)
    {
        if (!IsActive)
            return _inner.ScreenshotAsync(monitor, ct);
        if (_guestDesktop is not null)
            return _guestDesktop.ScreenshotAsync(ct);
        return ScreenshotScopedAsync(monitor, ct);
    }

    public async Task<DesktopOpResult> ClickAsync(
        int x, int y, string button, int clicks = 1, CancellationToken ct = default)
    {
        if (!IsActive)
            return await _inner.ClickAsync(x, y, button, clicks, ct).ConfigureAwait(false);

        if (_guestDesktop is not null)
            return await _guestDesktop.ClickAsync(x, y, button, clicks, ct).ConfigureAwait(false);

        var win = await ResolveTargetAsync(ct).ConfigureAwait(false);
        if (win is null)
            return MissingTarget();
        if (!ContainsPoint(win, x, y))
            return Outside(win, x, y, "click");

        await EnsureFocusedAsync(win, ct).ConfigureAwait(false);
        return await _inner.ClickAsync(x, y, button, clicks, ct).ConfigureAwait(false);
    }

    public async Task<DesktopOpResult> DragAsync(
        int x1, int y1, int x2, int y2, string button, CancellationToken ct = default)
    {
        if (!IsActive)
            return await _inner.DragAsync(x1, y1, x2, y2, button, ct).ConfigureAwait(false);

        if (_guestDesktop is not null)
            return await _guestDesktop.DragAsync(x1, y1, x2, y2, button, ct).ConfigureAwait(false);

        var win = await ResolveTargetAsync(ct).ConfigureAwait(false);
        if (win is null)
            return MissingTarget();
        if (!ContainsPoint(win, x1, y1) || !ContainsPoint(win, x2, y2))
            return Outside(win, x1, y1, "drag");

        await EnsureFocusedAsync(win, ct).ConfigureAwait(false);
        return await _inner.DragAsync(x1, y1, x2, y2, button, ct).ConfigureAwait(false);
    }

    public async Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default)
    {
        if (!IsActive)
            return await _inner.TypeAsync(text, ct).ConfigureAwait(false);

        if (_guestDesktop is not null)
            return await _guestDesktop.TypeAsync(text, ct).ConfigureAwait(false);

        var win = await ResolveTargetAsync(ct).ConfigureAwait(false);
        if (win is null)
            return MissingTarget();

        await EnsureFocusedAsync(win, ct).ConfigureAwait(false);
        return await _inner.TypeAsync(text, ct).ConfigureAwait(false);
    }

    public async Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default)
    {
        if (!IsActive)
            return await _inner.KeyAsync(key, ct).ConfigureAwait(false);

        var normalized = (key ?? string.Empty).Trim();

        if (_guestDesktop is not null)
        {
            // Guest keys stay inside Ubuntu — Alt+Tab / Super are valid there.
            if (IsHostOnlyDangerKey(normalized))
            {
                return new DesktopOpResult(
                    false,
                    $"desktop scope '{_titleContains}': key '{normalized}' refused even in guest.",
                    null);
            }

            return await _guestDesktop.KeyAsync(normalized, ct).ConfigureAwait(false);
        }

        if (IsBlockedHostKey(normalized))
        {
            return new DesktopOpResult(
                false,
                $"desktop scope '{_titleContains}': key '{normalized}' leaves the VM window — refused. " +
                "Use keys inside the guest only.",
                null);
        }

        var win = await ResolveTargetAsync(ct).ConfigureAwait(false);
        if (win is null)
            return MissingTarget();

        await EnsureFocusedAsync(win, ct).ConfigureAwait(false);
        return await _inner.KeyAsync(normalized, ct).ConfigureAwait(false);
    }

    public async Task<DesktopOpResult> ScrollAsync(
        int x, int y, int deltaY, int deltaX = 0, CancellationToken ct = default)
    {
        if (!IsActive)
            return await _inner.ScrollAsync(x, y, deltaY, deltaX, ct).ConfigureAwait(false);

        if (_guestDesktop is not null)
            return await _guestDesktop.ScrollAsync(x, y, deltaY, deltaX, ct).ConfigureAwait(false);

        var win = await ResolveTargetAsync(ct).ConfigureAwait(false);
        if (win is null)
            return MissingTarget();
        if (!ContainsPoint(win, x, y))
            return Outside(win, x, y, "scroll");

        await EnsureFocusedAsync(win, ct).ConfigureAwait(false);
        return await _inner.ScrollAsync(x, y, deltaY, deltaX, ct).ConfigureAwait(false);
    }

    public async Task<DesktopOpResult> OpenAppAsync(
        string app, string? args = null, CancellationToken ct = default)
    {
        if (!IsActive)
            return await _inner.OpenAppAsync(app, args, ct).ConfigureAwait(false);

        if (_guestApps is null)
        {
            return new DesktopOpResult(
                false,
                $"desktop scope '{_titleContains}': desktop_open_app on the Windows host is BLOCKED. " +
                "Drive apps inside the VM via Guest Additions (set SOULCORE_VBOX_GUEST_PASS). " +
                "Do not launch Chrome/Notepad/etc. on Kurt's real desktop.",
                null);
        }

        // guestcontrol does not need the VirtualBox window focused or visible.
        return await _guestApps.OpenAppAsync(app, args, ct).ConfigureAwait(false);
    }

    public async Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default)
    {
        if (!IsActive)
            return await _inner.ListWindowsAsync(ct).ConfigureAwait(false);

        if (_guestDesktop is not null)
            return await _guestDesktop.ListWindowsAsync(ct).ConfigureAwait(false);

        var listed = await _inner.ListWindowsAsync(ct).ConfigureAwait(false);
        var matches = MatchScope(listed).ToList();
        if (matches.Count == 0)
            matches = MatchScope(await ListWin32Async(ct).ConfigureAwait(false)).ToList();

        if (matches.Count == 0)
        {
            return new DesktopOpResult(
                true,
                $"desktop scope '{_titleContains}': no matching window (is the VM running / visible?). " +
                "Expected a title containing that substring (e.g. 'victoria-sandbox [Running] - Oracle VirtualBox').",
                Array.Empty<object>());
        }

        var lines = new StringBuilder("open desktop windows (scoped):");
        var data = new List<object>();
        for (var i = 0; i < matches.Count; i++)
        {
            var w = matches[i];
            var cx = w.X + w.Width / 2;
            var cy = w.Y + w.Height / 2;
            data.Add(new
            {
                index = i,
                title = w.Title,
                x = w.X,
                y = w.Y,
                width = w.Width,
                height = w.Height,
                centerX = cx,
                centerY = cy
            });
            lines.Append("\n[").Append(i).Append("] ").Append(w.Title)
                .Append(" bounds=(").Append(w.X).Append(',').Append(w.Y)
                .Append(' ').Append(w.Width).Append('x').Append(w.Height).Append(')')
                .Append(" center=(").Append(cx).Append(',').Append(cy).Append(')');
        }

        return new DesktopOpResult(true, lines.ToString(), data);
    }

    public async Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default)
    {
        if (!IsActive)
            return await _inner.FocusWindowAsync(title, ct).ConfigureAwait(false);

        if (_guestDesktop is not null)
            return await _guestDesktop.FocusWindowAsync(title, ct).ConfigureAwait(false);

        var ask = (title ?? string.Empty).Trim();
        if (ask.Length > 0
            && !ask.Contains(_titleContains, StringComparison.OrdinalIgnoreCase)
            && !_titleContains.Contains(ask, StringComparison.OrdinalIgnoreCase))
        {
            return new DesktopOpResult(
                false,
                $"desktop scope '{_titleContains}': focus refused for '{ask}' — only the scoped VM window is allowed.",
                null);
        }

        var win = await ResolveTargetAsync(ct).ConfigureAwait(false);
        if (win is null)
            return MissingTarget();

        return await _inner.FocusWindowAsync(win.Title, ct).ConfigureAwait(false);
    }

    private async Task<DesktopOpResult> ScreenshotScopedAsync(int monitor, CancellationToken ct)
    {
        var win = await ResolveTargetAsync(ct).ConfigureAwait(false);
        if (win is null)
            return MissingTarget();

        await EnsureFocusedAsync(win, ct).ConfigureAwait(false);
        var shot = await _inner.ScreenshotAsync(monitor, ct).ConfigureAwait(false);
        if (!shot.Success)
            return shot;

        var note =
            $"DESKTOP SCOPE active: only window '{win.Title}' " +
            $"bounds=({win.X},{win.Y} {win.Width}x{win.Height}). " +
            "Clicks/drags/scrolls outside those bounds are refused. " +
            "Prefer Guest Additions (SOULCORE_VBOX_GUEST_PASS) so the VM window can stay minimized.\n"
            + shot.Content;
        return new DesktopOpResult(true, note, shot.Data);
    }

    private async Task<WindowHit?> ResolveTargetAsync(CancellationToken ct)
    {
        var hit = MatchScope(await _inner.ListWindowsAsync(ct).ConfigureAwait(false)).FirstOrDefault();
        if (hit is not null)
            return hit;
        return MatchScope(await ListWin32Async(ct).ConfigureAwait(false)).FirstOrDefault();
    }

    private async Task<DesktopOpResult> ListWin32Async(CancellationToken ct)
    {
        if (_win32Windows is null)
            return new DesktopOpResult(true, "no win32 window list", Array.Empty<object>());
        return await _win32Windows.ListWindowsAsync(ct).ConfigureAwait(false);
    }

    private IEnumerable<WindowHit> MatchScope(DesktopOpResult listed)
    {
        if (!listed.Success)
            return Array.Empty<WindowHit>();
        return ParseWindows(listed.Content)
            .Where(w => w.Title.Contains(_titleContains, StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureFocusedAsync(WindowHit win, CancellationToken ct)
    {
        try
        {
            await _inner.FocusWindowAsync(win.Title, ct).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort; click path may still work via background delivery.
        }

        if (_win32Windows is null)
            return;
        try
        {
            await _win32Windows.FocusWindowAsync(win.Title, ct).ConfigureAwait(false);
        }
        catch
        {
            // Native SetForegroundWindow is best-effort.
        }
    }

    private DesktopOpResult MissingTarget() => new(
        false,
        $"desktop scope '{_titleContains}': target window not found. " +
        "Start the VM (window can stay minimized if Guest Additions + SOULCORE_VBOX_GUEST_PASS are set).",
        null);

    private static DesktopOpResult Outside(WindowHit win, int x, int y, string op) => new(
        false,
        $"desktop scope: {op} at ({x},{y}) is outside '{win.Title}' " +
        $"bounds=({win.X},{win.Y} {win.Width}x{win.Height}) — refused.",
        null);

    private static bool ContainsPoint(WindowHit win, int x, int y) =>
        x >= win.X && y >= win.Y && x < win.X + win.Width && y < win.Y + win.Height;

    private static bool IsBlockedHostKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        if (BlockedHostKeys.Contains(key))
            return true;
        var compact = key.Replace(" ", "", StringComparison.Ordinal);
        return BlockedHostKeys.Contains(compact);
    }

    private static bool IsHostOnlyDangerKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        var compact = key.Replace(" ", "", StringComparison.Ordinal);
        return compact.Equals("Ctrl+Alt+Delete", StringComparison.OrdinalIgnoreCase)
               || compact.Equals("Ctrl+Shift+Esc", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<WindowHit> ParseWindows(string? content)
    {
        var list = new List<WindowHit>();
        if (string.IsNullOrWhiteSpace(content))
            return list;

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            var m = BoundsLine.Match(line);
            if (!m.Success)
                continue;
            list.Add(new WindowHit(
                Title: m.Groups[2].Value.Trim(),
                X: int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                Y: int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture),
                Width: int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture),
                Height: int.Parse(m.Groups[6].Value, CultureInfo.InvariantCulture)));
        }

        return list;
    }

    public sealed record WindowHit(string Title, int X, int Y, int Width, int Height);
}
