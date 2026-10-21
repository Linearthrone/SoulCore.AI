using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace House.ChatDesktop.Services;

/// <summary>
/// Injects operator pointer and keyboard events into victoria-sandbox through
/// VirtualBox COM. The reparented VirtualBox window receives Win32 input and
/// does not forward it into the guest; <c>IMouse.putMouseEventAbsolute</c> and
/// <c>IKeyboard.putScancodes</c> do. A wheel is one
/// <c>putMouseEventAbsolute</c> at the real point with <c>dz</c> set to the
/// notch count and no button down. The console lock is taken for that gesture
/// and released before the next one. A failed call is not retried.
/// </summary>
public static class VictoriaVmPointer
{
    private static readonly object Gate = new();
    private static readonly List<WorkItem> Pending = new();
    private static readonly AutoResetEvent Signal = new(false);
    private static Thread? _thread;
    private static string _vmName = LocalStackControl.SandboxVmName;
    private static nint _excludeHwnd;
    private static int _guestWidth;
    private static int _guestHeight;
    private static bool _stop;
    private static string? _lastFault;
    private static int? _lockedSessionPid;

    public static event Action<string>? Faulted;

    /// <summary>Fired after a successful Absolute / key delivery clears the last fault.</summary>
    public static event Action? FaultCleared;

    public static void NoteHostWindow(nint hwnd) => _excludeHwnd = hwnd;

    /// <summary>
    /// Last known guest framebuffer size (hub / screenshot seed). Display COM may override.
    /// </summary>
    public static void NoteGuestFrame(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;
        lock (Gate)
        {
            _guestWidth = width;
            _guestHeight = height;
        }
    }

    /// <summary>
    /// Cached guest framebuffer from the last successful display read or <see cref="NoteGuestFrame"/>.
    /// </summary>
    public static bool TryGetCachedGuestFrame(out int width, out int height)
    {
        lock (Gate)
        {
            width = _guestWidth;
            height = _guestHeight;
            return width > 0 && height > 0;
        }
    }

    /// <summary>
    /// Map Her-screen HWND client pixels → guest Absolute. Display size is read from
    /// VirtualBox COM on the inject thread (never treat the slot size as the FB).
    /// </summary>
    public static void PostFromSurface(
        int surfaceX,
        int surfaceY,
        int surfaceWidth,
        int surfaceHeight,
        int buttons,
        int wheelNotches)
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (surfaceWidth <= 1 || surfaceHeight <= 1)
            return;
        if (surfaceX < 0 || surfaceY < 0)
            return;

        if (wheelNotches != 0)
        {
            Enqueue(new SurfaceWheelWork(surfaceX, surfaceY, surfaceWidth, surfaceHeight, wheelNotches));
            return;
        }

        if (buttons == 0)
            return;

        Enqueue(new SurfacePointerWork(surfaceX, surfaceY, surfaceWidth, surfaceHeight, buttons));
    }

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
            Enqueue(new WheelKeyWork(guestX, guestY, wheelNotches));
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
                        TryOpenConsole(ref vbox, ref session, ref mouse, ref keyboard);
                        if (keyboard is null)
                            continue;
                        SendScancodes(keyboard, keys.Codes);
                        _lastFault = null;
                    }
                    catch (Exception ex)
                    {
                        ReportFault($"VM key failed: {ex.Message}");
                    }
                    finally
                    {
                        DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
                    }

                    continue;
                }

                if (cmd is WheelKeyWork wheel)
                {
                    DeliverWheel(ref vbox, ref session, ref mouse, ref keyboard, wheel);
                    continue;
                }

                if (cmd is SurfaceWheelWork surfaceWheel)
                {
                    DeliverSurfaceWheel(ref vbox, ref session, ref mouse, ref keyboard, surfaceWheel);
                    continue;
                }

                if (cmd is SurfacePointerWork surfacePointer)
                {
                    DeliverSurfacePointer(ref vbox, ref session, ref mouse, ref keyboard, surfacePointer);
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
        if (pointer.Buttons == 0)
            return;

        var deliveryLogged = false;
        try
        {
            TryOpenConsole(ref vbox, ref session, ref mouse, ref keyboard);
            if (mouse is null)
            {
                PresenceStartupLog.Write(
                    VictoriaVmPointerPlan.FormatClickFailedBeforeInjectLine(
                        pointer.Buttons, "VirtualBox console has no mouse."));
                deliveryLogged = true;
                throw new InvalidOperationException("VirtualBox console has no mouse.");
            }

            InjectPointer(mouse, pointer.X, pointer.Y, pointer.Buttons, out deliveryLogged);
            ClearFault();
        }
        catch (Exception ex)
        {
            if (!deliveryLogged)
            {
                PresenceStartupLog.Write(
                    VictoriaVmPointerPlan.FormatClickDeliveryLine(
                        pointer.X, pointer.Y, pointer.Buttons,
                        parkOk: false, sent: false, comError: ex.Message));
            }

            ReportFault(VictoriaVmPointerPlan.FormatOperatorClickFault(ex.Message));
        }
        finally
        {
            DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void DeliverSurfacePointer(
        ref dynamic? vbox,
        ref dynamic? session,
        ref dynamic? mouse,
        ref dynamic? keyboard,
        SurfacePointerWork surface)
    {
        var deliveryLogged = false;
        try
        {
            TryOpenConsole(ref vbox, ref session, ref mouse, ref keyboard);
            if (mouse is null || session is null)
            {
                PresenceStartupLog.Write(
                    VictoriaVmPointerPlan.FormatClickFailedBeforeInjectLine(
                        surface.Buttons, "VirtualBox console has no mouse."));
                deliveryLogged = true;
                throw new InvalidOperationException("VirtualBox console has no mouse.");
            }

            var mapped = MapSurfaceToGuest(session, surface.X, surface.Y, surface.Width, surface.Height);
            if (mapped is null)
            {
                PresenceStartupLog.Write(
                    VictoriaVmPointerPlan.FormatClickDroppedLine(
                        surface.X, surface.Y, surface.Width, surface.Height));
                deliveryLogged = true;
                ReportFault(VictoriaVmPointerPlan.FormatOperatorClickFault(
                    "surface point unmapped to guest framebuffer."));
                return;
            }

            InjectPointer(mouse, mapped.Value.X, mapped.Value.Y, surface.Buttons, out deliveryLogged);
            ClearFault();
        }
        catch (Exception ex)
        {
            if (!deliveryLogged)
            {
                PresenceStartupLog.Write(
                    VictoriaVmPointerPlan.FormatClickFailedBeforeInjectLine(surface.Buttons, ex.Message));
            }

            ReportFault(VictoriaVmPointerPlan.FormatOperatorClickFault(ex.Message));
        }
        finally
        {
            DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void DeliverSurfaceWheel(
        ref dynamic? vbox,
        ref dynamic? session,
        ref dynamic? mouse,
        ref dynamic? keyboard,
        SurfaceWheelWork surface)
    {
        try
        {
            TryOpenConsole(ref vbox, ref session, ref mouse, ref keyboard);
            if (mouse is null || session is null)
                throw new InvalidOperationException("VirtualBox console has no mouse.");

            var mapped = MapSurfaceToGuest(session, surface.X, surface.Y, surface.Width, surface.Height);
            if (mapped is null)
            {
                PresenceStartupLog.Write(
                    VictoriaVmPointerPlan.FormatWheelStartupLine(surface.Notches, null, null));
                return;
            }

            var steps = VictoriaVmPointerPlan.WheelSteps(mapped.Value.X, mapped.Value.Y, surface.Notches);
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                mouse.PutMouseEventAbsolute(step.X, step.Y, step.Dz, step.Dw, step.Buttons);
                if (i == 0)
                    Thread.Sleep(30);
            }

            PresenceStartupLog.Write(
                VictoriaVmPointerPlan.FormatWheelDeliveryLine(
                    surface.Notches,
                    mapped.Value.X,
                    mapped.Value.Y,
                    sent: true,
                    comError: null));
            _lastFault = null;
        }
        catch (Exception ex)
        {
            ReportFault($"VM wheel failed: {ex.Message}");
            PresenceStartupLog.Write(
                VictoriaVmPointerPlan.FormatWheelDeliveryLine(
                    surface.Notches, surface.X, surface.Y, sent: false, comError: ex.Message));
        }
        finally
        {
            DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
        }
    }

    [SupportedOSPlatform("windows")]
    private static (int X, int Y)? MapSurfaceToGuest(
        dynamic session, int surfaceX, int surfaceY, int surfaceW, int surfaceH)
    {
        int hubW;
        int hubH;
        lock (Gate)
        {
            hubW = _guestWidth;
            hubH = _guestHeight;
        }

        int? displayW = null;
        int? displayH = null;
        try
        {
            dynamic console = session.Console;
            var display = TryReadDisplaySize(console);
            if (display is not null)
            {
                displayW = display.Value.Width;
                displayH = display.Value.Height;
                NoteGuestFrame(display.Value.Width, display.Value.Height);
            }
        }
        catch
        {
            // Hub / default seed below.
        }

        // PROP-19.3: same ResolveGuestFramebuffer + stretch-fill + Absolute as soft-cursor tip.
        var fb = VictoriaBrowserCoordMap.ResolveGuestFramebuffer(
            hubW, hubH, displayW, displayH, surfaceW, surfaceH);
        var inject = VictoriaBrowserCoordMap.MapSlotToInject(
            surfaceX, surfaceY, surfaceW, surfaceH, fb.Width, fb.Height);
        if (inject is not null)
        {
            PresenceStartupLog.Write(
                $"her-screen click map surface={surfaceW}x{surfaceH} pt={surfaceX},{surfaceY} " +
                $"guest={fb.Width}x{fb.Height} guest0={inject.Value.GuestX},{inject.Value.GuestY} " +
                $"abs={inject.Value.AbsX},{inject.Value.AbsY}" +
                (displayW is > 0 ? " display=ok" : " display=fallback"));
            return (inject.Value.GuestX, inject.Value.GuestY);
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static (int Width, int Height)? TryReadDisplaySize(dynamic console)
    {
        try
        {
            dynamic display = console.Display;
            if (display is null)
                return null;

            // VBox 7+: getScreenResolution(..., out GuestMonitorStatus). Older: 6 args.
            var size = TryInvokeScreenResolution(display, includeMonitorStatus: true)
                       ?? TryInvokeScreenResolution(display, includeMonitorStatus: false);
            if (size is not null)
                return size;
        }
        catch
        {
            // Caller falls back to surface / hub size.
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static (int Width, int Height)? TryInvokeScreenResolution(
        dynamic display, bool includeMonitorStatus)
    {
        try
        {
            // screenId, w, h, bpp, xOrigin, yOrigin [, guestMonitorStatus]
            var args = includeMonitorStatus
                ? new object[] { 0, 0u, 0u, 0u, 0, 0, 0 }
                : new object[] { 0, 0u, 0u, 0u, 0, 0 };
            var modifiers = new ParameterModifier(args.Length);
            for (var i = 1; i < args.Length; i++)
                modifiers[i] = true;

            display.GetType().InvokeMember(
                "GetScreenResolution",
                BindingFlags.InvokeMethod,
                binder: null,
                target: display,
                args: args,
                modifiers: new[] { modifiers },
                culture: CultureInfo.InvariantCulture,
                namedParameters: null);

            var w = Convert.ToInt32(args[1], CultureInfo.InvariantCulture);
            var h = Convert.ToInt32(args[2], CultureInfo.InvariantCulture);
            if (w > 0 && h > 0)
                return (w, h);
        }
        catch
        {
            // Try the other arity.
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static void InjectPointer(
        dynamic mouse, int guestX, int guestY, int buttons, out bool deliveryLogged)
    {
        deliveryLogged = false;
        if (buttons == 0)
            return;

        var (ax, ay) = VictoriaVmPointerPlan.ToAbsolute(guestX, guestY);
        var parkOk = false;
        try
        {
            // Park must leave the embed HWND before Absolute button edges, or Mouse
            // Integration clears the press. Do not inject (or claim "sent") on park fail.
            WithCursorParked(_excludeHwnd, () =>
            {
                mouse.PutMouseEventAbsolute(ax, ay, 0, 0, 0);
                Thread.Sleep(50);
                mouse.PutMouseEventAbsolute(ax, ay, 0, 0, buttons);
                Thread.Sleep(50);
                mouse.PutMouseEventAbsolute(ax, ay, 0, 0, 0);
            }, out parkOk);

            if (!parkOk)
            {
                PresenceStartupLog.Write(
                    VictoriaVmPointerPlan.FormatClickDeliveryLine(
                        guestX, guestY, buttons,
                        parkOk: false, sent: false,
                        comError: VictoriaVmPointerPlan.ParkFailedReason));
                deliveryLogged = true;
                throw new InvalidOperationException(
                    "park failed — host cursor still over embed HWND.");
            }

            PresenceStartupLog.Write(
                VictoriaVmPointerPlan.FormatClickDeliveryLine(
                    guestX, guestY, buttons, parkOk: true, sent: true, comError: null));
            deliveryLogged = true;
        }
        catch (Exception ex)
        {
            if (!deliveryLogged)
            {
                PresenceStartupLog.Write(
                    VictoriaVmPointerPlan.FormatClickDeliveryLine(
                        guestX, guestY, buttons, parkOk, sent: false, comError: ex.Message));
                deliveryLogged = true;
            }

            throw;
        }
    }

    /// <summary>
    /// One move to the real point, then one wheel event there. No button.
    /// The console is released when this returns. A failure is not retried.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void DeliverWheel(
        ref dynamic? vbox,
        ref dynamic? session,
        ref dynamic? mouse,
        ref dynamic? keyboard,
        WheelKeyWork wheel)
    {
        try
        {
            TryOpenConsole(ref vbox, ref session, ref mouse, ref keyboard);
            if (mouse is null)
                throw new InvalidOperationException("VirtualBox console has no mouse.");

            var steps = VictoriaVmPointerPlan.WheelSteps(wheel.X, wheel.Y, wheel.Notches);
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                mouse.PutMouseEventAbsolute(step.X, step.Y, step.Dz, step.Dw, step.Buttons);
                if (i == 0)
                    Thread.Sleep(30);
            }

            PresenceStartupLog.Write(
                VictoriaVmPointerPlan.FormatWheelDeliveryLine(
                    wheel.Notches,
                    wheel.X,
                    wheel.Y,
                    sent: true,
                    comError: null));
            _lastFault = null;
        }
        catch (Exception ex)
        {
            ReportFault($"VM wheel failed: {ex.Message}");
            PresenceStartupLog.Write(
                VictoriaVmPointerPlan.FormatWheelDeliveryLine(
                    wheel.Notches,
                    wheel.X,
                    wheel.Y,
                    sent: false,
                    comError: ex.Message));
        }
        finally
        {
            DropConsole(ref vbox, ref session, ref mouse, ref keyboard);
        }
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

    private static void ClearFault()
    {
        if (_lastFault is null)
            return;
        _lastFault = null;
        try { FaultCleared?.Invoke(); }
        catch { /* UI hook must not kill the STA thread */ }
    }

    /// <summary>
    /// Park off Mouse Integration before Absolute button changes. Exclude the embed
    /// HWND always; also exclude Presence GA_ROOT when another monitor/region exists
    /// (Host <c>HostPointerPark</c> intent — MI tracks the whole Presence client).
    /// Single-monitor maximized keeps embed-only so chrome remains parkable (#171).
    /// Empty excludes never count as parked. When park fails, body is not run.
    /// </summary>
    private static void WithCursorParked(nint excludeHwnd, Action body, out bool parkOk)
    {
        parkOk = false;
        if (!OperatingSystem.IsWindows() || !GetCursorPos(out var saved))
            return;

        var vx = GetSystemMetrics(SmXVirtualScreen);
        var vy = GetSystemMetrics(SmYVirtualScreen);
        var vw = Math.Max(1, GetSystemMetrics(SmCxVirtualScreen));
        var vh = Math.Max(1, GetSystemMetrics(SmCyVirtualScreen));
        var excludes = BuildParkExcludes(excludeHwnd, vx, vy, vw, vh);

        // Without an exclude rect we cannot prove we left MI — never auto-ok.
        if (excludes.Count == 0)
        {
            PresenceStartupLog.Write(
                "her-screen click park=failed COM error: no embed HWND exclude");
            return;
        }

        var parked = false;
        var parkX = 0;
        var parkY = 0;
        foreach (var (x, y) in VictoriaVmPointerPlan.EnumerateParkCandidates(excludes, vx, vy, vw, vh))
        {
            _ = SetCursorPos(x, y);
            Thread.Sleep(20);
            if (GetCursorPos(out var now)
                && !VictoriaVmPointerPlan.IsInsideAnyPark(now.X, now.Y, excludes))
            {
                Thread.Sleep(ParkSettleMs);
                parked = true;
                parkX = now.X;
                parkY = now.Y;
                break;
            }
        }

        if (!parked)
        {
            var fallback = VictoriaVmPointerPlan.PickParkPoint(excludes, vx, vy, vw, vh);
            _ = SetCursorPos(fallback.X, fallback.Y);
            Thread.Sleep(ParkSettleMs);
            if (GetCursorPos(out var now)
                && !VictoriaVmPointerPlan.IsInsideAnyPark(now.X, now.Y, excludes))
            {
                parked = true;
                parkX = now.X;
                parkY = now.Y;
            }
        }

        if (!parked)
        {
            _ = SetCursorPos(saved.X, saved.Y);
            PresenceStartupLog.Write(
                $"her-screen click park=failed excludes={excludes.Count} " +
                $"virtual={vw}x{vh}@{vx},{vy}");
            return;
        }

        PresenceStartupLog.Write(
            $"her-screen click park=ok at={parkX},{parkY} excludes={excludes.Count}");
        parkOk = true;
        try
        {
            body();
            // Hold park until Absolute settles — restoring onto embed immediately
            // lets Mouse Integration fight the trailing button-up.
            Thread.Sleep(ParkSettleMs);
        }
        finally
        {
            _ = SetCursorPos(saved.X, saved.Y);
        }
    }

    private static List<VictoriaVmPointerPlan.ParkRect> BuildParkExcludes(
        nint embedHwnd, int virtualX, int virtualY, int virtualW, int virtualH)
    {
        VictoriaVmPointerPlan.ParkRect? embed = null;
        VictoriaVmPointerPlan.ParkRect? root = null;

        if (embedHwnd != 0 && GetWindowRect(embedHwnd, out var embedRc))
        {
            embed = new VictoriaVmPointerPlan.ParkRect(
                embedRc.Left, embedRc.Top, embedRc.Right, embedRc.Bottom);
        }

        if (embedHwnd != 0)
        {
            var rootHwnd = GetAncestor(embedHwnd, GaRoot);
            if (rootHwnd == 0)
                rootHwnd = embedHwnd;
            if (GetWindowRect(rootHwnd, out var rootRc))
            {
                root = new VictoriaVmPointerPlan.ParkRect(
                    rootRc.Left, rootRc.Top, rootRc.Right, rootRc.Bottom);
            }
        }

        return VictoriaVmPointerPlan.BuildParkExcludePolicy(
            embed, root, virtualX, virtualY, virtualW, virtualH);
    }

    private abstract record WorkItem;

    private sealed record PointerWork(int X, int Y, int Buttons) : WorkItem;

    private sealed record SurfacePointerWork(int X, int Y, int Width, int Height, int Buttons) : WorkItem;

    private sealed record SurfaceWheelWork(int X, int Y, int Width, int Height, int Notches) : WorkItem;

    private sealed record KeyWork(byte[] Codes) : WorkItem;

    private sealed record WheelKeyWork(int X, int Y, int Notches) : WorkItem;

    private const uint GaRoot = 2;
    private const int ParkSettleMs = 80;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

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

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
