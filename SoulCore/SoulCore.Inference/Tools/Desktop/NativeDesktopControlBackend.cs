using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Native C# desktop backend (BED-135 Pass path — required).
/// <list type="bullet">
/// <item>Windows: GDI BitBlt capture (BMP) + Win32 <c>SendInput</c> / <c>EnumWindows</c>.</item>
/// <item>Linux: screenshot via ImageMagick <c>import</c> / <c>gnome-screenshot</c> / <c>scrot</c> when on PATH;
/// click/type/key/list/focus return honest <c>Success:false</c> (Windows-primary).</item>
/// <item>Other OS: all actions return platform-not-supported.</item>
/// </list>
/// </summary>
public sealed class NativeDesktopControlBackend : IDesktopControlBackend
{
    private readonly string _captureDirectory;

    public NativeDesktopControlBackend()
        : this(ResolveDefaultCaptureDirectory())
    {
    }

    /// <summary>Test / override ctor — writes screenshots under <paramref name="captureDirectory"/>.</summary>
    public NativeDesktopControlBackend(string captureDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(captureDirectory);
        _captureDirectory = Path.GetFullPath(captureDirectory);
        Directory.CreateDirectory(_captureDirectory);
    }

    public Task<DesktopBackendResult> ScreenshotAsync(int monitor, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (monitor < 0)
            return Task.FromResult(Fail("monitor must be >= 0"));

        try
        {
            if (OperatingSystem.IsWindows())
                return Task.FromResult(ScreenshotWindows(monitor));
            if (OperatingSystem.IsLinux())
                return Task.FromResult(ScreenshotLinux(monitor));
            return Task.FromResult(Fail(
                $"desktop_screenshot is not supported on {RuntimeInformation.OSDescription}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail($"desktop_screenshot failed: {ex.Message}"));
        }
    }

    public Task<DesktopBackendResult> ClickAsync(int x, int y, string button, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var btn = string.IsNullOrWhiteSpace(button) ? "left" : button.Trim().ToLowerInvariant();
        if (btn is not ("left" or "right" or "middle"))
            return Task.FromResult(Fail("button must be left, right, or middle"));

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(Fail(
                $"desktop_click is Windows-primary; not supported on {OsLabel()} (use a Windows Host or Hermes MCP stretch)"));
        }

        try
        {
            ClickWindows(x, y, btn);
            return Task.FromResult(Ok($"clicked {btn} at ({x},{y})", new { x, y, button = btn, platform = "windows" }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail($"desktop_click failed: {ex.Message}"));
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ClickWindows(int x, int y, string button) => Win32Input.Click(x, y, button);

    public Task<DesktopBackendResult> TypeAsync(string text, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
            return Task.FromResult(Fail("text must be non-empty"));

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(Fail(
                $"desktop_type is Windows-primary; not supported on {OsLabel()} (use a Windows Host or Hermes MCP stretch)"));
        }

        try
        {
            TypeWindows(text);
            return Task.FromResult(Ok($"typed {text.Length} chars", new { length = text.Length, platform = "windows" }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail($"desktop_type failed: {ex.Message}"));
        }
    }

    [SupportedOSPlatform("windows")]
    private static void TypeWindows(string text) => Win32Input.TypeText(text);

    public Task<DesktopBackendResult> KeyAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(key))
            return Task.FromResult(Fail("key must be non-empty"));

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(Fail(
                $"desktop_key is Windows-primary; not supported on {OsLabel()} (use a Windows Host or Hermes MCP stretch)"));
        }

        try
        {
            if (!KeyWindows(key.Trim(), out var reason))
                return Task.FromResult(Fail(reason ?? "unknown key"));
            return Task.FromResult(Ok($"pressed key '{key.Trim()}'", new { key = key.Trim(), platform = "windows" }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail($"desktop_key failed: {ex.Message}"));
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool KeyWindows(string key, out string? reason) => Win32Input.TryPressKey(key, out reason);

    public Task<DesktopBackendResult> ListWindowsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(Fail(
                $"list_desktop_windows is Windows-primary; not supported on {OsLabel()}"));
        }

        try
        {
            return Task.FromResult(ListWindowsWindows());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail($"list_desktop_windows failed: {ex.Message}"));
        }
    }

    [SupportedOSPlatform("windows")]
    private static DesktopBackendResult ListWindowsWindows()
    {
        var windows = Win32Windows.ListVisibleWindows();
        var lines = windows.Count == 0
            ? "(no visible top-level windows)"
            : string.Join('\n', windows.Select((w, i) =>
                $"{i}: hwnd=0x{w.Hwnd.ToInt64():X} title={w.Title}"));
        return Ok(lines, new { count = windows.Count, windows });
    }

    public Task<DesktopBackendResult> FocusWindowAsync(string title, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(title))
            return Task.FromResult(Fail("title must be non-empty"));

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(Fail(
                $"focus_desktop_window is Windows-primary; not supported on {OsLabel()}"));
        }

        try
        {
            return Task.FromResult(FocusWindowWindows(title.Trim()));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail($"focus_desktop_window failed: {ex.Message}"));
        }
    }

    [SupportedOSPlatform("windows")]
    private static DesktopBackendResult FocusWindowWindows(string title)
    {
        if (!Win32Windows.TryFocusByTitle(title, out var matched, out var reason))
            return Fail(reason ?? "window not found");
        return Ok($"focused window '{matched}'", new { title = matched, platform = "windows" });
    }

    [SupportedOSPlatform("windows")]
    private DesktopBackendResult ScreenshotWindows(int monitor)
    {
        var preferred = NextCapturePath("win", ".bmp");
        var path = Win32Capture.CaptureMonitor(monitor, preferred, out var width, out var height);
        var info = new FileInfo(path);
        return Ok(
            $"captured monitor {monitor} ({width}x{height}) → {path}",
            new { path, monitor, width, height, bytes = info.Length, platform = "windows", format = "bmp" });
    }

    private DesktopBackendResult ScreenshotLinux(int monitor)
    {
        var path = NextCapturePath("linux", ".png");
        var attempts = new (string FileName, string Arguments)[]
        {
            ("import", $"-window root {Quote(path)}"),
            ("gnome-screenshot", $"-f {Quote(path)}"),
            ("scrot", Quote(path)),
        };

        var errors = new StringBuilder();
        foreach (var (file, args) in attempts)
        {
            if (!TryRun(file, args, out var err))
            {
                errors.AppendLine($"{file}: {err}");
                continue;
            }

            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                errors.AppendLine($"{file}: produced empty/missing file");
                continue;
            }

            var info = new FileInfo(path);
            var note = monitor == 0
                ? $"captured desktop via {file} → {path}"
                : $"captured desktop via {file} → {path} (monitor={monitor} ignored on Linux; primary/root only)";
            return Ok(note, new
            {
                path,
                monitor,
                bytes = info.Length,
                platform = "linux",
                tool = file,
                format = "png",
            });
        }

        return Fail(
            "desktop_screenshot unavailable on Linux: need ImageMagick `import`, `gnome-screenshot`, or `scrot` on PATH. "
            + errors.ToString().Trim());
    }

    private string NextCapturePath(string tag, string extension)
    {
        var name = $"desktop_{tag}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}{extension}";
        return Path.Combine(_captureDirectory, name);
    }

    private static string ResolveDefaultCaptureDirectory()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
            local = Path.GetTempPath();
        return Path.Combine(local, "SoulCore", "desktop-captures");
    }

    private static bool TryRun(string fileName, string arguments, out string error)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (proc is null)
            {
                error = "failed to start process";
                return false;
            }

            if (!proc.WaitForExit(15_000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                error = "timed out";
                return false;
            }

            if (proc.ExitCode != 0)
            {
                var stderr = proc.StandardError.ReadToEnd();
                error = string.IsNullOrWhiteSpace(stderr) ? $"exit {proc.ExitCode}" : stderr.Trim();
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string Quote(string path)
        => $"\"{path.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static string OsLabel() => OperatingSystem.IsLinux() ? "Linux" :
        OperatingSystem.IsMacOS() ? "macOS" : RuntimeInformation.OSDescription;

    private static DesktopBackendResult Ok(string message, object? data = null)
        => new(true, message, data);

    private static DesktopBackendResult Fail(string message)
        => new(false, message, null);
}
