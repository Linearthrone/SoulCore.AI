using System.Text.RegularExpressions;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Email;

/// <summary>
/// High-confidence NL → email_* force-tool. Read-safe only (inbox/accounts/search).
/// Never force send/delete.
/// </summary>
public static class EmailToolIntent
{
    public enum Kind
    {
        Accounts,
        Inbox,
        Search
    }

    public readonly record struct Match(Kind Intent, string ToolName, string? AccountId);

    private static readonly Regex ExplicitTool = new(
        @"\bemail_(?:inbox|accounts|search)\b|\bcall\s+(?:the\s+)?email_(?:inbox|accounts|search)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AccountsAsk = new(
        @"\b(?:list|which|what)\b[\s\S]{0,24}\b(?:email\s+accounts?|mailboxes?)\b|" +
        @"\bemail_accounts\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SearchAsk = new(
        @"\bsearch\b[\s\S]{0,32}\b(?:e-?mails?|inbox|mail(?:box)?)\b|" +
        @"\b(?:find|look\s+up)\b[\s\S]{0,32}\b(?:e-?mail|inbox)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InboxAsk = new(
        @"\b(?:check|read|look\s+at|go\s+through|triage|sort)\b[\s\S]{0,40}\b(?:e-?mails?|inbox|mail)\b|" +
        @"\b(?:e-?mails?|inbox)\b[\s\S]{0,32}\b(?:check|unread|new|today)\b|" +
        @"\b(?:what(?:'s| is)|anything)\b[\s\S]{0,24}\b(?:(?:in\s+)?(?:my|the|her|your)\s+)?(?:e-?mail|inbox)\b|" +
        @"\b(?:my|your|her|victoria'?s|personal|business)\s+(?:e-?mail|inbox)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BrowserGmail = new(
        @"\b(?:open|launch|go\s+to)\b[\s\S]{0,24}\b(?:gmail|outlook\.com|mail\.google)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryMatch(string? userText, out Match match)
    {
        match = default;
        if (string.IsNullOrWhiteSpace(userText))
            return false;

        var text = userText.Trim();
        if (BrowserGmail.IsMatch(text) && !InboxAsk.IsMatch(text) && !SearchAsk.IsMatch(text))
            return false;

        var account = InferAccount(text);

        if (AccountsAsk.IsMatch(text)
            || (ExplicitTool.IsMatch(text) && text.Contains("email_accounts", StringComparison.OrdinalIgnoreCase)))
        {
            match = new Match(Kind.Accounts, "email_accounts", account);
            return true;
        }

        if (SearchAsk.IsMatch(text)
            || (ExplicitTool.IsMatch(text) && text.Contains("email_search", StringComparison.OrdinalIgnoreCase)))
        {
            match = new Match(Kind.Search, "email_search", account);
            return true;
        }

        if (InboxAsk.IsMatch(text)
            || (ExplicitTool.IsMatch(text) && text.Contains("email_inbox", StringComparison.OrdinalIgnoreCase)))
        {
            match = new Match(Kind.Inbox, "email_inbox", account);
            return true;
        }

        return false;
    }

    public static string? InferAccount(string? userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
            return null;
        var t = userText.ToLowerInvariant();
        if (Regex.IsMatch(t, @"\b(?:your|her|victoria'?s)\s+(?:e-?mail|inbox|mailbox)\b")
            || Regex.IsMatch(t, @"\bvictoria'?s\s+(?:e-?mail|inbox|mailbox)\b"))
            return EmailOptions.RoleVictoria;
        if (Regex.IsMatch(t, @"\bbusiness\b|\bwork\s+(?:e-?mail|inbox|mailbox)\b"))
            return EmailOptions.RoleBusiness;
        if (Regex.IsMatch(t, @"\bpersonal\b|\bmy\s+(?:e-?mail|inbox|mailbox)\b"))
            return EmailOptions.RolePersonal;
        return null;
    }
}
