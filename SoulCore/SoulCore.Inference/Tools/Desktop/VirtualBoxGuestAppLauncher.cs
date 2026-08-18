using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Drives an Ubuntu VirtualBox guest via Guest Additions <c>guestcontrol</c>.
/// Coordinates are the guest framebuffer (origin 0,0) — the host VirtualBox
/// window can stay minimized. Never <c>Process.Start</c> on the Windows host.
/// </summary>
public sealed partial class VirtualBoxGuestAppLauncher : IVmGuestDesktop, IVmGuestBrowser
{
    public const string DefaultVBoxManage =
        @"C:\Program Files\Oracle\VirtualBox\VBoxManage.exe";

    public const string GuestOpenedMarker = "Ubuntu VM";

    public static readonly TimeSpan GuestHeartbeatStaleAfter = TimeSpan.FromMinutes(2);

    private static readonly Regex HeartbeatAt = new(
        @"/VirtualBox/GuestInfo/OS/LoggedInUsers(?!List)\s+=\s+'[^']*'\s+@\s+(\S+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WmctrlLine = new(
        @"^0x[0-9a-fA-F]+\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(\d+)\s+(\d+)\s+\S+\s+(.*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly string _vmName;
    private readonly string _vboxManage;
    private readonly Func<string, IReadOnlyList<string>, CancellationToken, Task<(int Exit, string Stdout, string Stderr)>> _run;
    private readonly Func<string?> _password;
    private readonly Func<string> _username;
    private string? _cachedXauthority;
    private string? _cachedUid;

    public VirtualBoxGuestAppLauncher(string vmName, string? vboxManage = null)
        : this(vmName, vboxManage, RunVBoxManageAsync, ResolveGuestPassword, ResolveGuestUser)
    {
    }

    public VirtualBoxGuestAppLauncher(
        string vmName,
        string? vboxManage,
        Func<string, IReadOnlyList<string>, CancellationToken, Task<(int Exit, string Stdout, string Stderr)>> run,
        Func<string?>? password = null,
        Func<string>? username = null)
    {
        _vmName = string.IsNullOrWhiteSpace(vmName) ? "victoria-sandbox" : vmName.Trim();
        _vboxManage = string.IsNullOrWhiteSpace(vboxManage) ? DefaultVBoxManage : vboxManage.Trim();
        _run = run ?? throw new ArgumentNullException(nameof(run));
        _password = password ?? ResolveGuestPassword;
        _username = username ?? ResolveGuestUser;
    }

    public static string ResolveGuestUser()
    {
        var u = Environment.GetEnvironmentVariable("SOULCORE_VBOX_GUEST_USER");
        return string.IsNullOrWhiteSpace(u) ? "victoria" : u.Trim();
    }

    public static string? ResolveGuestPassword()
    {
        foreach (var key in new[] { SecretNames.VboxGuestPass, "VBOX_GUEST_PASS" })
        {
            var v = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        return null;
    }

    public async Task<DesktopOpResult> OpenAppAsync(
        string app, string? args = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(app))
            return new DesktopOpResult(false, "app must be non-empty", null);

        if (!DesktopAppLauncher.TryResolve(app, args, out var resolved, out var error))
            return new DesktopOpResult(false, error, null);

        if (!TryMapGuestExe(resolved.Alias, LooksLikeUrl(resolved.Arguments) ? resolved.Arguments.Trim().Trim('"') : null,
                out var exe, out var exeArgs))
        {
            return new DesktopOpResult(
                false,
                $"no Ubuntu mapping for '{resolved.Alias}'",
                null);
        }

        var started = await GuestStartAsync(exe, exeArgs, ct).ConfigureAwait(false);
        if (!started.Success)
            return started;

        var search = MapGuestSearch(resolved.Alias);
        var url = exeArgs.Length > 0 ? exeArgs[0] : null;
        var note = url is null
            ? $"Opened {search} in the {GuestOpenedMarker} via guestcontrol (host VirtualBox window can stay minimized)."
            : $"Opened {search} in the {GuestOpenedMarker} to {url} via guestcontrol (host VirtualBox window can stay minimized).";
        return new DesktopOpResult(
            true,
            note,
            new { app = resolved.Alias, vm = _vmName, search, exe, url, hostLaunch = false, method = "guestcontrol" });
    }

    public async Task<DesktopOpResult> ProbeWhoamiAsync(CancellationToken ct = default)
    {
        // Args after "--" must NOT repeat the exe path. VBoxManage already uses
        // --exe as argv[0]; repeating it makes commands like `id` treat the path
        // as a username ("no such user") after a successful guest logon.
        return await GuestRunAsync("/usr/bin/whoami", Array.Empty<string>(), wait: true, ct)
            .ConfigureAwait(false);
    }

    public async Task<DesktopOpResult> ScreenshotAsync(CancellationToken ct = default)
    {
        // Without a guest password, skip guestcontrol and go straight to
        // VBoxManage screenshotpng so Login/UI ForceTool loops still see a PNG.
        var auth = RequirePassword();
        if (auth.Error is not null)
        {
            var pngOnly = await HostScreenshotPngAsync(ct).ConfigureAwait(false);
            if (pngOnly.Success)
            {
                return new DesktopOpResult(
                    true,
                    pngOnly.Content + " (guestcontrol skipped: SOULCORE_VBOX_GUEST_PASS not set)",
                    pngOnly.Data);
            }

            return auth.Error;
        }

        var guestFile = "/tmp/hv-desktop.png";
        var shot = await GuestRunAsync(
                "/usr/bin/gnome-screenshot",
                new[] { "-f", guestFile },
                wait: true,
                ct)
            .ConfigureAwait(false);
        if (!shot.Success)
        {
            shot = await GuestRunAsync(
                    "/usr/bin/import",
                    new[] { "-window", "root", guestFile },
                    wait: true,
                    ct)
                .ConfigureAwait(false);
        }

        if (shot.Success)
        {
            var copied = await GuestCopyFromAsync(guestFile, ct).ConfigureAwait(false);
            if (copied.Success)
                return copied;
            shot = copied;
        }

        var fallback = await HostScreenshotPngAsync(ct).ConfigureAwait(false);
        if (fallback.Success)
        {
            return new DesktopOpResult(
                true,
                fallback.Content + " (guestcontrol screenshot failed: " + shot.Content + ")",
                fallback.Data);
        }

        return shot;
    }

    public async Task<DesktopOpResult> ClickAsync(
        int x, int y, string button, int clicks = 1, CancellationToken ct = default)
    {
        if (!TryMapMouseButton(button, out var btn, out var err))
            return new DesktopOpResult(false, err, null);
        if (clicks is not (1 or 2))
            return new DesktopOpResult(false, $"unsupported clicks={clicks} (use 1 or 2)", null);
        if (x < 0 || y < 0)
            return new DesktopOpResult(false, $"guest click ({x},{y}) is off the Ubuntu screen (origin 0,0).", null);

        var xdArgs = new List<string> { "mousemove", "--sync", x.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture), "click" };
        if (clicks == 2)
            xdArgs.AddRange(new[] { "--repeat", "2", "--delay", "80" });
        xdArgs.Add(btn.ToString(CultureInfo.InvariantCulture));

        var result = await XdotoolAsync(xdArgs, ct).ConfigureAwait(false);
        if (!result.Success)
            return result;
        var label = clicks == 2 ? "double-clicked" : "clicked";
        return new DesktopOpResult(
            true,
            $"{label} {button} at guest ({x},{y}) in the {GuestOpenedMarker} (not Windows).",
            new { x, y, button, clicks, coords = "guest-framebuffer" });
    }

    public async Task<DesktopOpResult> DragAsync(
        int x1, int y1, int x2, int y2, string button, CancellationToken ct = default)
    {
        if (!TryMapMouseButton(button, out var btn, out var err))
            return new DesktopOpResult(false, err, null);
        var result = await XdotoolAsync(
                new[]
                {
                    "mousemove", "--sync",
                    x1.ToString(CultureInfo.InvariantCulture), y1.ToString(CultureInfo.InvariantCulture),
                    "mousedown", btn.ToString(CultureInfo.InvariantCulture),
                    "mousemove", "--sync",
                    x2.ToString(CultureInfo.InvariantCulture), y2.ToString(CultureInfo.InvariantCulture),
                    "mouseup", btn.ToString(CultureInfo.InvariantCulture)
                },
                ct)
            .ConfigureAwait(false);
        if (!result.Success)
            return result;
        return new DesktopOpResult(
            true,
            $"dragged {button} guest ({x1},{y1})→({x2},{y2}) in the {GuestOpenedMarker}.",
            new { x1, y1, x2, y2, button, coords = "guest-framebuffer" });
    }

    public async Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text))
            return new DesktopOpResult(true, "typed nothing", null);
        var result = await XdotoolAsync(new[] { "type", "--clearmodifiers", "--", text }, ct)
            .ConfigureAwait(false);
        if (!result.Success)
            return result;
        return new DesktopOpResult(true, $"typed {text.Length} chars in the {GuestOpenedMarker}.", null);
    }

    public async Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default)
    {
        if (!TryMapXdotoolKey(key, out var xd, out var err))
            return new DesktopOpResult(false, err, null);
        var result = await XdotoolAsync(new[] { "key", "--clearmodifiers", xd }, ct)
            .ConfigureAwait(false);
        if (!result.Success)
            return result;
        return new DesktopOpResult(true, $"key '{key}' in the {GuestOpenedMarker} ({xd}).", null);
    }

    public async Task<DesktopOpResult> ScrollAsync(
        int x, int y, int deltaY, int deltaX = 0, CancellationToken ct = default)
    {
        if (x < 0 || y < 0)
            return new DesktopOpResult(false, $"guest scroll ({x},{y}) is off the Ubuntu screen.", null);

        var stepsY = Math.Clamp(Math.Abs(deltaY) <= 5 ? Math.Abs(deltaY) : Math.Abs(deltaY) / 120, 1, 12);
        var wheelY = deltaY >= 0 ? "4" : "5";
        var args = new List<string>
        {
            "mousemove", "--sync",
            x.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture),
            "click", "--repeat", stepsY.ToString(CultureInfo.InvariantCulture), "--delay", "30", wheelY
        };
        if (deltaX != 0)
        {
            var stepsX = Math.Clamp(Math.Abs(deltaX) <= 5 ? Math.Abs(deltaX) : Math.Abs(deltaX) / 120, 1, 12);
            args.AddRange(new[]
            {
                "click", "--repeat", stepsX.ToString(CultureInfo.InvariantCulture), "--delay", "30",
                deltaX >= 0 ? "6" : "7"
            });
        }

        var result = await XdotoolAsync(args, ct).ConfigureAwait(false);
        if (!result.Success)
            return result;
        return new DesktopOpResult(
            true,
            $"scrolled guest ({x},{y}) dY={deltaY} dX={deltaX} in the {GuestOpenedMarker}.",
            new { x, y, deltaY, deltaX, coords = "guest-framebuffer" });
    }

    public async Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default)
    {
        var listed = await GuestRunAsync("/usr/bin/wmctrl", new[] { "-lG" }, wait: true, ct)
            .ConfigureAwait(false);
        if (listed.Success && TryFormatWmctrl(listed.Content, out var formatted, out var data))
            return new DesktopOpResult(true, formatted, data);

        var (w, h) = await TryGuestScreenSizeAsync(ct).ConfigureAwait(false);
        var cx = w / 2;
        var cy = h / 2;
        var fallback =
            "open desktop windows (Ubuntu guest framebuffer, origin 0,0 — not Windows):\n" +
            $"[0] Ubuntu desktop bounds=(0,0 {w}x{h}) center=({cx},{cy})";
        return new DesktopOpResult(
            true,
            fallback,
            new object[]
            {
                new { index = 0, title = "Ubuntu desktop", x = 0, y = 0, width = w, height = h, centerX = cx, centerY = cy }
            });
    }

    public async Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default)
    {
        var ask = (title ?? string.Empty).Trim();
        if (ask.Length == 0)
            return new DesktopOpResult(false, "focus title must be non-empty", null);
        if (ask.IndexOfAny(new[] { '\n', '\r', '\0' }) >= 0)
            return new DesktopOpResult(false, "focus title contains control characters", null);

        var result = await GuestRunAsync("/usr/bin/wmctrl", new[] { "-a", ask }, wait: true, ct)
            .ConfigureAwait(false);
        if (!result.Success)
            return result;
        return new DesktopOpResult(true, $"focused guest window '{ask}' in the {GuestOpenedMarker}.", null);
    }

    public static string MapGuestSearch(string alias)
    {
        var a = (alias ?? "").Trim().ToLowerInvariant();
        return a switch
        {
            "chrome" or "msedge" or "edge" or "browser" => "firefox",
            "firefox" => "firefox",
            "notepad" => "text editor",
            "explorer" or "file_explorer" => "files",
            "cmd" or "powershell" => "terminal",
            _ => a,
        };
    }

    public static bool TryMapGuestExe(string alias, string? url, out string exe, out string[] args)
    {
        exe = "";
        args = Array.Empty<string>();
        var search = MapGuestSearch(alias);
        switch (search)
        {
            case "firefox":
                exe = "/usr/bin/firefox";
                if (!string.IsNullOrWhiteSpace(url))
                    args = new[] { url.Trim() };
                return true;
            case "text editor":
                exe = "/usr/bin/gnome-text-editor";
                return true;
            case "files":
                exe = "/usr/bin/nautilus";
                args = new[] { "--new-window" };
                return true;
            case "terminal":
                exe = "/usr/bin/gnome-terminal";
                return true;
            default:
                return false;
        }
    }

    public static bool TryMapXdotoolKey(string? key, out string xdotool, out string error)
    {
        xdotool = "";
        error = "";
        if (string.IsNullOrWhiteSpace(key))
        {
            error = "key must be non-empty";
            return false;
        }

        var parts = key.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            error = "key must be non-empty";
            return false;
        }

        var mapped = new List<string>(parts.Length);
        foreach (var raw in parts)
        {
            var p = raw.Trim();
            var token = p.ToLowerInvariant() switch
            {
                "ctrl" or "control" => "ctrl",
                "alt" or "menu" => "alt",
                "shift" => "shift",
                "super" or "meta" or "win" or "cmd" => "super",
                "enter" or "return" => "Return",
                "esc" or "escape" => "Escape",
                "tab" => "Tab",
                "space" or "spc" => "space",
                "backspace" or "bksp" => "BackSpace",
                "delete" or "del" => "Delete",
                "home" => "Home",
                "end" => "End",
                "pageup" or "pgup" => "Page_Up",
                "pagedown" or "pgdn" => "Page_Down",
                "up" or "arrowup" => "Up",
                "down" or "arrowdown" => "Down",
                "left" or "arrowleft" => "Left",
                "right" or "arrowright" => "Right",
                _ when Regex.IsMatch(p, @"^f([1-9]|1[0-2])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    => "F" + p[1..],
                _ when p.Length == 1 => p.ToLowerInvariant(),
                _ => ""
            };
            if (token.Length == 0)
            {
                error = $"unsupported key '{key}'";
                return false;
            }

            mapped.Add(token);
        }

        xdotool = string.Join("+", mapped);
        return true;
    }

    public static bool TryParseWmctrlLine(
        string line, out int x, out int y, out int w, out int h, out string title)
    {
        x = y = w = h = 0;
        title = "";
        var m = WmctrlLine.Match((line ?? "").Trim());
        if (!m.Success)
            return false;
        x = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        y = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        w = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
        h = int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture);
        title = m.Groups[6].Value.Trim();
        return w > 0 && h > 0 && title.Length > 0;
    }

    public static bool TryParseLoggedInHeartbeat(string enumerateOutput, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (string.IsNullOrWhiteSpace(enumerateOutput))
            return false;
        foreach (var raw in enumerateOutput.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Contains("LoggedInUsersList", StringComparison.Ordinal))
                continue;
            var m = HeartbeatAt.Match(line);
            if (!m.Success)
                continue;
            var token = m.Groups[1].Value.Trim().TrimEnd(',');
            return DateTimeOffset.TryParse(
                token,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out timestamp);
        }

        return false;
    }

    public static bool TryReadPngSize(byte[] png, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (png is null || png.Length < 24)
            return false;
        if (png[0] != 0x89 || png[1] != (byte)'P' || png[2] != (byte)'N' || png[3] != (byte)'G')
            return false;
        width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        return width > 0 && height > 0;
    }

    private static bool TryMapMouseButton(string? button, out int code, out string error)
    {
        code = 1;
        error = "";
        var btn = (button ?? "left").Trim().ToLowerInvariant();
        code = btn switch
        {
            "left" => 1,
            "middle" => 2,
            "right" => 3,
            _ => 0
        };
        if (code == 0)
        {
            error = $"unsupported button '{button}' (use left|right|middle)";
            return false;
        }

        return true;
    }

    private static bool TryFormatWmctrl(string stdout, out string formatted, out List<object> data)
    {
        formatted = "";
        data = new List<object>();
        if (string.IsNullOrWhiteSpace(stdout))
            return false;

        var lines = new StringBuilder(
            "open desktop windows (Ubuntu guest framebuffer, origin 0,0 — not Windows):");
        var i = 0;
        foreach (var raw in stdout.Split('\n'))
        {
            if (!TryParseWmctrlLine(raw, out var x, out var y, out var w, out var h, out var title))
                continue;
            var cx = x + w / 2;
            var cy = y + h / 2;
            data.Add(new
            {
                index = i,
                title,
                x,
                y,
                width = w,
                height = h,
                centerX = cx,
                centerY = cy
            });
            lines.Append("\n[").Append(i).Append("] ").Append(title)
                .Append(" bounds=(").Append(x).Append(',').Append(y)
                .Append(' ').Append(w).Append('x').Append(h).Append(')')
                .Append(" center=(").Append(cx).Append(',').Append(cy).Append(')');
            i++;
        }

        if (data.Count == 0)
            return false;
        formatted = lines.ToString();
        return true;
    }

    private DesktopOpResult MissingPassword() => new(
        false,
        "Cannot drive the Ubuntu VM: guest password is not set. " +
        $"Add {SecretNames.VboxGuestPass} to SoulCore/.env (user '{_username()}') and restart Host. " +
        "Do not paste the password in chat. The VirtualBox window can stay minimized — this uses Guest Additions, not host clicks.",
        null);

    private Task<DesktopOpResult> XdotoolAsync(IReadOnlyList<string> args, CancellationToken ct)
        => GuestRunAsync("/usr/bin/xdotool", args, wait: true, ct);

    private async Task<DesktopOpResult> GuestStartAsync(
        string exe, IReadOnlyList<string> exeArgs, CancellationToken ct)
    {
        var auth = RequirePassword();
        if (auth.Error is not null)
            return auth.Error;

        var putenv = await SessionPutenvAsync(auth.User, auth.Password!, ct).ConfigureAwait(false);
        using var passFile = new PasswordFile(auth.Password!);
        var argv = BuildGuestControl(
            _vmName,
            "start",
            auth.User,
            passFile.Path,
            exe,
            putenv,
            exeArgs,
            waitOutput: false);
        var (exit, stdout, stderr) = await _run(_vboxManage, argv, ct).ConfigureAwait(false);
        if (exit != 0)
        {
            return new DesktopOpResult(
                false,
                $"guestcontrol start '{exe}' failed (exit {exit}): {Redact(JoinOut(stdout, stderr), auth.Password)}",
                null);
        }

        return new DesktopOpResult(true, "ok", null);
    }

    private async Task<DesktopOpResult> GuestRunAsync(
        string exe, IReadOnlyList<string> exeArgs, bool wait, CancellationToken ct,
        int timeoutMs = 20000)
    {
        var auth = RequirePassword();
        if (auth.Error is not null)
            return auth.Error;

        var putenv = await SessionPutenvAsync(auth.User, auth.Password!, ct).ConfigureAwait(false);
        using var passFile = new PasswordFile(auth.Password!);
        var argv = BuildGuestControl(
            _vmName,
            "run",
            auth.User,
            passFile.Path,
            exe,
            putenv,
            exeArgs,
            waitOutput: wait,
            timeoutMs: timeoutMs);
        var (exit, stdout, stderr) = await _run(_vboxManage, argv, ct).ConfigureAwait(false);
        if (exit != 0)
        {
            return new DesktopOpResult(
                false,
                $"guestcontrol run '{exe}' failed (exit {exit}): {Redact(JoinOut(stdout, stderr), auth.Password)}",
                null);
        }

        return new DesktopOpResult(true, stdout ?? "", null);
    }

    private async Task<DesktopOpResult> GuestCopyFromAsync(string guestPath, CancellationToken ct)
    {
        var auth = RequirePassword();
        if (auth.Error is not null)
            return auth.Error;

        var destDir = Path.Combine(Path.GetTempPath(), "hv-guest-shot");
        Directory.CreateDirectory(destDir);
        using var passFile = new PasswordFile(auth.Password!);
        var argv = new List<string>
        {
            "guestcontrol", _vmName, "copyfrom",
            "--username", auth.User,
            "--passwordfile", passFile.Path,
            "--quiet",
            "--target-directory", destDir,
            guestPath
        };
        var (exit, stdout, stderr) = await _run(_vboxManage, argv, ct).ConfigureAwait(false);
        if (exit != 0)
        {
            return new DesktopOpResult(
                false,
                $"guestcontrol copyfrom failed (exit {exit}): {Redact(JoinOut(stdout, stderr), auth.Password)}",
                null);
        }

        var hostFile = Path.Combine(destDir, Path.GetFileName(guestPath));
        if (!File.Exists(hostFile))
        {
            var hits = Directory.GetFiles(destDir, "*.png");
            if (hits.Length == 0)
                return new DesktopOpResult(false, "guest screenshot copied but no PNG on host", null);
            hostFile = hits[0];
        }

        var bytes = await File.ReadAllBytesAsync(hostFile, ct).ConfigureAwait(false);
        TryReadPngSize(bytes, out var width, out var height);
        return new DesktopOpResult(
            true,
            $"captured Ubuntu guest framebuffer {width}x{height} (origin 0,0 — not a Windows monitor). " +
            "Click with desktop_click using these guest coordinates. Host VirtualBox window can stay minimized.",
            new { path = hostFile, bytes, format = "png", width, height, backend = "vbox-guestcontrol", coords = "guest-framebuffer" });
    }

    private async Task<DesktopOpResult> HostScreenshotPngAsync(CancellationToken ct)
    {
        var path = Path.Combine(Path.GetTempPath(), "hv-vbox-screenshotpng.png");
        var argv = new[] { "controlvm", _vmName, "screenshotpng", path };
        var (exit, stdout, stderr) = await _run(_vboxManage, argv, ct).ConfigureAwait(false);
        if (exit != 0 || !File.Exists(path))
        {
            return new DesktopOpResult(
                false,
                $"VBoxManage screenshotpng failed (exit {exit}): {JoinOut(stdout, stderr)}",
                null);
        }

        var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        TryReadPngSize(bytes, out var width, out var height);
        return new DesktopOpResult(
            true,
            $"captured Ubuntu guest via screenshotpng {width}x{height} (may be stale if Guest Additions video is stuck).",
            new { path, bytes, format = "png", width, height, backend = "vbox-screenshotpng", coords = "guest-framebuffer" });
    }

    private async Task<(int Width, int Height)> TryGuestScreenSizeAsync(CancellationToken ct)
    {
        var info = await GuestRunAsync("/usr/bin/xdpyinfo", Array.Empty<string>(), wait: true, ct)
            .ConfigureAwait(false);
        if (info.Success)
        {
            var m = Regex.Match(info.Content ?? "", @"dimensions:\s+(\d+)x(\d+)");
            if (m.Success
                && int.TryParse(m.Groups[1].Value, out var w)
                && int.TryParse(m.Groups[2].Value, out var h)
                && w > 0 && h > 0)
            {
                return (w, h);
            }
        }

        return (1280, 800);
    }

    private (string User, string? Password, DesktopOpResult? Error) RequirePassword()
    {
        var user = _username();
        var password = _password();
        if (string.IsNullOrWhiteSpace(password))
            return (user, null, MissingPassword());
        return (user, password, null);
    }

    private async Task<IReadOnlyList<string>> SessionPutenvAsync(
        string user, string password, CancellationToken ct)
    {
        var uid = await ResolveUidAsync(user, password, ct).ConfigureAwait(false);
        var xauth = await ResolveXauthorityAsync(user, password, uid, ct).ConfigureAwait(false);
        return new[]
        {
            "DISPLAY=:0",
            "WAYLAND_DISPLAY=wayland-0",
            "GDK_BACKEND=x11",
            $"HOME=/home/{user}",
            $"USER={user}",
            $"LOGNAME={user}",
            $"XDG_RUNTIME_DIR=/run/user/{uid}",
            $"DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/{uid}/bus",
            $"XAUTHORITY={xauth}",
            "GNOME_ACCESSIBILITY=1",
            "GTK_MODULES=gail:atk-bridge",
            "NO_AT_BRIDGE=0"
        };
    }

    private async Task<string> ResolveUidAsync(string user, string password, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_cachedUid))
            return _cachedUid!;

        var probe = await GuestRunMinimalAsync(
                user,
                password,
                "/usr/bin/id",
                new[] { "-u" },
                ct)
            .ConfigureAwait(false);
        var uid = (probe.Stdout ?? "").Trim();
        if (!Regex.IsMatch(uid, @"^\d+$"))
            uid = "1000";
        _cachedUid = uid;
        return uid;
    }

    private async Task<string> ResolveXauthorityAsync(
        string user, string password, string uid, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_cachedXauthority))
            return _cachedXauthority!;

        // Ubuntu GNOME on Wayland: Xwayland auth is under /run/user/<uid>/.mutter-Xwaylandauth.*
        var probe = await GuestRunMinimalAsync(
                user,
                password,
                "/bin/bash",
                new[]
                {
                    "-lc",
                    $"ls /run/user/{uid}/.mutter-Xwaylandauth.* 2>/dev/null | head -1; " +
                    $"test -f /home/{user}/.Xauthority && echo /home/{user}/.Xauthority"
                },
                ct)
            .ConfigureAwait(false);
        var line = (probe.Stdout ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(s => s.StartsWith('/'));
        _cachedXauthority = string.IsNullOrWhiteSpace(line)
            ? $"/home/{user}/.Xauthority"
            : line.Trim();
        return _cachedXauthority;
    }

    private async Task<(int Exit, string Stdout, string Stderr)> GuestRunMinimalAsync(
        string user,
        string password,
        string exe,
        IReadOnlyList<string> exeArgs,
        CancellationToken ct)
    {
        using var passFile = new PasswordFile(password);
        var argv = BuildGuestControl(
            _vmName,
            "run",
            user,
            passFile.Path,
            exe,
            Array.Empty<string>(),
            exeArgs,
            waitOutput: true);
        return await _run(_vboxManage, argv, ct).ConfigureAwait(false);
    }

    public static List<string> BuildGuestControl(
        string vmName,
        string verb,
        string username,
        string passwordFile,
        string exe,
        IReadOnlyList<string> putenv,
        IReadOnlyList<string> exeArgs,
        bool waitOutput,
        int timeoutMs = 20000)
    {
        var argv = new List<string>
        {
            "guestcontrol", vmName, verb,
            "--username", username,
            "--passwordfile", passwordFile,
            "--profile",
            "--quiet",
            "--exe", exe
        };
        foreach (var env in putenv)
        {
            argv.Add("--putenv");
            argv.Add(env);
        }

        if (waitOutput)
        {
            argv.Add("--wait-stdout");
            argv.Add("--wait-stderr");
            argv.Add("--timeout");
            argv.Add(Math.Max(1000, timeoutMs).ToString(CultureInfo.InvariantCulture));
        }

        argv.Add("--");
        // Guest argv after "--": do NOT prepend <exe>. VBoxManage supplies argv[0]
        // from --exe; duplicating the path breaks tools that parse USER args (id).
        argv.AddRange(exeArgs);
        return argv;
    }

    private static string JoinOut(string stdout, string stderr)
    {
        var s = (stderr ?? "").Trim();
        if (s.Length == 0)
            s = (stdout ?? "").Trim();
        return s;
    }

    private static string Redact(string text, string? password)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(password))
            return text;
        return text.Replace(password, "***", StringComparison.Ordinal);
    }

    private static bool LooksLikeUrl(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        var t = s.Trim().Trim('"');
        return t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> RunVBoxManageAsync(
        string fileName, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in arguments)
            psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stdout.AppendLine(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderr.AppendLine(e.Data);
        };
        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            return (1, "", $"failed to start VBoxManage: {ex.Message}");
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private sealed class PasswordFile : IDisposable
    {
        public string Path { get; }

        public PasswordFile(string password)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "hv-vbox-" + Guid.NewGuid().ToString("N") + ".pass");
            File.WriteAllText(Path, password, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(Path))
                    File.Delete(Path);
            }
            catch
            {
                // best-effort
            }
        }
    }
}
