namespace SoulCore.Protocol;

/// <summary>
/// Formats Kurt's quote-reply context into the user turn the model sees.
/// </summary>
public static class QuotedChatText
{
    public const int MaxQuotedChars = 2000;

    public static string? NormalizeQuoted(string? quotedText)
    {
        if (string.IsNullOrWhiteSpace(quotedText))
            return null;
        var trimmed = quotedText.Trim();
        return trimmed.Length <= MaxQuotedChars
            ? trimmed
            : trimmed[..MaxQuotedChars] + "…";
    }

    /// <summary>
    /// Builds the user turn the model sees: quoted excerpt first, then Kurt's reply.
    /// </summary>
    public static string BuildUserText(string text, string? quotedText)
    {
        var quote = NormalizeQuoted(quotedText);
        if (quote is null)
            return text;

        var blocked = quote
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "\n> ", StringComparison.Ordinal);

        return
            "[Kurt is replying to this excerpt of your earlier message:]\n" +
            "> " + blocked +
            "\n\n" + text;
    }
}
