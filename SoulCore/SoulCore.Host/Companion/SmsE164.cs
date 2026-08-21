using System.Text;
using System.Text.RegularExpressions;

namespace SoulCore.Host.Companion;

/// <summary>E.164 normalize + allowlist match for SMS gateway ingest (PROP-1.2).</summary>
public static class SmsE164
{
    private static readonly Regex DigitsOnly = new(@"\D", RegexOptions.Compiled);

    /// <summary>
    /// Normalize to leading <c>+</c> + digits when possible.
    /// Accepts <c>+1…</c>, <c>1…</c> (US 11-digit), or bare 10-digit US.
    /// Returns empty when input has no digits.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var trimmed = raw.Trim();
        var digits = DigitsOnly.Replace(trimmed, string.Empty);
        if (digits.Length == 0)
            return string.Empty;

        if (trimmed.StartsWith('+'))
            return "+" + digits;

        // US convenience: 10-digit → +1… ; 11-digit starting with 1 → +…
        if (digits.Length == 10)
            return "+1" + digits;
        if (digits.Length == 11 && digits[0] == '1')
            return "+" + digits;

        return "+" + digits;
    }

    /// <summary>
    /// Parse allowlist config into normalized E.164 set (Ordinal).
    /// </summary>
    public static HashSet<string> ParseAllowlist(string? csv)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(csv))
            return set;

        foreach (var part in csv.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' },
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var n = Normalize(part);
            if (n.Length > 0)
                set.Add(n);
        }

        return set;
    }

    public static bool IsAllowlisted(string? fromRaw, string? allowlistCsv)
    {
        var from = Normalize(fromRaw);
        if (from.Length == 0)
            return false;

        var set = ParseAllowlist(allowlistCsv);
        if (set.Count == 0)
            return false;

        return set.Contains(from);
    }

    /// <summary>Redact for logs: keep country-ish prefix + last 2 digits.</summary>
    public static string Redact(string? e164)
    {
        var n = Normalize(e164);
        if (n.Length <= 4)
            return "***";
        return n[..Math.Min(3, n.Length)] + new string('*', Math.Max(0, n.Length - 5)) + n[^2..];
    }
}
