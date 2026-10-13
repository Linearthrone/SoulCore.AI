using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace House.ChatDesktop.Services;

/// <summary>
/// Injects operator pointer events into victoria-sandbox through VirtualBox COM.
/// The reparented VirtualBox window receives Win32 clicks and does not forward
/// them into the guest; <c>IMouse.putMouseEventAbsolute</c> does.
/// </summary>
public static class VictoriaVmPointer
{
    private static readonly object Gate = new();
    private static readonly List<PointerCmd> Pending = new();
    private static readonly AutoResetEvent Signal = new(false);
    private static Thread? _thread;
    private static string _vmName = LocalStackControl.SandboxVmName;
    private static nint _excludeHwnd;
    private static bool _stop;
    private static string? _lastFault;

    public static event Action<string>? Faulted;

    public static void NoteHostWindow(nint hwnd) => _excludeHwnd = hwnd;

    public static void EnsureStarted(string vmName)
    {
        if (!OperatingSystem.IsWindows())
            return;
        lock (Gate)
        {
            _vmName = string.IsNullOrWhiteSpace(vmName)
                ? LocalStackControl.SandboxVmName
                : vmName.Trim();
            _stop = false;
            if (_thread is { IsAlive: true })
                return;
            _thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "VictoriaVmPointer"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }
    }

    public static void Stop()
    {
        Thread? thread;
        lock (Gate)
        {
            _stop = true;
            Pending.Clear();
            thread = _thread;
            _thread = null;
        }

        Signal.Set();
        if (thread is { IsAlive: true })
            thread.Join(TimeSpan.FromSeconds(2));
    }

    public static void Post(int guestX, int guestY, int buttons, int wheelNotches)
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (guestX < 0 || guestY < 0)
            return;

        lock (Gate)
        {
            Pending.Add(new PointerCmd(guestX, guestY, buttons, wheelNotches));
            if (Pending.Count > 32)
                Pending.RemoveAt(0);
        }

        Signal.Set();
    }

    [SupportedOSPlatform("windows")]
    private static void ThreadMain()
    {
        dynamic? vbox = null;
        dynamic? session = null;
        dynamic? mouse = null;
        try
        {
            while (true)
            {
                PointerCmd cmd = default;
                var have = false;
                lock (Gate)
                {
                    if (_stop && Pending.Count == 0)
                        break;
                    if (Pending.Count > 0)
                    {
                        cmd = Pending[0];
                        Pending.RemoveAt(0);
                        have = true;
                    }
                }

                if (!have)
                {
                    Signal.WaitOne(200);
                    continue;
                }

                try
                {
                    if (mouse is null)
                        mouse = TryOpenMouse(ref vbox, ref session);
                    if (mouse is null)
                        continue;

                    var (ax, ay) = VictoriaVmPointerPlan.ToAbsolute(cmd.X, cmd.Y);
                    if (cmd.Buttons != 0)
                    {
                        WithCursorParked(_excludeHwnd, () =>
                        {
                            mouse.PutMouseEventAbsolute(ax, ay, 0, 0, 0);
                            Thread.Sleep(30);
                            mouse.PutMouseEventAbsolute(ax, ay, 0, 0, cmd.Buttons);
                            Thread.Sleep(40);
                            mouse.PutMouseEventAbsolute(ax, ay, 0, 0, 0);
                        });
                    }
                    else if (cmd.WheelNotches != 0)
                    {
                        mouse.PutMouseEventAbsolute(ax, ay, cmd.WheelNotches, 0, 0);
                    }

                    _lastFault = null;
                }
                catch (Exception ex)
                {
                    ReportFault($"VM click failed: {ex.Message}");
                    mouse = null;
                    Release(ref session, ref vbox);
                }
            }
        }
        finally
        {
            Release(ref session, ref vbox);
        }
    }

    [SupportedOSPlatform("windows")]
    private static dynamic? TryOpenMouse(ref dynamic? vbox, ref dynamic? session)
    {
        var vboxType = Type.GetTypeFromProgID("VirtualBox.VirtualBox");
        var sessionType = Type.GetTypeFromProgID("VirtualBox.Session");
        if (vboxType is null || sessionType is null)
        {
            ReportFault("VM click failed: VirtualBox COM is not registered.");
            return null;
        }

        vbox = Activator.CreateInstance(vboxType);
        session = Activator.CreateInstance(sessionType);
        if (vbox is null || session is null)
        {
            ReportFault("VM click failed: could not create the VirtualBox session.");
            return null;
        }

        dynamic machine = vbox.FindMachine(_vmName);
        machine.LockMachine(session, VictoriaVmPointerPlan.LockTypeShared);
        dynamic console = session.Console
            ?? throw new InvalidOperationException("VirtualBox session has no console.");
        dynamic mouse = console.Mouse
            ?? throw new InvalidOperationException("VirtualBox console has no mouse.");
        return mouse;
    }

    private static void Release(ref dynamic? session, ref dynamic? vbox)
    {
        if (session is not null)
        {
            try { session.UnlockMachine(); }
            catch { /* already unlocked */ }

            try { Marshal.FinalReleaseComObject(session); }
            catch { /* ignore */ }
            session = null;
        }

        if (vbox is not null)
        {
            try { Marshal.FinalReleaseComObject(vbox); }
            catch { /* ignore */ }
            vbox = null;
        }
    }

    private static void ReportFault(string message)
    {
        if (string.Equals(_lastFault, message, StringComparison.Ordinal))
            return;
        _lastFault = message;
        PresenceStartupLog.Write(message);
        try { Faulted?.Invoke(message); }
        catch { /* UI hook must not kill the STA thread */ }
    }

    private static void WithCursorParked(nint excludeHwnd, Action body)
    {
        if (!OperatingSystem.IsWindows() || !GetCursorPos(out var saved))
        {
            body();
            return;
        }

        var root = excludeHwnd == 0 ? 0 : GetAncestor(excludeHwnd, GaRoot);
        if (root == 0)
            root = excludeHwnd;
        RECT rc = default;
        var haveRect = root != 0 && GetWindowRect(root, out rc);
        if (haveRect)
        {
            var midY = (rc.Top + rc.Bottom) / 2;
            TryPark(rc.Right + 24, midY, rc);
            TryPark(rc.Left - 24, midY, rc);
        }

        try
        {
            Thread.Sleep(40);
            body();
            Thread.Sleep(20);
        }
        finally
        {
            _ = SetCursorPos(saved.X, saved.Y);
        }
    }

    private static void TryPark(int x, int y, RECT exclude)
    {
        _ = SetCursorPos(x, y);
        Thread.Sleep(15);
        if (GetCursorPos(out var now) && !Contains(exclude, now.X, now.Y))
            return;
    }

    private static bool Contains(RECT rc, int x, int y) =>
        x >= rc.Left && x < rc.Right && y >= rc.Top && y < rc.Bottom;

    private readonly record struct PointerCmd(int X, int Y, int Buttons, int WheelNotches);

    private const uint GaRoot = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint hWnd, uint gaFlags);
}
