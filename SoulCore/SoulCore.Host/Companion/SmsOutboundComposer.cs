using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SoulCore.Host.Companion;

/// <summary>
/// PROP-20.7: shape outbound SMS as short answer + public source URL(s) within the 480-char pipe.
/// Long findings are returned for research-paper persistence (not private journals).
/// </summary>
public sealed record SmsOutboundShape(
    string OutboundText,
    string? LongFindingsForPaper,
    IReadOnlyList<string> PublicUrlsIncluded);

public static class SmsOutboundComposer
{
    public const int MaxSmsChars = 480;

    private static readonly Regex HttpUrlRegex = new(
        @"https?://[^\s<>""')\]]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Compose carrier body: answer + public URL(s), truncated to <see cref="MaxSmsChars"/>.
    /// When the full answer cannot fit (or was longer than the pipe),
    /// <see cref="SmsOutboundShape.LongFindingsForPaper"/> holds the untruncated findings.
    /// </summary>
    public static SmsOutboundShape Compose(string? answer, IEnumerable<string?>? publicUrls)
    {
        var urls = NormalizePublicUrls(publicUrls);
        var answerClean = (answer ?? string.Empty).Trim();
        if (answerClean.Length == 0 && urls.Count == 0)
            return new SmsOutboundShape(string.Empty, null, urls);

        // Strip any accidental internal paper/journal addresses from the answer text.
        answerClean = StripInternalAddresses(answerClean);

        var urlBlock = urls.Count == 0
            ? string.Empty
            : "\n" + string.Join("\n", urls);

        var combined = answerClean + urlBlock;
        if (combined.Length <= MaxSmsChars)
        {
            // Still persist a paper when the raw answer was already over the pipe before URL attach.
            string? longBody = answerClean.Length > MaxSmsChars ? answerClean : null;
            return new SmsOutboundShape(combined, longBody, urls);
        }

        var budgetForAnswer = MaxSmsChars - urlBlock.Length;
        if (budgetForAnswer < 1)
        {
            // URLs alone overflow — keep as many full public URLs as fit.
            var fitted = FitUrls(urls, MaxSmsChars);
            var longFindings = string.IsNullOrWhiteSpace(answerClean) ? null : answerClean;
            return new SmsOutboundShape(fitted, longFindings, urls);
        }

        var truncatedAnswer = TruncateForCarrier(answerClean, budgetForAnswer);
        var outbound = truncatedAnswer + urlBlock;
        var longFindingsForPaper = answerClean.Length > budgetForAnswer ? answerClean : null;
        return new SmsOutboundShape(outbound, longFindingsForPaper, urls);
    }

    /// <summary>
    /// Collect public http(s) URLs opened during the SMS tool loop (browser_navigate args/results).
    /// Internal paper/journal addresses are excluded.
    /// </summary>
    public static IReadOnlyList<string> CollectPublicUrlsFromToolTrace(
        IEnumerable<(string Name, JsonElement? Arguments, string? Content)>? trace)
    {
        if (trace is null)
            return Array.Empty<string>();

        var found = new List<string>();
        foreach (var entry in trace)
        {
            if (string.Equals(entry.Name, "browser_navigate", StringComparison.OrdinalIgnoreCase)
                && entry.Arguments is { } args
                && args.ValueKind == JsonValueKind.Object
                && args.TryGetProperty("url", out var urlEl)
                && urlEl.ValueKind == JsonValueKind.String)
            {
                AddIfPublic(found, urlEl.GetString());
            }

            if (!string.IsNullOrWhiteSpace(entry.Content))
            {
                foreach (Match m in HttpUrlRegex.Matches(entry.Content))
                    AddIfPublic(found, m.Value.TrimEnd('.', ',', ';'));
            }
        }

        return NormalizePublicUrls(found);
    }

    public static bool IsPublicHttpUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;
        if (string.IsNullOrWhiteSpace(uri.Host))
            return false;

        var host = uri.Host.Trim().ToLowerInvariant();
        if (host is "localhost" or "127.0.0.1" or "::1" or "[::1]")
            return false;
        if (host.EndsWith(".local", StringComparison.Ordinal))
            return false;
        if (host.StartsWith("10.", StringComparison.Ordinal)
            || host.StartsWith("192.168.", StringComparison.Ordinal)
            || host.StartsWith("169.254.", StringComparison.Ordinal))
            return false;
        if (IsCarrierInternalPath(uri))
            return false;
        return true;
    }

    private static bool IsCarrierInternalPath(Uri uri)
    {
        var path = uri.AbsolutePath ?? string.Empty;
        return path.Contains("/research/papers", StringComparison.OrdinalIgnoreCase)
               || path.Contains("/journals/", StringComparison.OrdinalIgnoreCase)
               || path.Contains("/victoria/journal", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripInternalAddresses(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        return HttpUrlRegex.Replace(text, m =>
            IsPublicHttpUrl(m.Value.TrimEnd('.', ',', ';')) ? m.Value : string.Empty);
    }

    private static IReadOnlyList<string> NormalizePublicUrls(IEnumerable<string?>? raw)
    {
        if (raw is null)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var u in raw)
            AddIfPublic(list, u);
        return list;
    }

    private static void AddIfPublic(List<string> list, string? raw)
    {
        if (!IsPublicHttpUrl(raw))
            return;
        var trimmed = raw!.Trim().TrimEnd('.', ',', ';', ')');
        if (!IsPublicHttpUrl(trimmed))
            return;
        if (list.Any(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase)))
            return;
        list.Add(trimmed);
    }

    private static string FitUrls(IReadOnlyList<string> urls, int max)
    {
        var sb = new StringBuilder();
        foreach (var u in urls)
        {
            var next = sb.Length == 0 ? u : "\n" + u;
            if (sb.Length + next.Length > max)
                break;
            sb.Append(next);
        }

        return sb.Length == 0 && urls.Count > 0
            ? TruncateForCarrier(urls[0], max)
            : sb.ToString();
    }

    /// <summary>
    /// PROP-20.11 / F2: single carrier ellipsis-truncate for SMS inbound + outbound.
    /// </summary>
    public static string TruncateForCarrier(string? s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s ?? string.Empty;
        if (max <= 1)
            return "…";
        return s[..(max - 1)].TrimEnd() + "…";
    }
}
