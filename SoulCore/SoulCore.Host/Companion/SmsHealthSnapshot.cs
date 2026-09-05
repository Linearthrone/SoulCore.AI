using SoulCore.Config;

namespace SoulCore.Host.Companion;

/// <summary>
/// PROP-1.4: SMS fields safe for <c>/health</c> — booleans and lengths only, never raw MDNs/tokens.
/// </summary>
public static class SmsHealthSnapshot
{
    public static object Build(SmsOptions sms)
    {
        ArgumentNullException.ThrowIfNull(sms);

        var allowlist = SmsE164.ParseAllowlist(sms.KurtAllowlistE164);
        var mdnNorm = SmsE164.Normalize(sms.VictoriaMdn);

        return new
        {
            allowlistConfigured = allowlist.Count > 0,
            allowlistCount = allowlist.Count,
            victoriaMdnConfigured = mdnNorm.Length > 0,
            victoriaMdnLength = mdnNorm.Length,
            outboundEnabled = sms.OutboundEnabled,
            autoReplyEnabled = sms.AutoReplySmsEnabled,
            // Hard SEC gate: inbound SMS/MMS never enters tool-loop (CompleteAsync only).
            inboundUsesToolLoop = false
        };
    }
}
