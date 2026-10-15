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
}
