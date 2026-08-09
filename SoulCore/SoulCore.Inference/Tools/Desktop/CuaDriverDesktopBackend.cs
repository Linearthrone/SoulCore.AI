using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Desktop backend via local <c>cua-driver</c> — same path LLMOD used for Victoria's
/// large blue agent cursor. Overlay never moves the OS mouse; clicks default to
/// <c>delivery_mode=background</c> (UIA / PostMessage / synthetic pointer).
/// </summary>
public sealed class CuaDriverDesktopBackend : IDesktopControlBackend
{
    public const string BackendName = "cua";

    private readonly CuaDriverCli _cli;
    private readonly IDesktopViewHub? _view;
    private readonly Func<bool> _preferBackground;
    private readonly string _sessionId;
    private readonly object _initGate = new();
    private bool _initialized;
    private int? _lastPid;
    private int? _lastWindowId;

    public CuaDriverDesktopBackend(
        CuaDriverCli cli,
        IDesktopViewHub? view = null,
        Func<bool>? preferBackground = null,
        string? sessionId = null)
    {
        _cli = cli ?? throw new ArgumentNullException(nameof(cli));
        _view = view;
        _preferBackground = preferBackground ?? (() => true);
        _sessionId = string.IsNullOrWhiteSpace(sessionId)
            ? CuaDriverCli.DefaultSessionId
            : sessionId.Trim();
    }

    public CuaDriverDesktopBackend(CuaDriverCli cli, IDesktopViewHub? view, IToolsAccessSettings? access)
        : this(cli, view, access is null ? null : () => access.SoftCursorRestore)
    {
    }

    public async Task<DesktopOpResult> ScreenshotAsync(int monitor, CancellationToken ct = default)
    {
        _ = monitor;
        var ready = await EnsureSessionAsync(ct).ConfigureAwait(false);
        if (!ready.Success)
            return ready;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoulCore", "scratch", "desktop");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"cua-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.png");

        var call = await _cli.CallAsync("get_desktop_state", new
        {
            session = _sessionId,
            screenshot_out_file = path,
        }, ct).ConfigureAwait(false);

        if (!call.Success)
            return new DesktopOpResult(false, $"cua screenshot failed: {call.Error}", null);

        if (!File.Exists(path))
            return new DesktopOpResult(false, "cua screenshot: output file missing", null);

        var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        var width = 0;
        var height = 0;
        if (call.TryParseJson(out var el))
        {
            if (el.TryGetProperty("screenshot_width", out var w)) width = w.GetInt32();
            if (el.TryGetProperty("screenshot_height", out var h)) height = h.GetInt32();
            if (el.TryGetProperty("screen_width", out var sw) && width <= 0) width = sw.GetInt32();
            if (el.TryGetProperty("screen_height", out var sh) && height <= 0) height = sh.GetInt32();
        }

        _view?.RecordScreenshot(bytes, "png", width, height, path);
        _view?.RecordAction($"screenshot via cua-driver ({width}x{height})");
        return new DesktopOpResult(
            true,
            $"captured desktop screenshot via cua-driver saved to {path} ({bytes.Length} bytes PNG)",
            new { path, bytes, format = "png", width, height, backend = BackendName });
    }

    public async Task<DesktopOpResult> ClickAsync(
        int x, int y, string button, int clicks = 1, CancellationToken ct = default)
    {
        var btn = (button ?? "left").Trim().ToLowerInvariant();
        if (btn is not ("left" or "right" or "middle"))
            return new DesktopOpResult(false, $"unsupported button '{button}' (use left|right|middle)", null);
        if (clicks is not (1 or 2))
            return new DesktopOpResult(false, $"unsupported clicks={clicks} (use 1 or 2)", null);

        var ready = await EnsureSessionAsync(ct).ConfigureAwait(false);
        if (!ready.Success)
            return ready;

        await MoveAgentCursorAsync(x, y, ct).ConfigureAwait(false);
        RememberTargetUnderPoint(x, y);

        for (var i = 0; i < clicks; i++)
        {
            var once = await ClickOnceAsync(x, y, btn, ct).ConfigureAwait(false);
            if (!once.Success)
                return once;
        }

        var clickLabel = clicks == 2 ? $"double-clicked {btn}" : $"clicked {btn}";
        var note = $"{clickLabel} at ({x},{y}) via cua agent cursor";
        _view?.RecordAction(note, x, y);
        return new DesktopOpResult(true, note, new { x, y, button = btn, clicks, backend = BackendName });
    }

    private async Task<DesktopOpResult> ClickOnceAsync(int x, int y, string btn, CancellationToken ct)
    {
        var delivery = _preferBackground() ? "background" : "foreground";
        var call = await _cli.CallAsync("click", new Dictionary<string, object?>
        {
            ["x"] = x,
            ["y"] = y,
            ["button"] = btn,
            ["delivery_mode"] = delivery,
            ["session"] = _sessionId,
        }, ct).ConfigureAwait(false);

        if (!call.Success && delivery == "background"
            && (call.Error?.Contains("background_unavailable", StringComparison.OrdinalIgnoreCase) == true
                || call.Error?.Contains("foreground", StringComparison.OrdinalIgnoreCase) == true))
        {
            call = await _cli.CallAsync("click", new Dictionary<string, object?>
            {
                ["x"] = x,
                ["y"] = y,
                ["button"] = btn,
                ["delivery_mode"] = "foreground",
                ["session"] = _sessionId,
            }, ct).ConfigureAwait(false);
            delivery = "foreground";
        }

        if (!call.Success)
            return new DesktopOpResult(false, $"cua click failed: {call.Error}", null);

        return new DesktopOpResult(true, $"clicked {btn}", new { delivery });
    }

    public async Task<DesktopOpResult> DragAsync(
        int x1, int y1, int x2, int y2, string button, CancellationToken ct = default)
    {
        var btn = (button ?? "left").Trim().ToLowerInvariant();
        if (btn is not ("left" or "right" or "middle"))
            return new DesktopOpResult(false, $"unsupported button '{button}' (use left|right|middle)", null);

        var ready = await EnsureSessionAsync(ct).ConfigureAwait(false);
        if (!ready.Success)
            return ready;

        await MoveAgentCursorAsync(x1, y1, ct).ConfigureAwait(false);
        RememberTargetUnderPoint(x1, y1);

        if (_lastPid is not int pid)
            return new DesktopOpResult(false,
                "cua drag requires a resolvable target pid under (x1,y1) — focus the app window first", null);

        var delivery = _preferBackground() ? "background" : "foreground";
        var args = new Dictionary<string, object?>
        {
            ["pid"] = pid,
            ["from_x"] = x1,
            ["from_y"] = y1,
            ["to_x"] = x2,
            ["to_y"] = y2,
            ["button"] = btn,
            ["delivery_mode"] = delivery,
            ["session"] = _sessionId,
            ["duration_ms"] = 500,
            ["steps"] = 20,
        };
        if (_lastWindowId is int wid)
            args["window_id"] = wid;

        var call = await _cli.CallAsync("drag", args, ct).ConfigureAwait(false);
        if (!call.Success && delivery == "background"
            && (call.Error?.Contains("background_unavailable", StringComparison.OrdinalIgnoreCase) == true
                || call.Error?.Contains("foreground", StringComparison.OrdinalIgnoreCase) == true))
        {
            args["delivery_mode"] = "foreground";
            call = await _cli.CallAsync("drag", args, ct).ConfigureAwait(false);
            delivery = "foreground";
        }

        if (!call.Success)
            return new DesktopOpResult(false, $"cua drag failed: {call.Error}", null);

        await MoveAgentCursorAsync(x2, y2, ct).ConfigureAwait(false);

        var note = delivery == "background"
            ? $"dragged {btn} from ({x1},{y1}) to ({x2},{y2}) via cua agent cursor [OS mouse untouched]"
            : $"dragged {btn} from ({x1},{y1}) to ({x2},{y2}) via cua foreground fallback";
        _view?.RecordAction(note, x2, y2);
        return new DesktopOpResult(true, note,
            new { x1, y1, x2, y2, button = btn, delivery, pid, backend = BackendName });
    }

    public async Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text))
            return new DesktopOpResult(false, "text must be non-empty", null);

        var ready = await EnsureSessionAsync(ct).ConfigureAwait(false);
        if (!ready.Success)
            return ready;

        if (_lastPid is not int pid)
            return new DesktopOpResult(false,
                "cua type requires a prior click target (no pid yet) — click first, then type", null);

        var args = new Dictionary<string, object?>
        {
            ["pid"] = pid,
            ["text"] = text,
            ["delivery_mode"] = _preferBackground() ? "background" : "foreground",
            ["session"] = _sessionId,
        };
        if (_lastWindowId is int wid)
            args["window_id"] = wid;

        var call = await _cli.CallAsync("type_text", args, ct).ConfigureAwait(false);
        if (!call.Success)
            return new DesktopOpResult(false, $"cua type failed: {call.Error}", null);

        _view?.RecordAction($"typed {text.Length} character(s) via cua");
        return new DesktopOpResult(true, $"typed {text.Length} character(s) via cua-driver",
            new { length = text.Length, pid, backend = BackendName });
    }

    public async Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new DesktopOpResult(false, "key must be non-empty", null);

        var ready = await EnsureSessionAsync(ct).ConfigureAwait(false);
        if (!ready.Success)
            return ready;

        if (_lastPid is not int pid)
            return new DesktopOpResult(false,
                "cua key requires a prior click target (no pid yet) — click first, then key", null);

        var args = new Dictionary<string, object?>
        {
            ["pid"] = pid,
            ["key"] = key.Trim(),
            ["delivery_mode"] = _preferBackground() ? "background" : "foreground",
            ["session"] = _sessionId,
        };
        if (_lastWindowId is int wid)
            args["window_id"] = wid;

        var call = await _cli.CallAsync("press_key", args, ct).ConfigureAwait(false);
        if (!call.Success)
            return new DesktopOpResult(false, $"cua key failed: {call.Error}", null);

        _view?.RecordAction($"pressed key '{key}' via cua");
        return new DesktopOpResult(true, $"pressed key '{key}' via cua-driver",
            new { key, pid, backend = BackendName });
    }

    public async Task<DesktopOpResult> ScrollAsync(
        int x, int y, int deltaY, int deltaX = 0, CancellationToken ct = default)
    {
        if (deltaY == 0 && deltaX == 0)
            return new DesktopOpResult(false, "deltaY or deltaX must be non-zero", null);

        var ready = await EnsureSessionAsync(ct).ConfigureAwait(false);
        if (!ready.Success)
            return ready;

        await MoveAgentCursorAsync(x, y, ct).ConfigureAwait(false);
        RememberTargetUnderPoint(x, y);

        var delivery = _preferBackground() ? "background" : "foreground";
        var args = new Dictionary<string, object?>
        {
            ["x"] = x,
            ["y"] = y,
            ["delta_y"] = deltaY,
            ["delta_x"] = deltaX,
            ["delivery_mode"] = delivery,
            ["session"] = _sessionId,
        };
        if (_lastPid is int pid)
            args["pid"] = pid;
        if (_lastWindowId is int wid)
            args["window_id"] = wid;

        var call = await _cli.CallAsync("scroll", args, ct).ConfigureAwait(false);
        if (!call.Success)
        {
            // Fallback verb name used by some cua-driver builds.
            call = await _cli.CallAsync("mouse_scroll", args, ct).ConfigureAwait(false);
        }

        if (!call.Success)
            return new DesktopOpResult(false, $"cua scroll failed: {call.Error}", null);

        var note = $"scrolled at ({x},{y}) deltaY={deltaY} deltaX={deltaX} via cua-driver";
        _view?.RecordAction(note, x, y);
        return new DesktopOpResult(true, note, new { x, y, deltaY, deltaX, backend = BackendName });
    }

    public Task<DesktopOpResult> OpenAppAsync(
        string app, string? args = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // cua-driver does not own process create — allowlisted launch; prefer no-activate.
        var result = DesktopAppLauncher.Launch(app, args, backgroundNoActivate: _preferBackground());
        if (result.Success)
            _view?.RecordAction(result.Content);
        return Task.FromResult(result);
    }

    public async Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default)
    {
        var ready = await EnsureSessionAsync(ct).ConfigureAwait(false);
        if (!ready.Success)
            return ready;

        var call = await _cli.CallAsync("list_windows", new { session = _sessionId }, ct)
            .ConfigureAwait(false);
        if (!call.Success)
            return new DesktopOpResult(false, $"cua list_windows failed: {call.Error}", null);

        var windows = new List<object>();
        var lines = new StringBuilder("open desktop windows:");
        if (call.TryParseJson(out var root))
        {
            JsonElement arr = default;
            var found = false;
            if (root.ValueKind == JsonValueKind.Array)
            {
                arr = root;
                found = true;
            }
            else if (root.TryGetProperty("_legacy_windows", out var legacy) && legacy.ValueKind == JsonValueKind.Array)
            {
                arr = legacy;
                found = true;
            }
            else if (root.TryGetProperty("windows", out var wins) && wins.ValueKind == JsonValueKind.Array)
            {
                arr = wins;
                found = true;
            }

            if (found)
            {
                var i = 0;
                foreach (var w in arr.EnumerateArray())
                {
                    var title = w.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(title)) continue;
                    var hwnd = w.TryGetProperty("window_id", out var id) ? id.GetInt64() : 0;
                    var wx = ReadInt(w, "x");
                    var wy = ReadInt(w, "y");
                    var ww = ReadInt(w, "width");
                    var wh = ReadInt(w, "height");
                    if (w.TryGetProperty("bounds", out var bounds) && bounds.ValueKind == JsonValueKind.Object)
                    {
                        wx = ReadInt(bounds, "x", wx);
                        wy = ReadInt(bounds, "y", wy);
                        ww = ReadInt(bounds, "width", ww);
                        wh = ReadInt(bounds, "height", wh);
                    }

                    var cx = ww > 0 ? wx + ww / 2 : wx;
                    var cy = wh > 0 ? wy + wh / 2 : wy;
                    windows.Add(new
                    {
                        index = i,
                        title,
                        hwnd,
                        x = wx,
                        y = wy,
                        width = ww,
                        height = wh,
                        centerX = cx,
                        centerY = cy
                    });
                    lines.Append("\n[").Append(i).Append("] ").Append(title)
                        .Append(" bounds=(").Append(wx).Append(',').Append(wy)
                        .Append(' ').Append(ww).Append('x').Append(wh).Append(')')
                        .Append(" center=(").Append(cx).Append(',').Append(cy).Append(')');
                    i++;
                }
            }
        }

        if (windows.Count == 0)
            return new DesktopOpResult(true, call.Stdout.Length > 0 ? call.Stdout : "no visible titled windows", windows);

        return new DesktopOpResult(true, lines.ToString(), windows);
    }

    private static int ReadInt(JsonElement el, string name, int fallback = 0)
    {
        if (!el.TryGetProperty(name, out var p)) return fallback;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n)) return n;
        return fallback;
    }

    public async Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return new DesktopOpResult(false, "title must be non-empty", null);

        var ready = await EnsureSessionAsync(ct).ConfigureAwait(false);
        if (!ready.Success)
            return ready;

        // Resolve pid from list_windows, then bring_to_front (explicit — steals focus).
        var list = await _cli.CallAsync("list_windows", new { session = _sessionId }, ct)
            .ConfigureAwait(false);
        if (!list.Success || !list.TryParseJson(out var root))
            return new DesktopOpResult(false, $"cua focus: list_windows failed: {list.Error}", null);

        JsonElement arr = default;
        var foundArr = false;
        if (root.TryGetProperty("_legacy_windows", out var legacy) && legacy.ValueKind == JsonValueKind.Array)
        {
            arr = legacy;
            foundArr = true;
        }
        else if (root.TryGetProperty("windows", out var wins) && wins.ValueKind == JsonValueKind.Array)
        {
            arr = wins;
            foundArr = true;
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            arr = root;
            foundArr = true;
        }

        if (!foundArr)
            return new DesktopOpResult(false, $"no window matching title '{title}'", null);

        foreach (var w in arr.EnumerateArray())
        {
            var t = w.TryGetProperty("title", out var tp) ? tp.GetString() ?? "" : "";
            if (!t.Contains(title, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!w.TryGetProperty("pid", out var pidEl))
                continue;
            var pid = pidEl.GetInt32();
            var wid = w.TryGetProperty("window_id", out var idEl) ? idEl.GetInt32() : (int?)null;
            var args = new Dictionary<string, object?> { ["pid"] = pid, ["session"] = _sessionId };
            if (wid is int windowId)
                args["window_id"] = windowId;

            var call = await _cli.CallAsync("bring_to_front", args, ct).ConfigureAwait(false);
            if (!call.Success)
                return new DesktopOpResult(false, $"cua bring_to_front failed: {call.Error}", null);

            _lastPid = pid;
            _lastWindowId = wid;
            _view?.RecordAction($"focused window '{t}' via cua");
            return new DesktopOpResult(true, $"focused window '{t}'", new { title = t, pid, backend = BackendName });
        }

        return new DesktopOpResult(false, $"no window matching title '{title}'", null);
    }

    private async Task<DesktopOpResult> EnsureSessionAsync(CancellationToken ct)
    {
        lock (_initGate)
        {
            if (_initialized)
                return new DesktopOpResult(true, "ready", null);
        }

        var session = await _cli.CallAsync("start_session", new { session = _sessionId }, ct)
            .ConfigureAwait(false);
        if (!session.Success)
            return new DesktopOpResult(false, $"cua start_session failed: {session.Error}", null);

        _ = await _cli.CallAsync("set_config", new { capture_scope = "desktop" }, ct)
            .ConfigureAwait(false);
        _ = await _cli.CallAsync("set_agent_cursor_enabled", new { enabled = true }, ct)
            .ConfigureAwait(false);
        _ = await _cli.CallAsync("set_agent_cursor_style", new
        {
            gradient_colors = new[] { "#4FC3F7", "#1565C0" },
            bloom_color = "#29B6F6",
            session = _sessionId,
        }, ct).ConfigureAwait(false);

        lock (_initGate)
        {
            _initialized = true;
        }

        return new DesktopOpResult(true, "cua session ready", null);
    }

    private async Task MoveAgentCursorAsync(int x, int y, CancellationToken ct)
    {
        var call = await _cli.CallAsync("move_cursor", new
        {
            x,
            y,
            session = _sessionId,
        }, ct).ConfigureAwait(false);
        if (call.Success)
            _view?.RecordAction($"agent cursor → ({x},{y})", x, y);
    }

    private void RememberTargetUnderPoint(int x, int y)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var hwnd = WindowFromPoint(new POINT { X = x, Y = y });
            if (hwnd == IntPtr.Zero)
                return;
            _ = GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != 0)
                _lastPid = unchecked((int)pid);
            _lastWindowId = unchecked((int)hwnd.ToInt64());
        }
        catch
        {
            // best-effort for subsequent type/key
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
