namespace House.ChatDesktop.Services;

/// <summary>
/// Turns a Win32 key message into the PS/2 scancodes VirtualBox
/// <c>IKeyboard.putScancode</c> expects. Make on key-down (including
/// auto-repeat), break on key-up, with an E0 prefix when the extended bit is set.
/// </summary>
public static class VictoriaVmKeyPlan
{
    public const uint WmKeyDown = 0x0100;
    public const uint WmKeyUp = 0x0101;
    public const uint WmSysKeyDown = 0x0104;
    public const uint WmSysKeyUp = 0x0105;

    public static bool TryFromKeyMessage(
        uint msg,
        long lParamBits,
        out byte[] scancodes,
        out int holdId,
        out bool isUp)
    {
        scancodes = Array.Empty<byte>();
        holdId = 0;
        isUp = msg is WmKeyUp or WmSysKeyUp;
        if (msg is not (WmKeyDown or WmKeyUp or WmSysKeyDown or WmSysKeyUp))
            return false;

        var scan = (int)((lParamBits >> 16) & 0xFF);
        if (scan == 0)
            return false;

        var extended = (lParamBits & (1L << 24)) != 0;
        var make = (byte)scan;
        var code = isUp ? (byte)(make | 0x80) : make;
        scancodes = extended ? new byte[] { 0xE0, code } : new byte[] { code };
        holdId = make | (extended ? 0x100 : 0);
        return true;
    }

    public static byte[] Break(int holdId)
    {
        var extended = (holdId & 0x100) != 0;
        var make = (byte)(holdId & 0x7F);
        var code = (byte)(make | 0x80);
        return extended ? new byte[] { 0xE0, code } : new byte[] { code };
    }

    /// <summary>
    /// One Her-screen wheel detent is one Page Up or Page Down tap: make then
    /// break. Positive notches (wheel away, content up) are Page Up
    /// <c>E0 49</c> / <c>E0 C9</c>. Negative notches are Page Down
    /// <c>E0 51</c> / <c>E0 D1</c>. Several notches are several taps, not a held key.
    /// </summary>
    public static byte[] WheelTap(int notches)
    {
        if (notches == 0)
            return Array.Empty<byte>();

        var one = notches > 0
            ? new byte[] { 0xE0, 0x49, 0xE0, 0xC9 }
            : new byte[] { 0xE0, 0x51, 0xE0, 0xD1 };
        var count = Math.Abs(notches);
        if (count == 1)
            return one;

        var all = new byte[one.Length * count];
        for (var i = 0; i < count; i++)
            one.CopyTo(all, i * one.Length);
        return all;
    }

    /// <summary>Startup-log key name for a wheel. Empty when there is no detent.</summary>
    public static string WheelKeyName(int notches) =>
        notches > 0 ? "pageup" : notches < 0 ? "pagedown" : "";
}
