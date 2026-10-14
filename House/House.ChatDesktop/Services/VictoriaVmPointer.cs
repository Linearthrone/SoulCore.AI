using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace House.ChatDesktop.Services;

/// <summary>
/// Injects operator pointer and keyboard events into victoria-sandbox through
/// VirtualBox COM. The reparented VirtualBox window receives Win32 input and
/// does not forward it into the guest; <c>IMouse.putMouseEventAbsolute</c> and
/// <c>IKeyboard.putScancode</c> do.
/// </summary>
public static class VictoriaVmPointer
{
    private static readonly object Gate = new();
    private static readonly List<WorkItem> Pending = new();
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

        Enqueue(new PointerWork(guestX, guestY, buttons, wheelNotches));
    }

    public static void PostKeys(ReadOnlySpan<byte> scancodes)
    {
        if (!OperatingSystem.IsWindows() || scancodes.IsEmpty)
            return;

        Enqueue(new KeyWork(scancodes.ToArray()));
    }

    private static void Enqueue(WorkItem item)
    {
        lock (Gate)
        {
            Pending.Add(item);
            while (Pending.Count > 64)
            {
                var drop = Pending.FindIndex(work => work is PointerWork);
                Pending.RemoveAt(drop >= 0 ? drop : 0);
            }
        }

        Signal.Set();
    }

    [SupportedOSPlatform("windows")]
    private static void ThreadMain()
    {
        dynamic? vbox = null;
        dynamic? session = null;
        dynamic? mouse = null;
        dynamic? keyboard = null;
        try
        {
            while (true)
            {
                WorkItem? cmd = null;
                lock (Gate)
                {
                    if (_stop && Pending.Count == 0)
                        break;
                    if (Pending.Count > 0)
                    {
                        cmd = Pending[0];
                        Pending.RemoveAt(0);
                    }
                }

                if (cmd is null)
                {
                    Signal.WaitOne(200);
                    continue;
                }

                try
                {
                    if (mouse is null || keyboard is null)
                        TryOpenConsole(ref vbox, ref session, ref mouse, ref keyboard);

                    if (cmd is KeyWork keys)
                    {
                        if (keyboard is null)
                            continue;
                        SendScancodes(keyboard, keys.Codes);
                    }
                    else if (cmd is PointerWork pointer)
                    {
                        if (mouse is null)
                            continue;

                        var (ax, ay) = VictoriaVmPointerPlan.ToAbsolute(pointer.X, pointer.Y);
                        if (pointer.Buttons != 0)
                        {
                            var buttons = pointer.Buttons;
                            WithCursorParked(_excludeHwnd, () =>
                            {
                                mouse.PutMouseEventAbsolute(ax, ay, 0, 0, 0);
                                Thread.Sleep(30);
                                mouse.PutMouseEventAbsolute(ax, ay, 0, 0, buttons);
                                Thread.Sleep(40);
                                mouse.PutMouseEventAbsolute(ax, ay, 0, 0, 0);
                            });
                        }
                        else if (VictoriaVmPointerPlan.ShouldParkForWheel(pointer.WheelNotches))
                        {
                            var steps = VictoriaVmPointerPlan.WheelSteps(pointer.WheelNotches);
                            WithCursorParked(_excludeHwnd, () =>
                            {
                                for (var i = 0; i < steps.Count; i++)
                                {
                                    var step = steps[i];
                                    mouse.PutMouseEventAbsolute(ax, ay, step.Dz, step.Dw, step.ButtonState);
                                    if (i == 0 && steps.Count > 1)
                                        Thread.Sleep(30);
                                }
                            });
                        }
                    }

                    _lastFault = null;
                }
                catch (Exception ex)
                {
                    var kind = cmd is KeyWork ? "VM key failed" : "VM click failed";
                    ReportFault($"{kind}: {ex.Message}");
                    mouse = null;
                    keyboard = null;
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
    private static void SendScancodes(dynamic keyboard, byte[] codes)
    {
        foreach (var code in codes)
            keyboard.PutScancode((int)code);
    }

    [SupportedOSPlatform("windows")]
    private static void TryOpenConsole(
        ref dynamic? vbox, ref dynamic? session, ref dynamic? mouse, ref dynamic? keyboard)
    {
        var vboxType = Type.GetTypeFromProgID("VirtualBox.VirtualBox");
        var sessionType = Type.GetTypeFromProgID("VirtualBox.Session");
        if (vboxType is null || sessionType is null)
        {
            ReportFault("VM input failed: VirtualBox COM is not registered.");
            return;
        }

        vbox = Activator.CreateInstance(vboxType);
        session = Activator.CreateInstance(sessionType);
        if (vbox is null || session is null)
        {
            ReportFault("VM input failed: could not create the VirtualBox session.");
            return;
        }

        dynamic machine = vbox.FindMachine(_vmName);
        machine.LockMachine(session, VictoriaVmPointerPlan.LockTypeShared);
        dynamic console = session.Console
            ?? throw new InvalidOperationException("VirtualBox session has no console.");
        mouse = console.Mouse
            ?? throw new InvalidOperationException("VirtualBox console has no mouse.");
        keyboard = console.Keyboard
            ?? throw new InvalidOperationException("VirtualBox console has no keyboard.");
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

    private abstract record WorkItem;

    private sealed record PointerWork(int X, int Y, int Buttons, int WheelNotches) : WorkItem;

    private sealed record KeyWork(byte[] Codes) : WorkItem;

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
