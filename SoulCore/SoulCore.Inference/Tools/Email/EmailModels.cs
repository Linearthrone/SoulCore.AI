namespace SoulCore.Inference.Tools.Email;

/// <summary>Public account card — never includes password.</summary>
public sealed record EmailAccountInfo(
    string Id,
    string Role,
    string DisplayName,
    string Address,
    bool Enabled,
    bool Configured);

/// <summary>Inbox / search row.</summary>
public sealed record EmailMessageSummary(
    string AccountId,
    string Uid,
    string Folder,
    string From,
    string To,
    string Subject,
    DateTimeOffset Date,
    bool Unread,
    bool Flagged,
    string Snippet);

/// <summary>Full message for <c>email_read</c>.</summary>
public sealed record EmailMessageDetail(
    string AccountId,
    string Uid,
    string Folder,
    string From,
    string To,
    string Cc,
    string Subject,
    DateTimeOffset Date,
    bool Unread,
    bool Flagged,
    string Body,
    string MessageId,
    IReadOnlyList<string> AttachmentNames);

/// <summary>Outbound compose / reply.</summary>
public sealed record EmailComposeRequest(
    IReadOnlyList<string> To,
    string Subject,
    string Body,
    IReadOnlyList<string>? Cc = null,
    string? InReplyToUid = null,
    string? InReplyToFolder = null);
