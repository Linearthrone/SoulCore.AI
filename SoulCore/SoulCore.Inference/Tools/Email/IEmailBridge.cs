namespace SoulCore.Inference.Tools.Email;

/// <summary>
/// IMAP/SMTP mailbox surface. Tools enforce AllowEmail* gates and
/// send/delete <c>confirmed=true</c> — the bridge never sees a gated call.
/// </summary>
public interface IEmailBridge
{
    IReadOnlyList<EmailAccountInfo> ListAccounts();

    Task<IReadOnlyList<string>> ListFoldersAsync(string accountId, CancellationToken ct = default);

    Task<IReadOnlyList<EmailMessageSummary>> ListAsync(
        string accountId,
        string folder,
        int limit,
        bool unreadOnly,
        CancellationToken ct = default);

    Task<EmailMessageDetail?> GetAsync(
        string accountId,
        string uid,
        string? folder,
        CancellationToken ct = default);

    Task<IReadOnlyList<EmailMessageSummary>> SearchAsync(
        string accountId,
        string query,
        string? folder,
        int limit,
        CancellationToken ct = default);

    Task MoveAsync(
        string accountId,
        string uid,
        string destFolder,
        string? sourceFolder,
        CancellationToken ct = default);

    Task MarkAsync(
        string accountId,
        string uid,
        bool? seen,
        bool? flagged,
        string? folder,
        CancellationToken ct = default);

    Task DeleteAsync(
        string accountId,
        string uid,
        string? folder,
        CancellationToken ct = default);

    Task<string> SendAsync(
        string accountId,
        EmailComposeRequest request,
        CancellationToken ct = default);
}
