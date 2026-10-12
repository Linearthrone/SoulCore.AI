using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Presence;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Email;

/// <summary>
/// <c>email_delete</c> — master AllowEmailDelete + host-side operator consent (PROP-17.1).
/// Model-minted <c>confirmed</c> is stripped and never grants consent.
/// </summary>
public sealed class EmailDeleteTool : EmailToolBase
{
    private static readonly JsonElement Schema = EmailToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "account": { "type": "string", "description": "Mailbox id: victoria | personal | business." },
            "uid": { "type": "string", "description": "Message uid to delete." },
            "folder": { "type": "string", "description": "Folder, default INBOX." },
            "confirmed": { "type": "boolean", "description": "Ignored — model cannot mint consent. Kayleigh replies yes.", "default": false }
          },
          "required": ["uid"]
        }
        """);

    public EmailDeleteTool(IEmailBridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, new ComputerControlGate(options), consent: null)
    {
    }

    public EmailDeleteTool(IEmailBridge bridge, IOptions<ToolsOptions> options, IToolsAccessSettings access)
        : this(bridge, options, access, consent: null)
    {
    }

    public EmailDeleteTool(
        IEmailBridge bridge,
        IOptions<ToolsOptions> options,
        IToolsAccessSettings access,
        IOperatorConsentGate? consent)
        : base(bridge, options, access, consent)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "email_delete",
        Description: "Delete a message. First call returns a confirmation prompt; only deletes after Kayleigh replies yes (model cannot mint confirmed).",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!DeleteAllowed)
            return Task.FromResult(new ToolResult(false, EmailToolSupport.DeleteDeniedMessage, null));

        args = EmailToolSupport.StripModelConfirmed(args);

        if (!EmailToolSupport.TryGetRequiredString(args, "uid", out var uid, out var err))
            return Task.FromResult(err!);

        var account = EmailToolSupport.ResolveAccountId(args, Bridge);
        if (string.IsNullOrWhiteSpace(account))
            return Task.FromResult(new ToolResult(false, EmailToolSupport.NoAccountsMessage, null));

        if (!EmailToolSupport.IsConfirmed(args, Consent, OperatorConsentGate.EmailDelete))
        {
            Consent?.NotePending(OperatorConsentGate.EmailDelete);
            return Task.FromResult(new ToolResult(
                false,
                EmailToolSupport.BuildDeleteConfirmPrompt(account, uid, subject: null),
                null));
        }

        return GuardedAsync(async () =>
        {
            var folder = EmailToolSupport.TryGetOptionalString(args, "folder", out var f) ? f : null;
            await Bridge.DeleteAsync(account, uid, folder, ct).ConfigureAwait(false);
            return new ToolResult(true, $"deleted uid={uid} on {account}", new { account, uid });
        });
    }
}

/// <summary>
/// <c>email_send</c> — master AllowEmailSend + host-side operator consent (PROP-17.1).
/// </summary>
public sealed class EmailSendTool : EmailToolBase
{
    private static readonly JsonElement Schema = EmailToolSupport.ParseSchema(
        """
        {
          "type": "object",
          "properties": {
            "account": { "type": "string", "description": "Mailbox to send from: victoria | personal | business." },
            "to": { "type": "string", "description": "Recipient(s), comma-separated." },
            "subject": { "type": "string", "description": "Subject line." },
            "body": { "type": "string", "description": "Plain-text body." },
            "cc": { "type": "string", "description": "Optional CC recipients, comma-separated." },
            "reply_to_uid": { "type": "string", "description": "When set, send as a reply to this uid (sets In-Reply-To)." },
            "folder": { "type": "string", "description": "Folder of reply_to_uid, default INBOX." },
            "confirmed": { "type": "boolean", "description": "Ignored — model cannot mint consent. Kayleigh replies yes.", "default": false }
          },
          "required": ["to", "subject", "body"]
        }
        """);

    public EmailSendTool(IEmailBridge bridge, IOptions<ToolsOptions> options)
        : this(bridge, options, new ComputerControlGate(options), consent: null)
    {
    }

    public EmailSendTool(IEmailBridge bridge, IOptions<ToolsOptions> options, IToolsAccessSettings access)
        : this(bridge, options, access, consent: null)
    {
    }

    public EmailSendTool(
        IEmailBridge bridge,
        IOptions<ToolsOptions> options,
        IToolsAccessSettings access,
        IOperatorConsentGate? consent)
        : base(bridge, options, access, consent)
    {
    }

    public override ToolDefinition Definition { get; } = new(
        Name: "email_send",
        Description: "Send or reply to email. First call returns a confirmation prompt; only sends after Kayleigh replies yes (model cannot mint confirmed).",
        Parameters: Schema);

    public override Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!SendAllowed)
            return Task.FromResult(new ToolResult(false, EmailToolSupport.SendDeniedMessage, null));

        args = EmailToolSupport.StripModelConfirmed(args);

        if (!EmailToolSupport.TryGetRequiredString(args, "to", out var toRaw, out var err))
            return Task.FromResult(err!);
        if (!EmailToolSupport.TryGetRequiredString(args, "subject", out var subject, out err))
            return Task.FromResult(err!);
        if (!EmailToolSupport.TryGetRequiredString(args, "body", out var body, out err))
            return Task.FromResult(err!);

        var to = EmailToolSupport.SplitAddresses(toRaw);
        if (to.Count == 0)
            return Task.FromResult(new ToolResult(false, "error: 'to' needs at least one address.", null));

        var account = EmailToolSupport.ResolveAccountId(args, Bridge);
        if (string.IsNullOrWhiteSpace(account))
            return Task.FromResult(new ToolResult(false, EmailToolSupport.NoAccountsMessage, null));

        if (!EmailToolSupport.IsConfirmed(args, Consent, OperatorConsentGate.EmailSend))
        {
            Consent?.NotePending(OperatorConsentGate.EmailSend);
            return Task.FromResult(new ToolResult(
                false,
                EmailToolSupport.BuildSendConfirmPrompt(account, to, subject),
                null));
        }

        var cc = EmailToolSupport.ReadStringList(args, "cc");
        if (cc.Count == 0 && EmailToolSupport.TryGetOptionalString(args, "cc", out var ccRaw) && ccRaw is not null)
            cc = EmailToolSupport.SplitAddresses(ccRaw);

        EmailToolSupport.TryGetOptionalString(args, "reply_to_uid", out var replyUid);
        var replyFolder = EmailToolSupport.TryGetOptionalString(args, "folder", out var f) ? f : null;

        return GuardedAsync(async () =>
        {
            var messageId = await Bridge.SendAsync(
                account,
                new EmailComposeRequest(to, subject, body, cc, replyUid, replyFolder),
                ct).ConfigureAwait(false);
            return new ToolResult(
                true,
                $"sent from {account} to {string.Join(", ", to)} subject \"{subject}\"",
                new { account, to, subject, messageId, reply_to_uid = replyUid });
        });
    }
}
