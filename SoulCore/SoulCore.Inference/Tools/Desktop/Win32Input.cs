using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>Win32 SendInput helpers for click / type / key (Windows-only).</summary>
[SupportedOSPlatform("windows")]
internal static class Win32Input
{
    internal static void Click(int x, int y, string button)
    {
        SetCursorPos(x, y);
        uint down;
        uint up;
        switch (button)
        {
            case "right":
                down = MOUSEEVENTF_RIGHTDOWN;
                up = MOUSEEVENTF_RIGHTUP;
                break;
            case "middle":
                down = MOUSEEVENTF_MIDDLEDOWN;
                up = MOUSEEVENTF_MIDDLEUP;
                break;
            default:
                down = MOUSEEVENTF_LEFTDOWN;
                up = MOUSEEVENTF_LEFTUP;
                break;
        }

        var inputs = new INPUT[2];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].U.mi = new MOUSEINPUT { dwFlags = down };
        inputs[1].type = INPUT_MOUSE;
        inputs[1].U.mi = new MOUSEINPUT { dwFlags = up };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
            throw new InvalidOperationException($"SendInput click sent {sent}/{inputs.Length}");
    }

    internal static void TypeText(string text)
    {
        foreach (var ch in text)
        {
            if (ch == '\r')
                continue;
            if (ch == '\n')
            {
                if (!TryPressKey("Enter", out var reason))
                    throw new InvalidOperationException(reason);
                continue;
            }

            var inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = ch,
                dwFlags = KEYEVENTF_UNICODE,
            };
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = ch,
                dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
            };
            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            if (sent != inputs.Length)
                throw new InvalidOperationException($"SendInput type sent {sent}/{inputs.Length}");
        }
    }

    internal static bool TryPressKey(string key, out string? reason)
    {
        if (!TryMapVirtualKey(key, out var vk))
        {
            reason = $"unsupported key '{key}' (try Enter, Escape, Tab, Space, Backspace, Delete, Left/Right/Up/Down, Home, End, F1-F12)";
            return false;
        }

        var inputs = new INPUT[2];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].U.ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 };
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].U.ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            reason = $"SendInput key sent {sent}/{inputs.Length}";
            return false;
        }

        reason = null;
        return true;
    }

    private static bool TryMapVirtualKey(string key, out ushort vk)
    {
        vk = 0;
        var k = key.Trim();
        if (k.Length == 1)
        {
            var c = char.ToUpperInvariant(k[0]);
            if (c is >= 'A' and <= 'Z')
            {
                vk = c;
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                vk = c;
                return true;
            }
        }

        switch (k.ToLowerInvariant())
        {
            case "enter":
            case "return":
                vk = VK_RETURN;
                return true;
            case "escape":
            case "esc":
                vk = VK_ESCAPE;
                return true;
            case "tab":
                vk = VK_TAB;
                return true;
            case "space":
            case "spacebar":
                vk = VK_SPACE;
                return true;
            case "backspace":
            case "back":
                vk = VK_BACK;
                return true;
            case "delete":
            case "del":
                vk = VK_DELETE;
                return true;
            case "left":
                vk = VK_LEFT;
                return true;
            case "right":
                vk = VK_RIGHT;
                return true;
            case "up":
                vk = VK_UP;
                return true;
            case "down":
                vk = VK_DOWN;
                return true;
            case "home":
                vk = VK_HOME;
                return true;
            case "end":
                vk = VK_END;
                return true;
            case "f1": vk = VK_F1; return true;
            case "f2": vk = VK_F2; return true;
            case "f3": vk = VK_F3; return true;
            case "f4": vk = VK_F4; return true;
            case "f5": vk = VK_F5; return true;
            case "f6": vk = VK_F6; return true;
            case "f7": vk = VK_F7; return true;
            case "f8": vk = VK_F8; return true;
            case "f9": vk = VK_F9; return true;
            case "f10": vk = VK_F10; return true;
            case "f11": vk = VK_F11; return true;
            case "f12": vk = VK_F12; return true;
            default:
                return false;
        }
    }

    private const int INPUT_MOUSE = 0;
    private const int INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    private const ushort VK_BACK = 0x08;
    private const ushort VK_TAB = 0x09;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_ESCAPE = 0x1B;
    private const ushort VK_SPACE = 0x20;
    private const ushort VK_END = 0x23;
    private const ushort VK_HOME = 0x24;
    private const ushort VK_LEFT = 0x25;
    private const ushort VK_UP = 0x26;
    private const ushort VK_RIGHT = 0x27;
    private const ushort VK_DOWN = 0x28;
    private const ushort VK_DELETE = 0x2E;
    private const ushort VK_F1 = 0x70;
    private const ushort VK_F2 = 0x71;
    private const ushort VK_F3 = 0x72;
    private const ushort VK_F4 = 0x73;
    private const ushort VK_F5 = 0x74;
    private const ushort VK_F6 = 0x75;
    private const ushort VK_F7 = 0x76;
    private const ushort VK_F8 = 0x77;
    private const ushort VK_F9 = 0x78;
    private const ushort VK_F10 = 0x79;
    private const ushort VK_F11 = 0x7A;
    private const ushort VK_F12 = 0x7B;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
