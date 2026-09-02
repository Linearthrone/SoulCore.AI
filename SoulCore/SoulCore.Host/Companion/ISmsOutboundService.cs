namespace SoulCore.Host.Companion;

public enum SmsOutboundKind
{
    Sms = 0,
    Mms = 1
}

public enum SmsOutboundStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    DroppedRateLimit = 3
}

/// <summary>One outbound carrier job for the tablet gateway (PROP-1.3).</summary>
public sealed class SmsOutboundJob
{
    public required string Id { get; init; }
    public required SmsOutboundKind Kind { get; init; }
    public required string ToE164 { get; init; }
    public string? Text { get; init; }
    public string? ContentType { get; init; }
    public byte[]? ImageBytes { get; set; }
    public DateTimeOffset CreatedUtc { get; init; }
    public SmsOutboundStatus Status { get; set; }
    public string? Error { get; set; }
    public string? Source { get; init; }
}

public sealed record SmsOutboundEnqueueResult(
    bool Ok,
    string? JobId,
    bool RateLimited,
    string? Error);

public interface ISmsOutboundService
{
    /// <summary>Enqueue a short SMS to an allowlisted E.164 (Kurt).</summary>
    Task<SmsOutboundEnqueueResult> EnqueueSmsAsync(
        string toE164,
        string text,
        string? source = null,
        CancellationToken cancellationToken = default);

    /// <summary>Enqueue an MMS still (image + optional caption) to allowlisted Kurt.</summary>
    Task<SmsOutboundEnqueueResult> EnqueueMmsAsync(
        string toE164,
        byte[] imageBytes,
        string contentType,
        string? caption = null,
        string? source = null,
        CancellationToken cancellationToken = default);

    /// <summary>Prefer Victoria browser frame, else Presence desktop hub; MMS to first allowlisted Kurt.</summary>
    Task<SmsOutboundEnqueueResult> EnqueueScreenshotMmsToKurtAsync(
        string? caption = null,
        string? source = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<SmsOutboundJob> ListPending(int limit = 10);

    bool TryAck(string jobId, bool success, string? error = null);

    /// <summary>Test/observability: jobs recorded this process (including rate-limited drops).</summary>
    IReadOnlyList<SmsOutboundJob> ListRecent(int limit = 50);
}
