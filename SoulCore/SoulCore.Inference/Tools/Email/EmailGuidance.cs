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
        "You manage three mailboxes when configured: victoria (yours), personal (Kurt), business (Kurt).\n" +
        "Use email_* tools — do NOT open Gmail in the browser for routine check/sort/reply.\n" +
        "Workflow:\n" +
        "1) email_accounts if you need to see which mailboxes are ready.\n" +
        "2) email_inbox(account, unread_only=true) to triage; email_search(query) to find a thread.\n" +
        "3) email_read(uid) before you summarize or reply. Quote facts from the tool result — do not invent mail.\n" +
        "4) Sort with email_file(uid, dest) (Archive / INBOX / a label). Mark read with email_mark.\n" +
        "5) email_delete and email_send are two-phase: first call returns a confirm prompt. " +
        "Tell Kurt what you would send or delete and wait. Only call again with confirmed=true after he agrees.\n" +
        "Never send or delete on a first tool call. Never put passwords in chat. " +
        "If a tool says AllowEmailRead/Send/Delete is required, tell Kurt to enable it in Settings → Tools & Access.";

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
