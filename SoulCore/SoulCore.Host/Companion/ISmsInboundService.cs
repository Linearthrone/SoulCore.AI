namespace SoulCore.Host.Companion;

public sealed record SmsInboundRequest(
    string FromE164,
    string? Text,
    byte[]? ImageBytes,
    string? ImageContentType);

public sealed record SmsInboundResult(
    bool Ok,
    bool Dropped,
    string? ReplyText,
    string? MediaId,
    string? FrameId,
    string? Error,
    bool UsedStub = false,
    string? Provider = null,
    string? OutboundSmsJobId = null,
    string? OutboundMmsJobId = null);

public interface ISmsInboundService
{
    /// <summary>
    /// Allowlist → optional media store → no-tools chat turn on
    /// <c>presence-local</c> → broadcast to Presence WS so ChatDesktop sees it.
    /// </summary>
    Task<SmsInboundResult> HandleAsync(
        SmsInboundRequest request,
        CancellationToken cancellationToken = default);
}
