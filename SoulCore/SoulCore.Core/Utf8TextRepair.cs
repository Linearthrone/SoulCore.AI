namespace SoulCore.Core;

/// <summary>
/// Undoes UTF-8 text that was decoded as Windows-1252 (<c>â€”</c> for an em dash)
/// or as the OEM code page (<c>ΓÇª</c> for an ellipsis). Leaves real Unicode alone.
/// </summary>
public static class Utf8TextRepair
{
    private static readonly char[] Cp1252High =
    {
        '\u20AC', '\u0081', '\u201A', '\u0192', '\u201E', '\u2026', '\u2020', '\u2021',
        '\u02C6', '\u2030', '\u0160', '\u2039', '\u0152', '\u008D', '\u017D', '\u008F',
        '\u0090', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014',
        '\u02DC', '\u2122', '\u0161', '\u203A', '\u0153', '\u009D', '\u017E', '\u0178'
    };

    private const string Cp437High =
        "ÇüéâäàåçêëèïîìÄÅ" +
        "ÉæÆôöòûùÿÖÜ¢£¥₧ƒ" +
        "áíóúñÑªº¿⌐¬½¼¡«»" +
        "░▒▓│┤╡╢╖╕╣║╗╝╜╛┐" +
        "└┴┬├─┼╞╟╚╔╩╦╠═╬╧" +
        "╨╤╥╙╘╒╓╫╪┘┌█▄▌▐▀" +
        "αßΓπΣσµτΦΘΩδ∞φε∩" +
        "≡±≥≤⌠⌡÷≈°∙·√ⁿ²■";

    public static string Repair(string? text)
    {
        if (string.IsNullOrEmpty(text) || !LooksMojibake(text))
            return text ?? "";

        var current = text;
        current = Try(current, Cp1252, Cp1252Char);
        current = Try(current, Cp437, Cp437Char);
        if (LooksMojibake(current))
        {
            current = Try(current, Cp1252, Cp1252Char);
            current = Try(current, Cp437, Cp437Char);
        }

        return current;
    }

    public static bool LooksMojibake(string text)
        => text.Contains("â€", StringComparison.Ordinal)
           || text.Contains("ΓÇ", StringComparison.Ordinal)
           || text.Contains("Ã©", StringComparison.Ordinal)
           || text.Contains("Ã¨", StringComparison.Ordinal)
           || text.Contains("Ã¢", StringComparison.Ordinal);

    private static string Try(string text, Func<string, byte[]?> encode, Func<byte, char> decode)
    {
        if (!LooksMojibake(text))
            return text;

        var bytes = encode(text);
        if (bytes is null)
            return text;

        var repaired = System.Text.Encoding.UTF8.GetString(bytes);
        if (repaired.Contains('\uFFFD', StringComparison.Ordinal))
            return text;

        var utf8 = System.Text.Encoding.UTF8.GetBytes(repaired);
        if (utf8.Length != text.Length)
            return text;
        for (var i = 0; i < utf8.Length; i++)
        {
            if (decode(utf8[i]) != text[i])
                return text;
        }

        return repaired;
    }

    private static char Cp1252Char(byte b)
        => b < 0x80 || b >= 0xA0 ? (char)b : Cp1252High[b - 0x80];

    private static char Cp437Char(byte b)
        => b < 0x80 ? (char)b : Cp437High[b - 0x80];

    private static byte[]? Cp1252(string text) => Encode(text, Cp1252Byte);

    private static byte[]? Cp437(string text) => Encode(text, Cp437Byte);

    private static byte[]? Encode(string text, Func<char, int> map)
    {
        var bytes = new byte[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var b = map(text[i]);
            if (b < 0)
                return null;
            bytes[i] = (byte)b;
        }

        return bytes;
    }

    private static int Cp1252Byte(char c)
    {
        if (c <= 0x7F || (c >= 0xA0 && c <= 0xFF))
            return c;
        for (var i = 0; i < Cp1252High.Length; i++)
        {
            if (Cp1252High[i] == c)
                return 0x80 + i;
        }

        return -1;
    }

    private static int Cp437Byte(char c)
    {
        if (c <= 0x7F)
            return c;
        var at = Cp437High.IndexOf(c);
        return at < 0 ? -1 : 0x80 + at;
    }
}
