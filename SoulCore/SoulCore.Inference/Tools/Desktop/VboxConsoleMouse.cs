using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Injects guest mouse events through the VirtualBox COM console API
/// (<c>IMouse.putMouseEventAbsolute</c>). VirtualBox COM must run on an STA
/// thread (ASP.NET Host is MTA — in-process calls otherwise fail/no-op). Falls
/// back to <c>powershell -STA</c> when in-process COM is unavailable.
/// </summary>
public static class VboxConsoleMouse
{
    public const int LeftButton = 0x01;
    public const int RightButton = 0x02;
    public const int MiddleButton = 0x04;
    public const int LockTypeShared = 1;

    /// <summary>
    /// Guest framebuffer (0,0) → VBox absolute (1,1) origin; button mask for down/up.
    /// </summary>
    public static (int Ax, int Ay, int DownFlags) PlanAbsolutePress(int x, int y, int xdotoolButton)
    {
        var ax = Math.Max(0, x) + 1;
        var ay = Math.Max(0, y) + 1;
        var flags = xdotoolButton switch
        {
            1 => LeftButton,
            2 => MiddleButton,
            3 => RightButton,
            _ => LeftButton
        };
        return (ax, ay, flags);
    }

    /// <summary>
    /// Click at guest framebuffer coords. Returns null when COM/VBox is unavailable
    /// (non-Windows) so callers can fall back.
    /// </summary>
    public static DesktopOpResult? TryClick(string vmName, int x, int y, int xdotoolButton, int clicks)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        if (string.IsNullOrWhiteSpace(vmName) || x < 0 || y < 0 || clicks is not (1 or 2))
            return null;

        var name = vmName.Trim();
        var (ax, ay, down) = PlanAbsolutePress(x, y, xdotoolButton);

        // 1) In-process COM on a dedicated STA thread (Host request threads are MTA).
        DesktopOpResult? sta = null;
        if (OperatingSystem.IsWindows())
            sta = RunOnStaWindows(() => ClickComSta(name, x, y, ax, ay, down, clicks));
        if (sta is not null && sta.Success)
            return sta;

        // 2) powershell.exe -STA — reliable with VirtualBox.VirtualBox ProgID.
        var ps = ClickViaPowerShellSta(name, x, y, ax, ay, down, clicks);
        if (ps is not null && ps.Success)
            return ps;

        // Prefer the more specific failure if both tried.
        if (ps is not null)
            return ps;
        return sta ?? new DesktopOpResult(
            false,
            "VirtualBox console mouse unavailable (COM STA + PowerShell -STA both failed).",
            null);
    }

    [SupportedOSPlatform("windows")]
    private static DesktopOpResult? RunOnStaWindows(Func<DesktopOpResult?> work)
    {
        DesktopOpResult? result = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = work();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(15)))
        {
            return new DesktopOpResult(false, "VirtualBox console mouse STA thread timed out.", null);
        }

        if (error is COMException com)
        {
            return new DesktopOpResult(
                false,
                $"VirtualBox console mouse COM failed (0x{com.ErrorCode:X8}): {com.Message}",
                null);
        }

        if (error is not null)
        {
            return new DesktopOpResult(
                false,
                $"VirtualBox console mouse failed: {error.GetType().Name}: {error.Message}",
                null);
        }

        return result;
    }

    [SupportedOSPlatform("windows")]
    private static DesktopOpResult? ClickComSta(
        string vmName, int x, int y, int ax, int ay, int down, int clicks)
    {
        var vboxType = Type.GetTypeFromProgID("VirtualBox.VirtualBox");
        var sessionType = Type.GetTypeFromProgID("VirtualBox.Session");
        if (vboxType is null || sessionType is null)
            return null;

        dynamic? vbox = null;
        dynamic? session = null;
        try
        {
            vbox = Activator.CreateInstance(vboxType)
                ?? throw new InvalidOperationException("VirtualBox.VirtualBox create failed");
            session = Activator.CreateInstance(sessionType)
                ?? throw new InvalidOperationException("VirtualBox.Session create failed");
            dynamic machine = vbox.FindMachine(vmName);
            machine.LockMachine(session, LockTypeShared);
            try
            {
                dynamic console = session.Console
                    ?? throw new InvalidOperationException(
                        "VirtualBox session.Console is null — is victoria-sandbox running?");
                dynamic mouse = console.Mouse
                    ?? throw new InvalidOperationException("VirtualBox Console.Mouse is null");

                bool absolute = false;
                try { absolute = (bool)mouse.AbsoluteSupported; }
                catch { absolute = true; }
                if (!absolute)
                {
                    return new DesktopOpResult(
                        false,
                        "VirtualBox Absolute mouse not supported by guest (Guest Additions?).",
                        null);
                }

                // Do NOT send out-of-range after the click — that can cancel the press.
                mouse.PutMouseEventAbsolute(ax, ay, 0, 0, 0);
                Thread.Sleep(50);

                for (var i = 0; i < clicks; i++)
                {
                    mouse.PutMouseEventAbsolute(ax, ay, 0, 0, down);
                    Thread.Sleep(50);
                    mouse.PutMouseEventAbsolute(ax, ay, 0, 0, 0);
                    if (i + 1 < clicks)
                        Thread.Sleep(90);
                }

                Thread.Sleep(30);

                var label = clicks == 2 ? "double-clicked" : "clicked";
                return new DesktopOpResult(
                    true,
                    $"{label} via VirtualBox console mouse (STA) at guest ({x},{y}) in the Ubuntu VM.",
                    new { x, y, clicks, coords = "guest-framebuffer", method = "vbox-console-mouse" });
            }
            finally
            {
                try { session.UnlockMachine(); }
                catch { /* best-effort */ }
            }
        }
        finally
        {
            if (session is not null)
            {
                try { Marshal.FinalReleaseComObject(session); }
                catch { /* ignore */ }
            }

            if (vbox is not null)
            {
                try { Marshal.FinalReleaseComObject(vbox); }
                catch { /* ignore */ }
            }
        }
    }

    private static DesktopOpResult? ClickViaPowerShellSta(
        string vmName, int x, int y, int ax, int ay, int down, int clicks)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        // Escape single quotes for PowerShell single-quoted string.
        var safeName = vmName.Replace("'", "''", StringComparison.Ordinal);
        var sb = new StringBuilder();
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine($"$vmName = '{safeName}'");
        sb.AppendLine($"$ax = {ax}; $ay = {ay}; $down = {down}; $clicks = {clicks}");
        sb.AppendLine("$vb = New-Object -ComObject VirtualBox.VirtualBox");
        sb.AppendLine("$machine = $vb.FindMachine($vmName)");
        sb.AppendLine("$session = New-Object -ComObject VirtualBox.Session");
        sb.AppendLine("$machine.LockMachine($session, 1)");
        sb.AppendLine("try {");
        sb.AppendLine("  $mouse = $session.Console.Mouse");
        sb.AppendLine("  if ($null -eq $mouse) { throw 'Console.Mouse is null' }");
        sb.AppendLine("  $mouse.PutMouseEventAbsolute($ax, $ay, 0, 0, 0)");
        sb.AppendLine("  Start-Sleep -Milliseconds 50");
        sb.AppendLine("  for ($i = 0; $i -lt $clicks; $i++) {");
        sb.AppendLine("    $mouse.PutMouseEventAbsolute($ax, $ay, 0, 0, $down)");
        sb.AppendLine("    Start-Sleep -Milliseconds 50");
        sb.AppendLine("    $mouse.PutMouseEventAbsolute($ax, $ay, 0, 0, 0)");
        sb.AppendLine("    if ($i + 1 -lt $clicks) { Start-Sleep -Milliseconds 90 }");
        sb.AppendLine("  }");
        sb.AppendLine("  'ok'");
        sb.AppendLine("} finally { try { $session.UnlockMachine() } catch {} }");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -STA -ExecutionPolicy Bypass -Command -",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return new DesktopOpResult(false, "powershell.exe failed to start for VBox mouse.", null);

            proc.StandardInput.Write(sb.ToString());
            proc.StandardInput.Close();
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(15000))
            {
                try { proc.Kill(entireProcessTree: true); }
                catch { /* ignore */ }
                return new DesktopOpResult(false, "powershell -STA VBox mouse timed out.", null);
            }

            if (proc.ExitCode != 0 || stdout.IndexOf("ok", StringComparison.OrdinalIgnoreCase) < 0)
            {
                var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                return new DesktopOpResult(
                    false,
                    $"powershell -STA VBox mouse failed (exit {proc.ExitCode}): {detail.Trim()}",
                    null);
            }

            var label = clicks == 2 ? "double-clicked" : "clicked";
            return new DesktopOpResult(
                true,
                $"{label} via VirtualBox console mouse (powershell -STA) at guest ({x},{y}) in the Ubuntu VM.",
                new { x, y, clicks, coords = "guest-framebuffer", method = "vbox-console-mouse-ps" });
        }
        catch (Exception ex)
        {
            return new DesktopOpResult(
                false,
                $"powershell -STA VBox mouse failed: {ex.GetType().Name}: {ex.Message}",
                null);
        }
    }
}
