namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// PS/2 scancodes for <c>VBoxManage controlvm keyboardputscancode</c>.
/// xdotool injects into Xwayland; the Ubuntu guest session is Wayland, so
/// Firefox never receives those keys even when xdotool exits 0.
/// </summary>
public static class VboxConsoleKeyboard
{
    private static readonly string[] ModifierRelease =
    {
        "aa", // left shift
        "b6", // right shift
        "9d", // left ctrl
        "b8", // left alt
        "e0", "db", // left super
    };

    private static readonly Dictionary<char, (byte Code, bool Shift)> Chars = BuildChars();

    public static bool TryPlanText(string text, out string[] scancodes, out string error)
    {
        scancodes = Array.Empty<string>();
        error = "";
        if (string.IsNullOrEmpty(text))
        {
            error = "text must be non-empty";
            return false;
        }

        var codes = new List<string>(text.Length * 4 + ModifierRelease.Length);
        codes.AddRange(ModifierRelease);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                AppendTap(codes, 0x1c, extended: false);
                continue;
            }

            if (ch == '\n')
            {
                AppendTap(codes, 0x1c, extended: false);
                continue;
            }

            if (ch == '\t')
            {
                AppendTap(codes, 0x0f, extended: false);
                continue;
            }

            if (!Chars.TryGetValue(ch, out var mapped))
            {
                error = $"no PS/2 scancode for U+{(int)ch:X4}";
                return false;
            }

            if (mapped.Shift)
                AppendMake(codes, 0x2a, extended: false);
            AppendTap(codes, mapped.Code, extended: false);
            if (mapped.Shift)
                AppendBreak(codes, 0x2a, extended: false);
        }

        scancodes = codes.ToArray();
        return true;
    }

    public static bool TryPlanKey(string? key, out string[] scancodes, out string error)
    {
        scancodes = Array.Empty<string>();
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

        var modifiers = new List<(byte Code, bool Extended)>();
        (byte Code, bool Extended)? main = null;
        foreach (var raw in parts)
        {
            if (!TryMapPart(raw, out var mapped, out var isModifier, out error))
                return false;
            if (isModifier)
            {
                modifiers.Add(mapped);
                continue;
            }

            if (main is not null)
            {
                error = $"unsupported key '{key}'";
                return false;
            }

            main = mapped;
        }

        var codes = new List<string>(ModifierRelease.Length + (modifiers.Count + 1) * 4);
        codes.AddRange(ModifierRelease);
        foreach (var mod in modifiers)
            AppendMake(codes, mod.Code, mod.Extended);
        if (main is { } tap)
            AppendTap(codes, tap.Code, tap.Extended);
        for (var i = modifiers.Count - 1; i >= 0; i--)
            AppendBreak(codes, modifiers[i].Code, modifiers[i].Extended);

        scancodes = codes.ToArray();
        return true;
    }

    private static bool TryMapPart(
        string raw, out (byte Code, bool Extended) mapped, out bool isModifier, out string error)
    {
        mapped = default;
        isModifier = false;
        error = "";
        var p = raw.Trim();
        switch (p.ToLowerInvariant())
        {
            case "ctrl" or "control":
                mapped = (0x1d, false);
                isModifier = true;
                return true;
            case "alt" or "menu":
                mapped = (0x38, false);
                isModifier = true;
                return true;
            case "shift":
                mapped = (0x2a, false);
                isModifier = true;
                return true;
            case "super" or "meta" or "win" or "cmd":
                mapped = (0x5b, true);
                isModifier = true;
                return true;
            case "enter" or "return":
                mapped = (0x1c, false);
                return true;
            case "esc" or "escape":
                mapped = (0x01, false);
                return true;
            case "tab":
                mapped = (0x0f, false);
                return true;
            case "space" or "spc":
                mapped = (0x39, false);
                return true;
            case "backspace" or "bksp":
                mapped = (0x0e, false);
                return true;
            case "delete" or "del":
                mapped = (0x53, true);
                return true;
            case "home":
                mapped = (0x47, true);
                return true;
            case "end":
                mapped = (0x4f, true);
                return true;
            case "pageup" or "pgup":
                mapped = (0x49, true);
                return true;
            case "pagedown" or "pgdn":
                mapped = (0x51, true);
                return true;
            case "up" or "arrowup":
                mapped = (0x48, true);
                return true;
            case "down" or "arrowdown":
                mapped = (0x50, true);
                return true;
            case "left" or "arrowleft":
                mapped = (0x4b, true);
                return true;
            case "right" or "arrowright":
                mapped = (0x4d, true);
                return true;
        }

        if (p.Length == 2
            && (p[0] == 'f' || p[0] == 'F')
            && int.TryParse(p[1..], out var fn)
            && fn is >= 1 and <= 12)
        {
            mapped = (fn switch
            {
                <= 10 => (byte)(0x3a + fn),
                11 => (byte)0x57,
                _ => (byte)0x58,
            }, false);
            return true;
        }

        if (p.Length == 1 && Chars.TryGetValue(char.ToLowerInvariant(p[0]), out var ch) && !ch.Shift)
        {
            mapped = (ch.Code, false);
            return true;
        }

        error = $"unsupported key '{raw}'";
        return false;
    }

    private static void AppendTap(List<string> codes, byte make, bool extended)
    {
        AppendMake(codes, make, extended);
        AppendBreak(codes, make, extended);
    }

    private static void AppendMake(List<string> codes, byte make, bool extended)
    {
        if (extended)
            codes.Add("e0");
        codes.Add(make.ToString("x2"));
    }

    private static void AppendBreak(List<string> codes, byte make, bool extended)
    {
        if (extended)
            codes.Add("e0");
        codes.Add(((byte)(make | 0x80)).ToString("x2"));
    }

    private static Dictionary<char, (byte Code, bool Shift)> BuildChars()
    {
        var map = new Dictionary<char, (byte Code, bool Shift)>();
        void Add(char ch, byte code, bool shift) => map[ch] = (code, shift);

        Add(' ', 0x39, false);
        Add('1', 0x02, false);
        Add('!', 0x02, true);
        Add('2', 0x03, false);
        Add('@', 0x03, true);
        Add('3', 0x04, false);
        Add('#', 0x04, true);
        Add('4', 0x05, false);
        Add('$', 0x05, true);
        Add('5', 0x06, false);
        Add('%', 0x06, true);
        Add('6', 0x07, false);
        Add('^', 0x07, true);
        Add('7', 0x08, false);
        Add('&', 0x08, true);
        Add('8', 0x09, false);
        Add('*', 0x09, true);
        Add('9', 0x0a, false);
        Add('(', 0x0a, true);
        Add('0', 0x0b, false);
        Add(')', 0x0b, true);
        Add('-', 0x0c, false);
        Add('_', 0x0c, true);
        Add('=', 0x0d, false);
        Add('+', 0x0d, true);
        Add('[', 0x1a, false);
        Add('{', 0x1a, true);
        Add(']', 0x1b, false);
        Add('}', 0x1b, true);
        Add('\\', 0x2b, false);
        Add('|', 0x2b, true);
        Add(';', 0x27, false);
        Add(':', 0x27, true);
        Add('\'', 0x28, false);
        Add('"', 0x28, true);
        Add('`', 0x29, false);
        Add('~', 0x29, true);
        Add(',', 0x33, false);
        Add('<', 0x33, true);
        Add('.', 0x34, false);
        Add('>', 0x34, true);
        Add('/', 0x35, false);
        Add('?', 0x35, true);

        const string letters = "qwertyuiopasdfghjklzxcvbnm";
        ReadOnlySpan<byte> letterCodes =
        [
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19,
            0x1e, 0x1f, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26,
            0x2c, 0x2d, 0x2e, 0x2f, 0x30, 0x31, 0x32,
        ];
        for (var i = 0; i < letters.Length; i++)
        {
            Add(letters[i], letterCodes[i], false);
            Add(char.ToUpperInvariant(letters[i]), letterCodes[i], true);
        }

        return map;
    }
}
