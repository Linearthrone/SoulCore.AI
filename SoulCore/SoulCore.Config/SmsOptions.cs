namespace SoulCore.Config;

/// <summary>
/// SMS/MMS gateway ingest (PROP-1.2). Values come from env
/// (<c>SOULCORE_Sms__*</c>) — never commit real MDNs.
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
}
