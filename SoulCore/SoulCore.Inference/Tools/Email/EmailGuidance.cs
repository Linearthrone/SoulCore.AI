namespace SoulCore.Inference.Tools.Email;

/// <summary>
/// System guidance so Victoria uses email_* tools instead of browsing Gmail
/// or describing a plan in prose.
/// </summary>
public static class EmailGuidance
{
    public const string Marker = "[Email]";

    public const string Block =
        Marker + "\n" +
        "Mailboxes: victoria (yours), personal, business. Use email_inbox / email_search / email_read — " +
        "do NOT open Gmail in the browser for routine check/sort/reply.\n" +
        "email_send and email_delete return a confirm prompt first. She replies yes — model-minted confirmed=true is ignored.";

    public static string AppendToPreamble(string? contextPreamble)
    {
        var baseText = string.IsNullOrWhiteSpace(contextPreamble)
            ? string.Empty
            : contextPreamble.TrimEnd();

        if (baseText.Contains(Marker, StringComparison.Ordinal))
            return baseText;

        if (baseText.Length == 0)
            return Block;

        return baseText + "\n\n" + Block;
    }
}
