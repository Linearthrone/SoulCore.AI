using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Email;

/// <summary>
/// IMAP + SMTP via MailKit. Connects per call; never logs passwords.
/// </summary>
public sealed class MailKitEmailBridge : IEmailBridge
{
    private readonly IOptions<EmailOptions> _options;
    private readonly ILogger<MailKitEmailBridge> _logger;

    public MailKitEmailBridge(IOptions<EmailOptions> options, ILogger<MailKitEmailBridge> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<EmailAccountInfo> ListAccounts() =>
        EnumerateAccounts().Select(ToInfo).ToList();

    public async Task<IReadOnlyList<string>> ListFoldersAsync(string accountId, CancellationToken ct = default)
    {
        var account = RequireAccount(accountId);
        await using var session = await ConnectImapAsync(account, ct).ConfigureAwait(false);
        var names = new List<string>();
        var root = session.Client.Inbox?.ParentFolder
            ?? session.Client.GetFolder(session.Client.PersonalNamespaces[0])
            ?? session.Client.Inbox
            ?? throw new InvalidOperationException("IMAP personal namespace unavailable");
        await CollectFoldersAsync(root, names, ct).ConfigureAwait(false);
        if (names.Count == 0)
            names.Add("INBOX");
        return names;
    }

    public async Task<IReadOnlyList<EmailMessageSummary>> ListAsync(
        string accountId,
        string folder,
        int limit,
        bool unreadOnly,
        CancellationToken ct = default)
    {
        var account = RequireAccount(accountId);
        await using var session = await ConnectImapAsync(account, ct).ConfigureAwait(false);
        var box = await OpenFolderAsync(session.Client, folder, FolderAccess.ReadOnly, ct).ConfigureAwait(false);
        if (box.Count == 0)
            return Array.Empty<EmailMessageSummary>();

        IList<UniqueId> uids = unreadOnly
            ? await box.SearchAsync(SearchQuery.NotSeen, ct).ConfigureAwait(false)
            : await box.SearchAsync(SearchQuery.All, ct).ConfigureAwait(false);

        if (uids.Count > limit)
            uids = uids.Skip(Math.Max(0, uids.Count - limit)).ToList();

        var summaries = await box.FetchAsync(
            uids,
            MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.InternalDate,
            ct).ConfigureAwait(false);

        return summaries
            .OrderByDescending(s => s.Date)
            .Select(s => ToSummary(account.ResolveId(), box.FullName, s))
            .ToList();
    }

    public async Task<EmailMessageDetail?> GetAsync(
        string accountId,
        string uid,
        string? folder,
        CancellationToken ct = default)
    {
        var account = RequireAccount(accountId);
        if (!UniqueId.TryParse(uid, out var unique))
            throw new InvalidOperationException($"invalid uid '{uid}'");

        await using var session = await ConnectImapAsync(account, ct).ConfigureAwait(false);
        var box = await OpenFolderAsync(session.Client, folder ?? EmailToolSupport.DefaultFolder, FolderAccess.ReadOnly, ct)
            .ConfigureAwait(false);
        var message = await box.GetMessageAsync(unique, ct).ConfigureAwait(false);
        var summaries = await box.FetchAsync(
            new[] { unique },
            MessageSummaryItems.UniqueId | MessageSummaryItems.Flags,
            ct).ConfigureAwait(false);
        var flags = summaries.FirstOrDefault()?.Flags ?? MessageFlags.None;
        return ToDetail(account.ResolveId(), box.FullName, unique, message, flags);
    }

    public async Task<IReadOnlyList<EmailMessageSummary>> SearchAsync(
        string accountId,
        string query,
        string? folder,
        int limit,
        CancellationToken ct = default)
    {
        var account = RequireAccount(accountId);
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<EmailMessageSummary>();

        await using var session = await ConnectImapAsync(account, ct).ConfigureAwait(false);
        var box = await OpenFolderAsync(session.Client, folder ?? EmailToolSupport.DefaultFolder, FolderAccess.ReadOnly, ct)
            .ConfigureAwait(false);

        var q = query.Trim();
        var search = SearchQuery.Or(
            SearchQuery.SubjectContains(q),
            SearchQuery.Or(SearchQuery.FromContains(q), SearchQuery.BodyContains(q)));
        var uids = await box.SearchAsync(search, ct).ConfigureAwait(false);
        if (uids.Count > limit)
            uids = uids.Skip(Math.Max(0, uids.Count - limit)).ToList();

        var summaries = await box.FetchAsync(
            uids,
            MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.InternalDate,
            ct).ConfigureAwait(false);

        return summaries
            .OrderByDescending(s => s.Date)
            .Select(s => ToSummary(account.ResolveId(), box.FullName, s))
            .ToList();
    }

    public async Task MoveAsync(
        string accountId,
        string uid,
        string destFolder,
        string? sourceFolder,
        CancellationToken ct = default)
    {
        var account = RequireAccount(accountId);
        if (!UniqueId.TryParse(uid, out var unique))
            throw new InvalidOperationException($"invalid uid '{uid}'");

        await using var session = await ConnectImapAsync(account, ct).ConfigureAwait(false);
        var source = await OpenFolderAsync(session.Client, sourceFolder ?? EmailToolSupport.DefaultFolder, FolderAccess.ReadWrite, ct)
            .ConfigureAwait(false);
        var dest = await ResolveFolderAsync(session.Client, destFolder, createIfMissing: true, ct).ConfigureAwait(false);
        await source.MoveToAsync(unique, dest, ct).ConfigureAwait(false);
    }

    public async Task MarkAsync(
        string accountId,
        string uid,
        bool? seen,
        bool? flagged,
        string? folder,
        CancellationToken ct = default)
    {
        var account = RequireAccount(accountId);
        if (!UniqueId.TryParse(uid, out var unique))
            throw new InvalidOperationException($"invalid uid '{uid}'");

        await using var session = await ConnectImapAsync(account, ct).ConfigureAwait(false);
        var box = await OpenFolderAsync(session.Client, folder ?? EmailToolSupport.DefaultFolder, FolderAccess.ReadWrite, ct)
            .ConfigureAwait(false);

        if (seen is true)
            await box.AddFlagsAsync(unique, MessageFlags.Seen, silent: true, ct).ConfigureAwait(false);
        else if (seen is false)
            await box.RemoveFlagsAsync(unique, MessageFlags.Seen, silent: true, ct).ConfigureAwait(false);

        if (flagged is true)
            await box.AddFlagsAsync(unique, MessageFlags.Flagged, silent: true, ct).ConfigureAwait(false);
        else if (flagged is false)
            await box.RemoveFlagsAsync(unique, MessageFlags.Flagged, silent: true, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(
        string accountId,
        string uid,
        string? folder,
        CancellationToken ct = default)
    {
        var account = RequireAccount(accountId);
        if (!UniqueId.TryParse(uid, out var unique))
            throw new InvalidOperationException($"invalid uid '{uid}'");

        await using var session = await ConnectImapAsync(account, ct).ConfigureAwait(false);
        var box = await OpenFolderAsync(session.Client, folder ?? EmailToolSupport.DefaultFolder, FolderAccess.ReadWrite, ct)
            .ConfigureAwait(false);

        IMailFolder? trash = null;
        try
        {
            trash = await TryFindFolderAsync(session.Client, "Trash", ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Trash folder lookup failed; falling back to \\Deleted");
        }

        if (trash is not null && !string.Equals(trash.FullName, box.FullName, StringComparison.OrdinalIgnoreCase))
        {
            await box.MoveToAsync(unique, trash, ct).ConfigureAwait(false);
            return;
        }

        await box.AddFlagsAsync(unique, MessageFlags.Deleted, silent: true, ct).ConfigureAwait(false);
        await box.ExpungeAsync(ct).ConfigureAwait(false);
    }

    public async Task<string> SendAsync(
        string accountId,
        EmailComposeRequest request,
        CancellationToken ct = default)
    {
        var account = RequireAccount(accountId);
        ArgumentNullException.ThrowIfNull(request);
        if (request.To.Count == 0)
            throw new InvalidOperationException("send requires at least one recipient");

        var message = new MimeMessage();
        var fromName = string.IsNullOrWhiteSpace(account.DisplayName)
            ? account.Address
            : account.DisplayName;
        message.From.Add(new MailboxAddress(fromName, account.Address.Trim()));
        foreach (var to in request.To)
            message.To.Add(MailboxAddress.Parse(to));
        if (request.Cc is not null)
        {
            foreach (var cc in request.Cc)
                message.Cc.Add(MailboxAddress.Parse(cc));
        }

        message.Subject = request.Subject ?? string.Empty;
        message.Body = new TextPart("plain") { Text = request.Body ?? string.Empty };

        if (!string.IsNullOrWhiteSpace(request.InReplyToUid))
        {
            try
            {
                var original = await GetAsync(accountId, request.InReplyToUid, request.InReplyToFolder, ct)
                    .ConfigureAwait(false);
                if (original is not null && !string.IsNullOrWhiteSpace(original.MessageId))
                {
                    message.InReplyTo = original.MessageId;
                    message.References.Add(original.MessageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "reply header lookup failed; sending without In-Reply-To");
            }
        }

        using var smtp = new SmtpClient();
        smtp.Timeout = 30_000;
        var smtpSecure = ResolveSmtpSecure(account);
        await smtp.ConnectAsync(account.SmtpHost.Trim(), account.SmtpPort, smtpSecure, ct).ConfigureAwait(false);
        try
        {
            await smtp.AuthenticateAsync(account.ResolveUsername(), account.Password, ct).ConfigureAwait(false);
            await smtp.SendAsync(message, ct).ConfigureAwait(false);
        }
        finally
        {
            if (smtp.IsConnected)
                await smtp.DisconnectAsync(true, ct).ConfigureAwait(false);
        }

        return message.MessageId ?? string.Empty;
    }

    private IEnumerable<EmailAccountOptions> EnumerateAccounts()
    {
        var list = _options.Value?.Accounts;
        if (list is null)
            yield break;
        foreach (var a in list)
        {
            if (a is null) continue;
            if (string.IsNullOrWhiteSpace(a.ResolveId()))
                continue;
            yield return a;
        }
    }

    private EmailAccountOptions RequireAccount(string accountId)
    {
        var accounts = EnumerateAccounts().ToList();
        if (accounts.Count == 0)
            throw new InvalidOperationException(EmailToolSupport.NoAccountsMessage);

        var key = (accountId ?? string.Empty).Trim();
        EmailAccountOptions? match = null;
        if (!string.IsNullOrWhiteSpace(key))
        {
            match = accounts.FirstOrDefault(a =>
                string.Equals(a.ResolveId(), key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(a.ResolveRole(), key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(a.Address, key, StringComparison.OrdinalIgnoreCase));
        }

        match ??= accounts.Count == 1 ? accounts[0] : null;
        if (match is null)
            throw new InvalidOperationException($"unknown email account '{accountId}' — use email_accounts");

        if (!match.Enabled)
            throw new InvalidOperationException($"email account '{match.ResolveId()}' is disabled");
        if (!match.HasPassword)
            throw new InvalidOperationException($"email account '{match.ResolveId()}' has no password in env");
        if (string.IsNullOrWhiteSpace(match.Address))
            throw new InvalidOperationException($"email account '{match.ResolveId()}' has no Address");

        return match;
    }

    private static EmailAccountInfo ToInfo(EmailAccountOptions a) =>
        new(a.ResolveId(), a.ResolveRole(), a.DisplayName ?? "", a.Address ?? "", a.Enabled, a.IsConfigured);

    private async Task<ImapSession> ConnectImapAsync(EmailAccountOptions account, CancellationToken ct)
    {
        var client = new ImapClient { Timeout = 30_000 };
        var secure = account.ImapUseSsl || account.ImapPort == 993
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
        await client.ConnectAsync(account.ImapHost.Trim(), account.ImapPort, secure, ct).ConfigureAwait(false);
        await client.AuthenticateAsync(account.ResolveUsername(), account.Password, ct).ConfigureAwait(false);
        _logger.LogDebug("IMAP connected account={Account}", account.ResolveId());
        return new ImapSession(client);
    }

    private static SecureSocketOptions ResolveSmtpSecure(EmailAccountOptions account)
    {
        if (account.SmtpUseSsl || account.SmtpPort == 465)
            return SecureSocketOptions.SslOnConnect;
        return SecureSocketOptions.StartTls;
    }

    private static async Task<IMailFolder> OpenFolderAsync(
        ImapClient client,
        string folder,
        FolderAccess access,
        CancellationToken ct)
    {
        var resolved = await ResolveFolderAsync(client, folder, createIfMissing: false, ct).ConfigureAwait(false);
        await resolved.OpenAsync(access, ct).ConfigureAwait(false);
        return resolved;
    }

    private static async Task<IMailFolder> ResolveFolderAsync(
        ImapClient client,
        string folder,
        bool createIfMissing,
        CancellationToken ct)
    {
        var name = EmailToolSupport.NormalizeFolderName(folder);
        if (string.Equals(name, "INBOX", StringComparison.OrdinalIgnoreCase))
            return client.Inbox ?? throw new InvalidOperationException("IMAP INBOX unavailable");

        var found = await TryFindFolderAsync(client, name, ct).ConfigureAwait(false);
        if (found is not null)
            return found;

        if (!createIfMissing)
            throw new InvalidOperationException($"folder '{folder}' not found");

        var parent = client.Inbox?.ParentFolder
            ?? client.GetFolder(client.PersonalNamespaces[0])
            ?? throw new InvalidOperationException("IMAP parent folder unavailable");
        var created = await parent.CreateAsync(name, isMessageFolder: true, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"failed to create folder '{name}'");
        return created;
    }

    private static async Task<IMailFolder?> TryFindFolderAsync(ImapClient client, string name, CancellationToken ct)
    {
        var aliases = FolderAliases(name);
        var personal = client.GetFolder(client.PersonalNamespaces[0]);
        var all = new List<IMailFolder>();
        if (personal is not null)
            await CollectFolderRefsAsync(personal, all, ct).ConfigureAwait(false);
        if (client.Inbox is not null)
            all.Add(client.Inbox);

        foreach (var alias in aliases)
        {
            var hit = all.FirstOrDefault(f =>
                string.Equals(f.FullName, alias, StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.Name, alias, StringComparison.OrdinalIgnoreCase)
                || f.FullName.EndsWith("/" + alias, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
                return hit;
        }

        return null;
    }

    private static async Task CollectFoldersAsync(IMailFolder root, List<string> names, CancellationToken ct)
    {
        if (root is null)
            return;
        if ((root.Attributes & FolderAttributes.NoSelect) == 0 && !string.IsNullOrWhiteSpace(root.FullName))
            names.Add(root.FullName);
        foreach (var child in await root.GetSubfoldersAsync(false, ct).ConfigureAwait(false))
            await CollectFoldersAsync(child, names, ct).ConfigureAwait(false);
    }

    private static async Task CollectFolderRefsAsync(IMailFolder root, List<IMailFolder> folders, CancellationToken ct)
    {
        if (root is null)
            return;
        folders.Add(root);
        foreach (var child in await root.GetSubfoldersAsync(false, ct).ConfigureAwait(false))
            await CollectFolderRefsAsync(child, folders, ct).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> FolderAliases(string name)
    {
        var n = name.Trim();
        var list = new List<string> { n, EmailToolSupport.NormalizeFolderName(n) };
        switch (n.ToLowerInvariant())
        {
            case "trash":
            case "[gmail]/trash":
                list.AddRange(new[] { "Trash", "[Gmail]/Trash", "Deleted Items" });
                break;
            case "sent":
            case "[gmail]/sent mail":
                list.AddRange(new[] { "Sent", "Sent Mail", "[Gmail]/Sent Mail" });
                break;
            case "archive":
            case "[gmail]/all mail":
                list.AddRange(new[] { "Archive", "All Mail", "[Gmail]/All Mail" });
                break;
            case "spam":
            case "[gmail]/spam":
                list.AddRange(new[] { "Spam", "Junk", "[Gmail]/Spam" });
                break;
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static EmailMessageSummary ToSummary(string accountId, string folder, IMessageSummary s)
    {
        var env = s.Envelope;
        var from = env?.From?.Mailboxes.FirstOrDefault();
        var to = env?.To?.Mailboxes.FirstOrDefault();
        var flags = s.Flags ?? MessageFlags.None;
        var unread = !flags.HasFlag(MessageFlags.Seen);
        var flagged = flags.HasFlag(MessageFlags.Flagged);
        var subject = env?.Subject ?? "(no subject)";
        return new EmailMessageSummary(
            accountId,
            s.UniqueId.ToString(),
            folder,
            FormatMailbox(from),
            FormatMailbox(to),
            subject,
            s.Date,
            unread,
            flagged,
            EmailToolSupport.Truncate(subject, EmailToolSupport.SnippetChars));
    }

    private static EmailMessageDetail ToDetail(
        string accountId,
        string folder,
        UniqueId uid,
        MimeMessage message,
        MessageFlags flags)
    {
        var attachments = message.Attachments
            .Select(a => a.ContentDisposition?.FileName ?? a.ContentType?.Name ?? "attachment")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        var body = message.TextBody ?? message.HtmlBody ?? string.Empty;
        if (!string.IsNullOrEmpty(message.HtmlBody) && string.IsNullOrEmpty(message.TextBody))
            body = StripRoughHtml(message.HtmlBody);

        return new EmailMessageDetail(
            accountId,
            uid.ToString(),
            folder,
            string.Join(", ", message.From.Mailboxes.Select(FormatMailbox)),
            string.Join(", ", message.To.Mailboxes.Select(FormatMailbox)),
            string.Join(", ", message.Cc.Mailboxes.Select(FormatMailbox)),
            message.Subject ?? "(no subject)",
            message.Date,
            Unread: !flags.HasFlag(MessageFlags.Seen),
            Flagged: flags.HasFlag(MessageFlags.Flagged),
            body,
            message.MessageId ?? string.Empty,
            attachments);
    }

    private static string FormatMailbox(MailboxAddress? box)
    {
        if (box is null)
            return "";
        if (string.IsNullOrWhiteSpace(box.Name))
            return box.Address;
        return $"{box.Name} <{box.Address}>";
    }

    private static string StripRoughHtml(string html)
    {
        var chars = new char[html.Length];
        var n = 0;
        var inTag = false;
        foreach (var c in html)
        {
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag)
                chars[n++] = c;
        }

        return new string(chars, 0, n);
    }

    private sealed class ImapSession : IAsyncDisposable
    {
        public ImapSession(ImapClient client) => Client = client;

        public ImapClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (Client.IsConnected)
                    await Client.DisconnectAsync(true).ConfigureAwait(false);
            }
            catch
            {
                // ignore disconnect failures
            }

            Client.Dispose();
        }
    }
}
