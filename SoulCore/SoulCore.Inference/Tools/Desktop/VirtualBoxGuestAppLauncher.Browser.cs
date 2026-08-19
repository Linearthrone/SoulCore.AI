using System.Globalization;
using System.Text;
using System.Text.Json;
using SoulCore.Inference.Tools.Browser;

namespace SoulCore.Inference.Tools.Desktop;

public sealed partial class VirtualBoxGuestAppLauncher
{
    public async Task<DesktopOpResult> BrowserNavigateAsync(string url, CancellationToken ct = default)
    {
        var target = NormalizeUrl(url);
        if (target is null)
            return new DesktopOpResult(false, "browser_navigate needs an http(s) URL.", null);

        var started = await GuestStartAsync("/usr/bin/firefox", new[] { target }, ct).ConfigureAwait(false);
        if (!started.Success)
            return started;

        await TryFocusFirefoxAsync(ct).ConfigureAwait(false);
        // BED-194: spawn ≠ loaded/logged-in. action_ok only; goal_complete=false.
        return new DesktopOpResult(
            true,
            $"Firefox launched toward {target} in the {GuestOpenedMarker} (not Kurt's Windows Chrome). " +
            "goal_complete=false — page load NOT verified. Call browser_snapshot / browser_click_text before claiming navigated or logged in.",
            BrowserResultHonesty.LaunchOnly(target, "vbox-guest"));
    }

    public async Task<DesktopOpResult> BrowserSnapshotAsync(string? query = null, CancellationToken ct = default)
    {
        await EnsureGuestBrowserScriptAsync(ct).ConfigureAwait(false);
        await TryEnableA11yAsync(ct).ConfigureAwait(false);
        var args = string.IsNullOrWhiteSpace(query)
            ? new[] { GuestBrowserScript.GuestPath, "snapshot" }
            : new[] { GuestBrowserScript.GuestPath, "snapshot", query.Trim() };
        var raw = await GuestPythonAsync(args, ct).ConfigureAwait(false);
        return FormatSnapshot(raw, query);
    }

    public async Task<DesktopOpResult> BrowserClickTextAsync(
        string text, int nth = 1, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new DesktopOpResult(false, "browser_click_text needs visible text (e.g. Login).", null);

        await EnsureGuestBrowserScriptAsync(ct).ConfigureAwait(false);
        await TryEnableA11yAsync(ct).ConfigureAwait(false);
        var n = Math.Max(1, nth);
        var raw = await GuestPythonAsync(
                new[] { GuestBrowserScript.GuestPath, "click_text", text.Trim(), n.ToString(CultureInfo.InvariantCulture) },
                ct)
            .ConfigureAwait(false);
        if (!raw.Success)
            return raw;

        if (!TryReadPicked(raw.Content, out var x, out var y, out var label, out var err))
            return new DesktopOpResult(false, err ?? raw.Content, null);

        var click = await ClickAsync(x, y, "left", 1, ct).ConfigureAwait(false);
        if (!click.Success)
            return click;
        return new DesktopOpResult(
            true,
            $"clicked '{label}' at guest ({x},{y}) in the {GuestOpenedMarker}.\n" + raw.Content,
            new { x, y, text = label, coords = "guest-framebuffer" });
    }

    public async Task<DesktopOpResult> BrowserFillAsync(
        string field, string value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(field))
            return new DesktopOpResult(false, "browser_fill needs a field name (Email, Password, Search).", null);

        await EnsureGuestBrowserScriptAsync(ct).ConfigureAwait(false);
        await TryEnableA11yAsync(ct).ConfigureAwait(false);
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        var raw = await GuestPythonAsync(
                new[] { GuestBrowserScript.GuestPath, "fill", field.Trim(), b64 },
                ct)
            .ConfigureAwait(false);
        if (!raw.Success)
            return raw;
        if (!TryReadPicked(raw.Content, out var x, out var y, out var label, out var err))
            return new DesktopOpResult(false, err ?? raw.Content, null);

        var click = await ClickAsync(x, y, "left", 1, ct).ConfigureAwait(false);
        if (!click.Success)
            return click;
        await KeyAsync("Ctrl+A", ct).ConfigureAwait(false);
        var typed = await TypeAsync(value ?? "", ct).ConfigureAwait(false);
        if (!typed.Success)
            return typed;
        return new DesktopOpResult(
            true,
            $"filled '{label}' ({(value ?? "").Length} chars) at guest ({x},{y}) in the {GuestOpenedMarker}.",
            new { x, y, field = label, coords = "guest-framebuffer" });
    }

    public async Task<DesktopOpResult> BrowserBackAsync(CancellationToken ct = default)
    {
        await TryFocusFirefoxAsync(ct).ConfigureAwait(false);
        var key = await KeyAsync("Alt+Left", ct).ConfigureAwait(false);
        if (!key.Success)
            return key;
        return new DesktopOpResult(true, $"browser back (Alt+Left) in the {GuestOpenedMarker}.", null);
    }

    public async Task<DesktopOpResult> BrowserTabsAsync(CancellationToken ct = default)
    {
        await EnsureGuestBrowserScriptAsync(ct).ConfigureAwait(false);
        var raw = await GuestPythonAsync(new[] { GuestBrowserScript.GuestPath, "tabs" }, ct)
            .ConfigureAwait(false);
        return FormatSnapshot(raw, "tab");
    }

    private async Task<DesktopOpResult> EnsureGuestBrowserScriptAsync(CancellationToken ct)
    {
        var host = Path.Combine(Path.GetTempPath(), "hv-browser.py");
        await File.WriteAllTextAsync(host, GuestBrowserScript.Source.Replace("\r\n", "\n"), ct)
            .ConfigureAwait(false);
        return await GuestCopyToAsync(host, "/tmp", ct).ConfigureAwait(false);
    }

    private async Task TryEnableA11yAsync(CancellationToken ct)
    {
        try
        {
            await GuestRunAsync(
                    "/usr/bin/gsettings",
                    new[] { "set", "org.gnome.desktop.interface", "toolkit-accessibility", "true" },
                    wait: true,
                    ct)
                .ConfigureAwait(false);
        }
        catch
        {
            // best-effort
        }
    }

    private async Task TryFocusFirefoxAsync(CancellationToken ct)
    {
        try
        {
            await FocusWindowAsync("Firefox", ct).ConfigureAwait(false);
        }
        catch
        {
            // best-effort
        }
    }

    private async Task<DesktopOpResult> GuestPythonAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var run = await GuestRunAsync("/usr/bin/python3", args, wait: true, ct, timeoutMs: 45000)
            .ConfigureAwait(false);
        if (!run.Success)
            return run;
        var stdout = (run.Content ?? "").Trim();
        if (string.IsNullOrWhiteSpace(stdout))
            return new DesktopOpResult(false, "guest python produced no output", null);
        try
        {
            using var doc = JsonDocument.Parse(ExtractJsonObject(stdout));
            var ok = doc.RootElement.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;
            return new DesktopOpResult(ok, stdout, null);
        }
        catch (JsonException)
        {
            return new DesktopOpResult(
                false,
                "guest python did not return JSON (is python3-gi / gir1.2-atspi-2.0 installed?):\n" + stdout,
                null);
        }
    }

    private async Task<DesktopOpResult> GuestCopyToAsync(string hostPath, string guestDir, CancellationToken ct)
    {
        var auth = RequirePassword();
        if (auth.Error is not null)
            return auth.Error;

        using var passFile = new PasswordFile(auth.Password!);
        var argv = new List<string>
        {
            "guestcontrol", _vmName, "copyto",
            "--username", auth.User,
            "--passwordfile", passFile.Path,
            "--quiet",
            "--target-directory", guestDir,
            hostPath
        };
        var (exit, stdout, stderr) = await _run(_vboxManage, argv, ct).ConfigureAwait(false);
        if (exit != 0)
        {
            return new DesktopOpResult(
                false,
                $"guestcontrol copyto failed (exit {exit}): {Redact(JoinOut(stdout, stderr), auth.Password)}",
                null);
        }

        return new DesktopOpResult(true, "copied", null);
    }

    private static DesktopOpResult FormatSnapshot(DesktopOpResult raw, string? query)
    {
        if (!raw.Success)
            return raw;
        try
        {
            using var doc = JsonDocument.Parse(ExtractJsonObject(raw.Content ?? ""));
            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var okEl) && okEl.ValueKind != JsonValueKind.True)
            {
                var err = root.TryGetProperty("error", out var e) ? e.GetString() : raw.Content;
                return new DesktopOpResult(false, err ?? "snapshot failed", null);
            }

            var sb = new StringBuilder();
            sb.Append("interactive controls in Ubuntu Firefox (guest framebuffer coords, origin 0,0):");
            if (!string.IsNullOrWhiteSpace(query))
                sb.Append(" filter=").Append(query.Trim());
            var count = 0;
            if (root.TryGetProperty("elements", out var els) && els.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in els.EnumerateArray())
                {
                    var name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var role = el.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "";
                    var x = el.TryGetProperty("x", out var xe) && xe.TryGetInt32(out var xi) ? xi : 0;
                    var y = el.TryGetProperty("y", out var ye) && ye.TryGetInt32(out var yi) ? yi : 0;
                    var w = el.TryGetProperty("w", out var we) && we.TryGetInt32(out var wi) ? wi : 0;
                    var h = el.TryGetProperty("h", out var he) && he.TryGetInt32(out var hi) ? hi : 0;
                    var cx = x + w / 2;
                    var cy = y + h / 2;
                    sb.Append("\n[").Append(count).Append("] ").Append(role)
                        .Append(" '").Append(name).Append("' bounds=(")
                        .Append(x).Append(',').Append(y).Append(' ').Append(w).Append('x').Append(h)
                        .Append(") center=(").Append(cx).Append(',').Append(cy).Append(')');
                    count++;
                    if (count >= 80)
                        break;
                }
            }

            if (count == 0)
            {
                sb.Append("\n(none — call desktop_screenshot and click from the image, or install python3-gi gir1.2-atspi-2.0 in the guest)");
            }
            else
            {
                sb.Append("\nPrefer browser_click_text with the visible name. desktop_click uses these guest coords.");
            }

            return new DesktopOpResult(true, sb.ToString(), null);
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    private static bool TryReadPicked(
        string? json, out int x, out int y, out string label, out string? error)
    {
        x = 0;
        y = 0;
        label = "";
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(ExtractJsonObject(json ?? ""));
            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var okEl) && okEl.ValueKind != JsonValueKind.True)
            {
                error = root.TryGetProperty("error", out var e) ? e.GetString() : json;
                return false;
            }

            if (!root.TryGetProperty("picked", out var picked) || picked.ValueKind != JsonValueKind.Object)
            {
                error = json;
                return false;
            }

            var hasCx = picked.TryGetProperty("cx", out var cxe) && cxe.TryGetInt32(out x);
            var hasCy = picked.TryGetProperty("cy", out var cye) && cye.TryGetInt32(out y);
            if (!hasCx || !hasCy)
            {
                var px = picked.TryGetProperty("x", out var xe) && xe.TryGetInt32(out var xi) ? xi : 0;
                var py = picked.TryGetProperty("y", out var ye) && ye.TryGetInt32(out var yi) ? yi : 0;
                var w = picked.TryGetProperty("w", out var we) && we.TryGetInt32(out var wi) ? wi : 0;
                var h = picked.TryGetProperty("h", out var he) && he.TryGetInt32(out var hi) ? hi : 0;
                x = px + w / 2;
                y = py + h / 2;
            }

            label = picked.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(label))
                label = "control";
            return x >= 0 && y >= 0;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return text;
        return text[start..(end + 1)];
    }

    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        var t = url.Trim().Trim('"');
        if (t.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            t = "https://" + t;
        if (!t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return t;
    }
}
