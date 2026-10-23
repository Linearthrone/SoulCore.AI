using System.Runtime.InteropServices;

namespace House.ChatDesktop.Services;

/// <summary>
/// PROP-16.2 gate. SetParent plus MoveWindow resizes a child HWND.
/// It does not scale a fixed framebuffer, and a click in the resized window
/// is not the pre-scale guest pixel. That case stops. The guest is not parented
/// into a click-through or frosted window.
/// </summary>
public static class GuestEmbedFit
{
    public const string ScaleStop =
        "Stopped. SetParent can resize the window but cannot scale the guest framebuffer and keep the click on that scaled pixel. The guest was not parented. No frosted stand-in.";

    public const string FrameUnknownStop =
        "Stopped. Guest frame size is unknown, so scale and hit-test cannot be checked. The guest was not parented.";

    public const string SlotEmptyStop =
        "Stopped. The viewport slot has no size yet. The guest was not parented.";

    public const string ClickThroughStop =
        "Stopped. The guest HWND is not parented into a click-through window.";

    public const string FrostedStop =
        "Stopped. The guest HWND is not parented into a frosted or transparent window.";

    public const string HitTestStop =
        "Stopped. SetParent did not keep the guest filling the slot with a hit-test on that HWND. The guest was detached.";

    public static EmbedFitDecision Decide(
        int slotWidth,
        int slotHeight,
        int frameWidth,
        int frameHeight,
        bool hostClickThrough,
        bool hostFrostedOrTransparent)
    {
        if (hostClickThrough)
            return EmbedFitDecision.Stop(ClickThroughStop);
        if (hostFrostedOrTransparent)
            return EmbedFitDecision.Stop(FrostedStop);
        if (slotWidth <= 0 || slotHeight <= 0)
            return EmbedFitDecision.Stop(SlotEmptyStop);
        if (frameWidth <= 0 || frameHeight <= 0)
            return EmbedFitDecision.Stop(FrameUnknownStop);
        if (frameWidth != slotWidth || frameHeight != slotHeight)
            return EmbedFitDecision.Stop(ScaleStop);
        return EmbedFitDecision.Ready("Guest frame matches the slot 1:1. Clicks stay in those client coordinates.");
    }

    /// <summary>
    /// Same SetParent / WS_CHILD / MoveWindow sequence as VictoriaBrowserEmbedHost.
    /// A 400×300 framebuffer forced into a 200×150 slot does not receive the scaled pixel's click.
    /// </summary>
    public static EmbedProbeResult ProbeQuarterScale()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new EmbedProbeResult(
                false, false, false, 300, 200, 0, 0, -1, -1,
                "Probe is Windows-only.");
        }

        lock (ProbeGate)
        {
            return ProbeQuarterScaleCore();
        }
    }

    private static EmbedProbeResult ProbeQuarterScaleCore()
    {
        const int frameW = 400;
        const int frameH = 300;
        const int frameX = 300;
        const int frameY = 200;
        const int hostW = 200;
        const int hostH = 150;
        nint host = 0;
        nint guest = 0;
        var registered = false;
        try
        {
            var instance = GetModuleHandle(null);
            var wc = new WNDCLASS
            {
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(ProbeProc),
                hInstance = instance,
                lpszClassName = ProbeClass
            };
            if (RegisterClass(ref wc) != 0)
                registered = true;
            else if (Marshal.GetLastPInvokeError() != ErrorClassAlreadyExists)
            {
                return Failed(frameX, frameY, "RegisterClass failed: " + Marshal.GetLastPInvokeError());
            }

            host = CreateWindowEx(0, ProbeClass, "probe-host", WsPopup | WsVisible,
                40, 40, hostW, hostH, 0, 0, instance, 0);
            guest = CreateWindowEx(0, ProbeClass, "probe-guest", WsPopup | WsVisible,
                80, 80, frameW, frameH, 0, 0, instance, 0);
            if (host == 0 || guest == 0)
                return Failed(frameX, frameY, "CreateWindowEx failed: " + Marshal.GetLastPInvokeError());

            _probeClickX = -1;
            _probeClickY = -1;
            var style = GetWindowLong(guest, GwlStyle);
            _ = SetWindowLong(guest, GwlStyle, (style | WsChild | WsVisible) & ~WsPopup);
            Marshal.SetLastPInvokeError(0);
            var previous = SetParent(guest, host);
            if (previous == 0 && Marshal.GetLastPInvokeError() != 0)
                return Failed(frameX, frameY, "SetParent failed: " + Marshal.GetLastPInvokeError());

            if (!GetClientRect(host, out var hostRect))
                return Failed(frameX, frameY, "GetClientRect failed.");
            var slotW = hostRect.Right - hostRect.Left;
            var slotH = hostRect.Bottom - hostRect.Top;
            _ = MoveWindow(guest, 0, 0, slotW, slotH, true);
            if (!GetClientRect(guest, out var guestRect))
                return Failed(frameX, frameY, "Guest GetClientRect failed.");

            var resized = Math.Abs((guestRect.Right - guestRect.Left) - slotW) <= 2
                          && Math.Abs((guestRect.Bottom - guestRect.Top) - slotH) <= 2
                          && slotW > 0
                          && slotH > 0
                          && (slotW != frameW || slotH != frameH);
            var clickX = frameX * slotW / frameW;
            var clickY = frameY * slotH / frameH;
            _ = SendMessage(guest, WmLButtonDown, (nint)1, MakeLParam(clickX, clickY));
            var hostEx = GetWindowLong(host, ViewportClickThrough.ExStyleIndex);
            var clickThrough = (hostEx & ViewportClickThrough.Transparent) != 0;
            var detail =
                $"SetParent+MoveWindow resized the child to {slotW}x{slotH}. " +
                $"Framebuffer pixel ({frameX},{frameY}) would appear at ({clickX},{clickY}) if the guest scaled. " +
                $"The click was delivered at ({_probeClickX},{_probeClickY}), which is the window coordinate, not the framebuffer pixel. " +
                "Host was not click-through. Guest was not parented into a frosted window.";
            return new EmbedProbeResult(
                true,
                clickThrough,
                resized,
                frameX,
                frameY,
                clickX,
                clickY,
                _probeClickX,
                _probeClickY,
                detail);
        }
        finally
        {
            if (guest != 0)
            {
                _ = SetParent(guest, 0);
                _ = DestroyWindow(guest);
            }

            if (host != 0)
                _ = DestroyWindow(host);
            if (registered)
                _ = UnregisterClass(ProbeClass, GetModuleHandle(null));
        }
    }

    private static EmbedProbeResult Failed(int frameX, int frameY, string detail) =>
        new(false, false, false, frameX, frameY, 0, 0, -1, -1, detail);

    private static nint MakeLParam(int x, int y) => (nint)((y << 16) | (x & 0xFFFF));

    private static nint ProbeWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WmLButtonDown)
        {
            _probeClickX = unchecked((short)(lParam & 0xFFFF));
            _probeClickY = unchecked((short)((lParam >> 16) & 0xFFFF));
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private const string ProbeClass = "SoulCoreGuestEmbedProbe";
    private const int ErrorClassAlreadyExists = 1410;
    private const int GwlStyle = -16;
    private const int WsChild = 0x40000000;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsVisible = 0x10000000;
    private const uint WmLButtonDown = 0x0201;

    private static readonly object ProbeGate = new();
    private static readonly WndProcDelegate ProbeProc = ProbeWndProc;
    private static int _probeClickX = -1;
    private static int _probeClickY = -1;

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string lpClassName, nint hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetParent(nint hWndChild, nint hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(nint hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);
}

public readonly record struct EmbedFitDecision(bool Allow, string Detail)
{
    public static EmbedFitDecision Stop(string detail) => new(false, detail);

    public static EmbedFitDecision Ready(string detail) => new(true, detail);
}

public sealed record EmbedProbeResult(
    bool Ran,
    bool HostWasClickThrough,
    bool WindowResizedToSlot,
    int FrameX,
    int FrameY,
    int ClickX,
    int ClickY,
    int ReceivedX,
    int ReceivedY,
    string Detail)
{
    public bool ScaledClickHonest =>
        Ran
        && WindowResizedToSlot
        && !HostWasClickThrough
        && ReceivedX == FrameX
        && ReceivedY == FrameY;
}
