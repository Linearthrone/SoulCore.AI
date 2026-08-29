using System.Text.Json;
using SoulCore.Config;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Email;

/// <summary>
/// Shared gates, arg parsing, and confirm prompts for the email tool family.
/// </summary>
public static class EmailToolSupport
{
    public const string ReadDeniedMessage =
        "email read requires user authorization — enable AllowEmailRead in Settings → Tools & Access";

    public const string SendDeniedMessage =
        "email send requires user authorization — enable AllowEmailSend in Settings → Tools & Access";

    public const string DeleteDeniedMessage =
        "email delete requires user authorization — enable AllowEmailDelete in Settings → Tools & Access";

    public const string NoAccountsMessage =
        "no email accounts configured — Kurt needs to add Email:Accounts (victoria / personal / business) in SoulCore/.env";

    public const string DefaultFolder = "INBOX";
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
    public const int MaxBodyChars = 8000;
    public const int SnippetChars = 160;

    public static bool IsReadAllowed(IToolsAccessSettings access) =>
        access is not null && access.AllowEmailRead;

    public static bool IsSendAllowed(IToolsAccessSettings access) =>
        access is not null && access.AllowEmailSend;

    public static bool IsDeleteAllowed(IToolsAccessSettings access) =>
        access is not null && access.AllowEmailDelete;

    public static bool IsReadAllowed(ToolsOptions options) =>
        options is not null && options.AllowEmailRead;

    public static bool IsSendAllowed(ToolsOptions options) =>
        options is not null && options.AllowEmailSend;

    public static bool IsDeleteAllowed(ToolsOptions options) =>
        options is not null && options.AllowEmailDelete;

    public static bool IsConfirmed(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object)
            return false;
        if (!args.TryGetProperty("confirmed", out var prop))
            return false;

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => IsTruthyString(prop.GetString()),
            JsonValueKind.Number => prop.TryGetInt32(out var n) && n != 0,
            _ => false
        };
    }

    public static JsonElement ParseSchema(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    public static bool TryGetOptionalString(JsonElement args, string name, out string? value)
    {
        value = null;
        if (args.ValueKind != JsonValueKind.Object)
            return false;
        if (!args.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return false;
        var s = el.GetString();
        if (string.IsNullOrWhiteSpace(s))
            return false;
        value = s.Trim();
        return true;
    }

    public static bool TryGetRequiredString(JsonElement args, string name, out string value, out ToolResult? error)
    {
        value = string.Empty;
        error = null;
        if (args.ValueKind != JsonValueKind.Object)
        {
            error = new ToolResult(false, $"error: expected a JSON object with '{name}'.", null);
            return false;
        }

        if (!TryGetOptionalString(args, name, out var found) || found is null)
        {
            error = new ToolResult(false, $"error: '{name}' is required.", null);
            return false;
        }

        value = found;
        return true;
    }

    public static int ReadLimit(JsonElement args, int fallback = DefaultLimit)
    {
        if (args.ValueKind != JsonValueKind.Object)
            return fallback;
        if (!args.TryGetProperty("limit", out var el))
            return fallback;
        var n = el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(el.GetString(), out var p) => p,
            _ => fallback
        };
        if (n < 1) n = 1;
        if (n > MaxLimit) n = MaxLimit;
        return n;
    }

    public static bool ReadBool(JsonElement args, string name, bool fallback = false)
    {
        if (args.ValueKind != JsonValueKind.Object)
            return fallback;
        if (!args.TryGetProperty(name, out var el))
            return fallback;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => IsTruthyString(el.GetString()),
            JsonValueKind.Number => el.TryGetInt32(out var n) && n != 0,
            _ => fallback
        };
    }

    public static IReadOnlyList<string> ReadStringList(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object)
            return Array.Empty<string>();
        if (!args.TryGetProperty(name, out var el))
            return Array.Empty<string>();

        if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString();
            return string.IsNullOrWhiteSpace(s)
                ? Array.Empty<string>()
                : SplitAddresses(s);
        }

        if (el.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                continue;
            var s = item.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                list.Add(s.Trim());
        }

        return list;
    }

    public static IReadOnlyList<string> SplitAddresses(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();
        return raw
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToArray();
    }

    public static string ResolveAccountId(JsonElement args, IEmailBridge bridge)
    {
        if (TryGetOptionalString(args, "account", out var requested) && requested is not null)
            return requested;

        var accounts = bridge.ListAccounts();
        if (accounts.Count == 1)
            return accounts[0].Id;
        var personal = accounts.FirstOrDefault(a =>
            string.Equals(a.Role, EmailOptions.RolePersonal, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Id, EmailOptions.RolePersonal, StringComparison.OrdinalIgnoreCase));
        if (personal is not null)
            return personal.Id;
        return accounts.FirstOrDefault()?.Id ?? "";
    }

    public static string ResolveFolder(JsonElement args) =>
        TryGetOptionalString(args, "folder", out var folder) && folder is not null
            ? folder
            : DefaultFolder;

    public static string BuildSendConfirmPrompt(string accountId, IReadOnlyList<string> to, string subject) =>
        $"confirm send from {accountId} to {string.Join(", ", to)} subject \"{subject}\"? reply yes, then recall with confirmed=true";

    public static string BuildDeleteConfirmPrompt(string accountId, string uid, string? subject) =>
        string.IsNullOrWhiteSpace(subject)
            ? $"confirm delete message {uid} on {accountId}? reply yes, then recall with confirmed=true"
            : $"confirm delete on {accountId}: \"{subject}\" (uid {uid})? reply yes, then recall with confirmed=true";

    public static string NormalizeFolderName(string? folder)
    {
        var raw = (folder ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return DefaultFolder;
        return raw.ToLowerInvariant() switch
        {
            "inbox" => DefaultFolder,
            "sent" or "sent mail" or "sent-mail" => "[Gmail]/Sent Mail",
            "drafts" or "draft" => "[Gmail]/Drafts",
            "trash" or "bin" or "deleted" => "[Gmail]/Trash",
            "spam" or "junk" => "[Gmail]/Spam",
            "archive" or "all mail" or "allmail" => "[Gmail]/All Mail",
            "starred" => "[Gmail]/Starred",
            _ => raw
        };
    }

    public static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        var t = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (t.Length <= max)
            return t;
        return t[..max].TrimEnd() + "…";
    }

    public static string FormatSummaries(IReadOnlyList<EmailMessageSummary> rows, string accountId, string folder)
    {
        if (rows.Count == 0)
            return $"no messages in {accountId}/{folder}.";

        var sb = new System.Text.StringBuilder(64 + rows.Count * 120);
        sb.Append(rows.Count).Append(" message").Append(rows.Count == 1 ? "" : "s")
          .Append(" in ").Append(accountId).Append('/').Append(folder).Append(':');
        foreach (var m in rows)
        {
            sb.Append("\n[").Append(m.Uid).Append("] ");
            if (m.Unread) sb.Append("UNREAD ");
            if (m.Flagged) sb.Append("FLAG ");
            sb.Append(m.Date.ToString("yyyy-MM-dd HH:mm")).Append("  ")
              .Append(Truncate(m.From, 40)).Append(" — ")
              .Append(Truncate(m.Subject, 80));
        }

        return sb.ToString();
    }

    public static string FormatDetail(EmailMessageDetail m)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("account=").Append(m.AccountId)
          .Append(" uid=").Append(m.Uid)
          .Append(" folder=").Append(m.Folder).AppendLine();
        sb.Append("from: ").Append(m.From).AppendLine();
        sb.Append("to: ").Append(m.To).AppendLine();
        if (!string.IsNullOrWhiteSpace(m.Cc))
            sb.Append("cc: ").Append(m.Cc).AppendLine();
        sb.Append("date: ").Append(m.Date.ToString("u")).AppendLine();
        sb.Append("subject: ").Append(m.Subject).AppendLine();
        if (m.AttachmentNames.Count > 0)
            sb.Append("attachments: ").Append(string.Join(", ", m.AttachmentNames)).AppendLine();
        sb.AppendLine();
        sb.Append(Truncate(m.Body, MaxBodyChars));
        return sb.ToString();
    }

    public static string FormatAccounts(IReadOnlyList<EmailAccountInfo> accounts)
    {
        if (accounts.Count == 0)
            return NoAccountsMessage;

        var sb = new System.Text.StringBuilder();
        sb.Append(accounts.Count).Append(" email account").Append(accounts.Count == 1 ? "" : "s").Append(':');
        foreach (var a in accounts)
        {
            sb.Append("\n[").Append(a.Id).Append("] role=").Append(a.Role)
              .Append(" ").Append(a.Address);
            if (!string.IsNullOrWhiteSpace(a.DisplayName))
                sb.Append(" (").Append(a.DisplayName).Append(')');
            if (!a.Enabled) sb.Append(" disabled");
            else if (!a.Configured) sb.Append(" — password missing");
        }

        return sb.ToString();
    }

    private static bool IsTruthyString(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        return s.Equals("true", StringComparison.OrdinalIgnoreCase)
            || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || s.Equals("1", StringComparison.OrdinalIgnoreCase);
    }
}
