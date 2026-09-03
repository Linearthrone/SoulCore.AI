namespace SoulCore.Config;

/// <summary>
/// SMS/MMS gateway (PROP-1.2 inbound + PROP-1.3 outbound). Env: <c>SOULCORE_Sms__*</c>.
/// Never commit real MDNs.
/// </summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>
    /// Canonical Presence / ChatDesktop conversation id for One Thread.
    /// </summary>
    public string ConversationSessionId { get; set; } = "presence-local";

    /// <summary>
    /// Comma/semicolon/whitespace-separated Kurt E.164 allowlist
    /// (e.g. <c>+15551234567</c>). Unknown senders are silently dropped.
    /// Empty allowlist = deny all (fail closed).
    /// </summary>
    public string KurtAllowlistE164 { get; set; } = "";

    /// <summary>
    /// Optional Victoria gateway MDN for ops notes only — not required for ingest.
    /// Never log the raw value at Information+.
    /// </summary>
    public string VictoriaMdn { get; set; } = "";

    /// <summary>
    /// When true and inference is down, return a deterministic stub reply
    /// (mirrors ChatWs:StubWhenModelDown for gateway smoke tests).
    /// </summary>
    public bool StubWhenModelDown { get; set; } = false;

    /// <summary>
    /// PROP-1.3: enqueue SMS replies / MMS stills for the tablet gateway to send.
    /// </summary>
    public bool OutboundEnabled { get; set; } = true;

    /// <summary>
    /// After an allowlisted inbound SMS, enqueue Victoria's <c>replyText</c> as outbound SMS.
    /// </summary>
    public bool AutoReplySmsEnabled { get; set; } = true;

    /// <summary>
    /// Optional HTTP webhook the Host POSTs when a message is enqueued
    /// (<c>{id,kind,toE164,text,contentType,imageBase64?}</c>). Empty = queue-only (tablet polls).
    /// </summary>
    public string OutboundWebhookUrl { get; set; } = "";

    /// <summary>Minimum seconds between outbound SMS jobs (anti-spam).</summary>
    public int MinSecondsBetweenSms { get; set; } = 12;

    /// <summary>Minimum seconds between outbound MMS jobs (anti-spam).</summary>
    public int MinSecondsBetweenMms { get; set; } = 60;

    /// <summary>Max outbound SMS enqueues per rolling hour.</summary>
    public int MaxSmsPerHour { get; set; } = 30;

    /// <summary>Max outbound MMS enqueues per rolling hour.</summary>
    public int MaxMmsPerHour { get; set; } = 6;

    /// <summary>Drop queued jobs older than this many minutes if never acked.</summary>
    public int PendingTtlMinutes { get; set; } = 120;
}
