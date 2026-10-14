using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace House.ChatDesktop.Services;

/// <summary>
/// Injects operator pointer and keyboard events into victoria-sandbox through
/// VirtualBox COM. The reparented VirtualBox window receives Win32 input and
/// does not forward it into the guest; <c>IMouse.putMouseEventAbsolute</c> and
/// <c>IKeyboard.putScancode</c> do. A wheel is a Page Up or Page Down tap on
/// that keyboard path, not a mouse <c>dz</c>. The shared console lock stays on
/// this STA thread and is reopened when it is no longer a live Running console.
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
    private static int? _lockedSessionPid;

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

        if (wheelNotches != 0)
        {
            Enqueue(new WheelKeyWork(guestX, guestY, wheelNotches, VictoriaVmKeyPlan.WheelTap(wheelNotches)));
            return;
        }

        Enqueue(new PointerWork(guestX, guestY, buttons));
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

                if (cmd is KeyWork keys)
                {
                    try
                    {
                        if (keyboard is null)
                        {
                            DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
                            TryOpenConsole(ref vbox, ref session, ref mouse, ref keyboard);
                        }

                        if (keyboard is null)
                            continue;
                        SendScancodes(keyboard, keys.Codes);
                        _lastFault = null;
                    }
                    catch (Exception ex)
                    {
                        ReportFault($"VM key failed: {ex.Message}");
                        DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
                    }

                    continue;
                }

                if (cmd is WheelKeyWork wheel)
                {
                    DeliverWheelKeys(ref vbox, ref session, ref mouse, ref keyboard, wheel);
                    continue;
                }

                if (cmd is PointerWork pointer)
                    DeliverPointer(ref vbox, ref session, ref mouse, ref keyboard, pointer);
            }
        }
        finally
        {
            DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void DeliverPointer(
        ref dynamic? vbox,
        ref dynamic? session,
        ref dynamic? mouse,
        ref dynamic? keyboard,
        PointerWork pointer)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                if (attempt > 0)
                    DropConsole(ref vbox, ref session, ref mouse, ref keyboard);

                EnsureLiveConsole(ref vbox, ref session, ref mouse, ref keyboard);
                if (mouse is null)
                    throw new InvalidOperationException("VirtualBox console has no mouse.");

                InjectPointer(mouse, pointer);
                _lastFault = null;
                return;
            }
            catch (Exception ex)
            {
                ReportFault($"VM click failed: {ex.Message}");
                DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static void InjectPointer(dynamic mouse, PointerWork pointer)
    {
        if (pointer.Buttons == 0)
            return;

        var (ax, ay) = VictoriaVmPointerPlan.ToAbsolute(pointer.X, pointer.Y);
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

    /// <summary>
    /// One wheel detent is one Page Up or Page Down tap through
    /// <see cref="SendScancodes"/>, the same <c>IKeyboard.putScancode</c> path
    /// as <see cref="PostKeys"/>. The cursor is not seated or parked.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void DeliverWheelKeys(
        ref dynamic? vbox,
        ref dynamic? session,
        ref dynamic? mouse,
        ref dynamic? keyboard,
        WheelKeyWork wheel)
    {
        Exception? failure = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                if (attempt > 0)
                    DropConsole(ref vbox, ref session, ref mouse, ref keyboard);

                if (keyboard is null)
                {
                    DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
                    TryOpenConsole(ref vbox, ref session, ref mouse, ref keyboard);
                }

                if (keyboard is null)
                    throw new InvalidOperationException("VirtualBox console has no keyboard.");

                SendScancodes(keyboard, wheel.Codes);
                PresenceStartupLog.Write(
                    VictoriaVmPointerPlan.FormatWheelStartupLine(
                        wheel.Notches,
                        (wheel.X, wheel.Y),
                        null));
                _lastFault = null;
                return;
            }
            catch (Exception ex)
            {
                failure = ex;
                ReportFault($"VM key failed: {ex.Message}");
                DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
            }
        }

        PresenceStartupLog.Write(
            VictoriaVmPointerPlan.FormatWheelStartupLine(
                wheel.Notches,
                (wheel.X, wheel.Y),
                failure?.Message ?? "putScancode failed"));
    }

    [SupportedOSPlatform("windows")]
    private static void EnsureLiveConsole(
        ref dynamic? vbox, ref dynamic? session, ref dynamic? mouse, ref dynamic? keyboard)
    {
        if (session is not null && mouse is not null)
        {
            var view = ReadLockView(session);
            if (!VictoriaVmPointerPlan.NeedsConsoleReconnect(
                    view.MachineState,
                    view.SessionLocked,
                    view.ConsolePresent,
                    view.MousePresent,
                    _lockedSessionPid,
                    view.SessionPid))
            {
                if (view.Mouse is not null)
                    mouse = view.Mouse;
                if (view.Keyboard is not null)
                    keyboard = view.Keyboard;
                if (_lockedSessionPid is null && view.SessionPid is > 0)
                    _lockedSessionPid = view.SessionPid;
                return;
            }
        }

        DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
        TryOpenConsole(ref vbox, ref session, ref mouse, ref keyboard);
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
            DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
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
        _lockedSessionPid = TryReadSessionPid(machine);
    }

    private struct LockView
    {
        public int? MachineState;
        public bool SessionLocked;
        public bool ConsolePresent;
        public bool MousePresent;
        public int? SessionPid;
        public dynamic? Mouse;
        public dynamic? Keyboard;
    }

    [SupportedOSPlatform("windows")]
    private static LockView ReadLockView(dynamic session)
    {
        var view = new LockView
        {
            SessionLocked = TryReadSessionState(session) == VictoriaVmPointerPlan.SessionStateLocked
        };

        try
        {
            dynamic machine = session.Machine;
            if (machine is not null)
            {
                view.MachineState = TryReadMachineState(machine);
                view.SessionPid = TryReadSessionPid(machine);
            }
        }
        catch
        {
            view.MachineState = null;
            view.SessionPid = null;
        }

        if (!view.SessionLocked)
            return view;

        try
        {
            dynamic console = session.Console;
            if (console is null)
                return view;

            view.ConsolePresent = true;
            try
            {
                dynamic liveMouse = console.Mouse;
                if (liveMouse is not null)
                {
                    view.Mouse = liveMouse;
                    view.MousePresent = true;
                }
            }
            catch
            {
                view.Mouse = null;
                view.MousePresent = false;
            }

            try
            {
                dynamic liveKeyboard = console.Keyboard;
                if (liveKeyboard is not null)
                    view.Keyboard = liveKeyboard;
            }
            catch
            {
                view.Keyboard = null;
            }
        }
        catch
        {
            view.ConsolePresent = false;
            view.MousePresent = false;
            view.Mouse = null;
            view.Keyboard = null;
        }

        return view;
    }

    private static int? TryReadSessionState(dynamic session)
    {
        try
        {
            object raw = session.State;
            return ReadComInt(raw);
        }
        catch
        {
            try
            {
                object raw = session.state;
                return ReadComInt(raw);
            }
            catch
            {
                return null;
            }
        }
    }

    private static int? TryReadMachineState(dynamic machine)
    {
        try
        {
            object raw = machine.State;
            return ReadComInt(raw);
        }
        catch
        {
            try
            {
                object raw = machine.state;
                return ReadComInt(raw);
            }
            catch
            {
                return null;
            }
        }
    }

    private static int? TryReadSessionPid(dynamic machine)
    {
        try
        {
            object raw = machine.SessionPID;
            return ReadComInt(raw);
        }
        catch
        {
            try
            {
                object raw = machine.sessionPID;
                return ReadComInt(raw);
            }
            catch
            {
                return null;
            }
        }
    }

    private static int? ReadComInt(object? value)
    {
        if (value is null)
            return null;
        try
        {
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            string? text;
            try
            {
                text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(text))
                return null;
            if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            if (string.Equals(text, "Running", StringComparison.OrdinalIgnoreCase))
                return VictoriaVmPointerPlan.MachineStateRunning;
            if (string.Equals(text, "Locked", StringComparison.OrdinalIgnoreCase))
                return VictoriaVmPointerPlan.SessionStateLocked;
            return null;
        }
    }

    private static void DropConsole(
        ref dynamic? vbox, ref dynamic? session, ref dynamic? mouse, ref dynamic? keyboard)
    {
        _lockedSessionPid = null;
        mouse = null;
        keyboard = null;
        Release(ref session, ref vbox);
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

    private sealed record PointerWork(int X, int Y, int Buttons) : WorkItem;

    private sealed record KeyWork(byte[] Codes) : WorkItem;

    private sealed record WheelKeyWork(int X, int Y, int Notches, byte[] Codes) : WorkItem;

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
