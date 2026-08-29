using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Email;

/// <summary><c>email_accounts</c> — list configured mailboxes. No secrets, no IMAP.</summary>
public sealed class EmailAccountsTool : EmailToolBase
{
    private static readonly JsonElement Schema = EmailToolSupport.ParseSchema(
        """{"type":"object","properties":{}}""");

    public EmailAccountsTool(IEmailBridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, new ComputerControlGate(options))
    {
    }

    public EmailAccountsTool(IEmailBridge bridge, IOptions<ToolsOptions> options, IToolsAccessSettings access)
        : base(bridge, options, access)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "email_accounts",
        Description: "List Victoria's mailbox plus Kurt's personal and business accounts she manages. No passwords.",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var accounts = Bridge.ListAccounts();
        return Task.FromResult(new ToolResult(
            true,
            EmailToolSupport.FormatAccounts(accounts),
            new { count = accounts.Count, accounts }));
    }
}

/// <summary><c>email_inbox</c> — list recent messages in a folder.</summary>
public sealed class EmailInboxTool : EmailToolBase
{
    private static readonly JsonElement Schema = EmailToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "account": { "type": "string", "description": "Mailbox id: victoria | personal | business." },
            "folder": { "type": "string", "description": "Folder/label, default INBOX." },
            "limit": { "type": "integer", "description": "Max messages (1-50, default 20)." },
            "unread_only": { "type": "boolean", "description": "When true, only unseen messages." }
          }
        }
        """);

    public EmailInboxTool(IEmailBridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, new ComputerControlGate(options))
    {
    }

    public EmailInboxTool(IEmailBridge bridge, IOptions<ToolsOptions> options, IToolsAccessSettings access)
        : base(bridge, options, access)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "email_inbox",
        Description: "List recent messages in a mailbox folder (INBOX by default). Use account=victoria|personal|business.",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!ReadAllowed)
            return Task.FromResult(new ToolResult(false, EmailToolSupport.ReadDeniedMessage, null));

        return GuardedAsync(async () =>
        {
            var account = EmailToolSupport.ResolveAccountId(args, Bridge);
            if (string.IsNullOrWhiteSpace(account))
                return new ToolResult(false, EmailToolSupport.NoAccountsMessage, null);

            var folder = EmailToolSupport.ResolveFolder(args);
            var limit = EmailToolSupport.ReadLimit(args);
            var unreadOnly = EmailToolSupport.ReadBool(args, "unread_only");
            var rows = await Bridge.ListAsync(account, folder, limit, unreadOnly, ct).ConfigureAwait(false);
            return new ToolResult(
                true,
                EmailToolSupport.FormatSummaries(rows, account, folder),
                new { account, folder, count = rows.Count, messages = rows });
        });
    }
}

/// <summary><c>email_read</c> — fetch one message body.</summary>
public sealed class EmailReadTool : EmailToolBase
{
    private static readonly JsonElement Schema = EmailToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "account": { "type": "string", "description": "Mailbox id: victoria | personal | business." },
            "uid": { "type": "string", "description": "Message uid from email_inbox / email_search." },
            "folder": { "type": "string", "description": "Folder the uid belongs to, default INBOX." }
          },
          "required": ["uid"]
        }
        """);

    public EmailReadTool(IEmailBridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, new ComputerControlGate(options))
    {
    }

    public EmailReadTool(IEmailBridge bridge, IOptions<ToolsOptions> options, IToolsAccessSettings access)
        : base(bridge, options, access)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "email_read",
        Description: "Read one email by uid (from email_inbox). Returns headers + body text.",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!ReadAllowed)
            return Task.FromResult(new ToolResult(false, EmailToolSupport.ReadDeniedMessage, null));

        if (!EmailToolSupport.TryGetRequiredString(args, "uid", out var uid, out var err))
            return Task.FromResult(err!);

        return GuardedAsync(async () =>
        {
            var account = EmailToolSupport.ResolveAccountId(args, Bridge);
            if (string.IsNullOrWhiteSpace(account))
                return new ToolResult(false, EmailToolSupport.NoAccountsMessage, null);

            var folder = EmailToolSupport.TryGetOptionalString(args, "folder", out var f) ? f : null;
            var msg = await Bridge.GetAsync(account, uid, folder, ct).ConfigureAwait(false);
            if (msg is null)
                return new ToolResult(false, $"message uid={uid} not found on {account}.", null);

            return new ToolResult(true, EmailToolSupport.FormatDetail(msg), msg);
        });
    }
}

/// <summary><c>email_search</c> — subject/from/body search.</summary>
public sealed class EmailSearchTool : EmailToolBase
{
    private static readonly JsonElement Schema = EmailToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "account": { "type": "string", "description": "Mailbox id: victoria | personal | business." },
            "query": { "type": "string", "description": "Search text (from, subject, or body)." },
            "folder": { "type": "string", "description": "Folder to search, default INBOX." },
            "limit": { "type": "integer", "description": "Max hits (1-50, default 20)." }
          },
          "required": ["query"]
        }
        """);

    public EmailSearchTool(IEmailBridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, new ComputerControlGate(options))
    {
    }

    public EmailSearchTool(IEmailBridge bridge, IOptions<ToolsOptions> options, IToolsAccessSettings access)
        : base(bridge, options, access)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "email_search",
        Description: "Search a mailbox for messages matching query (from/subject/body).",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!ReadAllowed)
            return Task.FromResult(new ToolResult(false, EmailToolSupport.ReadDeniedMessage, null));

        if (!EmailToolSupport.TryGetRequiredString(args, "query", out var query, out var err))
            return Task.FromResult(err!);

        return GuardedAsync(async () =>
        {
            var account = EmailToolSupport.ResolveAccountId(args, Bridge);
            if (string.IsNullOrWhiteSpace(account))
                return new ToolResult(false, EmailToolSupport.NoAccountsMessage, null);

            var folder = EmailToolSupport.TryGetOptionalString(args, "folder", out var f) ? f : EmailToolSupport.DefaultFolder;
            var limit = EmailToolSupport.ReadLimit(args);
            var rows = await Bridge.SearchAsync(account, query, folder, limit, ct).ConfigureAwait(false);
            return new ToolResult(
                true,
                EmailToolSupport.FormatSummaries(rows, account, folder ?? EmailToolSupport.DefaultFolder),
                new { account, folder, query, count = rows.Count, messages = rows });
        });
    }
}

/// <summary><c>email_file</c> — move to a folder/label (sort).</summary>
public sealed class EmailFileTool : EmailToolBase
{
    private static readonly JsonElement Schema = EmailToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "account": { "type": "string", "description": "Mailbox id: victoria | personal | business." },
            "uid": { "type": "string", "description": "Message uid to file." },
            "folder": { "type": "string", "description": "Current folder, default INBOX." },
            "dest": { "type": "string", "description": "Destination folder/label (Archive, INBOX, [Gmail]/Trash, …)." }
          },
          "required": ["uid", "dest"]
        }
        """);

    public EmailFileTool(IEmailBridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, new ComputerControlGate(options))
    {
    }

    public EmailFileTool(IEmailBridge bridge, IOptions<ToolsOptions> options, IToolsAccessSettings access)
        : base(bridge, options, access)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "email_file",
        Description: "Sort/file a message into a folder or Gmail label (Archive, INBOX, custom). Not delete.",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!ReadAllowed)
            return Task.FromResult(new ToolResult(false, EmailToolSupport.ReadDeniedMessage, null));

        if (!EmailToolSupport.TryGetRequiredString(args, "uid", out var uid, out var err))
            return Task.FromResult(err!);
        if (!EmailToolSupport.TryGetRequiredString(args, "dest", out var dest, out err))
            return Task.FromResult(err!);

        return GuardedAsync(async () =>
        {
            var account = EmailToolSupport.ResolveAccountId(args, Bridge);
            if (string.IsNullOrWhiteSpace(account))
                return new ToolResult(false, EmailToolSupport.NoAccountsMessage, null);

            var folder = EmailToolSupport.TryGetOptionalString(args, "folder", out var f) ? f : null;
            await Bridge.MoveAsync(account, uid, dest, folder, ct).ConfigureAwait(false);
            return new ToolResult(true, $"filed uid={uid} on {account} → {dest}", new { account, uid, dest });
        });
    }
}

/// <summary><c>email_mark</c> — seen / flagged.</summary>
public sealed class EmailMarkTool : EmailToolBase
{
    private static readonly JsonElement Schema = EmailToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "account": { "type": "string", "description": "Mailbox id: victoria | personal | business." },
            "uid": { "type": "string", "description": "Message uid to mark." },
            "folder": { "type": "string", "description": "Folder, default INBOX." },
            "seen": { "type": "boolean", "description": "true=read, false=unread." },
            "flagged": { "type": "boolean", "description": "true=flag/star, false=clear." }
          },
          "required": ["uid"]
        }
        """);

    public EmailMarkTool(IEmailBridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, new ComputerControlGate(options))
    {
    }

    public EmailMarkTool(IEmailBridge bridge, IOptions<ToolsOptions> options, IToolsAccessSettings access)
        : base(bridge, options, access)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "email_mark",
        Description: "Mark a message read/unread and/or flagged (starred).",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!ReadAllowed)
            return Task.FromResult(new ToolResult(false, EmailToolSupport.ReadDeniedMessage, null));

        if (!EmailToolSupport.TryGetRequiredString(args, "uid", out var uid, out var err))
            return Task.FromResult(err!);

        bool? seen = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("seen", out _)
            ? EmailToolSupport.ReadBool(args, "seen")
            : null;
        bool? flagged = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("flagged", out _)
            ? EmailToolSupport.ReadBool(args, "flagged")
            : null;

        if (seen is null && flagged is null)
            return Task.FromResult(new ToolResult(false, "error: set seen and/or flagged.", null));

        return GuardedAsync(async () =>
        {
            var account = EmailToolSupport.ResolveAccountId(args, Bridge);
            if (string.IsNullOrWhiteSpace(account))
                return new ToolResult(false, EmailToolSupport.NoAccountsMessage, null);

            var folder = EmailToolSupport.TryGetOptionalString(args, "folder", out var f) ? f : null;
            await Bridge.MarkAsync(account, uid, seen, flagged, folder, ct).ConfigureAwait(false);
            return new ToolResult(
                true,
                $"marked uid={uid} on {account} seen={seen?.ToString() ?? "-"} flagged={flagged?.ToString() ?? "-"}",
                new { account, uid, seen, flagged });
        });
    }
}
