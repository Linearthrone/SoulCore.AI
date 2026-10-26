namespace SoulCore.Core;

/// <summary>
/// Chat episodic text. Stored turns use <see cref="BuildFactualRecord"/>.
/// The authoring prompt remains for a private feeling line when a caller still wants one.
/// </summary>
public static class EpisodicMemoryPrompt
{
    /// <summary>Total char budget for the user payload (exchange excerpt).</summary>
    public const int TotalInputCharBudget = 800;

    /// <summary>Preferred generation cap for memory-authoring calls.</summary>
    public const int AuthorMaxTokens = 96;

    /// <summary>
    /// System/instruction for Victoria’s private memory of one exchange.
    /// </summary>
    public const string SystemInstruction =
        "Write 1–3 first-person sentences as Victoria’s private memory of this exchange. " +
        "No meta, no quotes of the full reply. Past tense. " +
        "Do not invent facts not present in the exchange.";

    private const string UserLabel = "User said:\n";
    private const string ReplyLabel = "\n\nI replied:\n";

    /// <summary>
    /// Truncated exchange payload for the memory-author user/prompt field.
    /// Combined length (labels + body) is at most <see cref="TotalInputCharBudget"/>.
    /// </summary>
    public static string BuildUserPayload(string userText, string assistantReply)
    {
        var user = (userText ?? string.Empty).Trim();
        var reply = (assistantReply ?? string.Empty).Trim();

        var overhead = UserLabel.Length + ReplyLabel.Length;
        var contentBudget = Math.Max(0, TotalInputCharBudget - overhead);

        var userPart = Truncate(user, contentBudget / 2);
        var replyPart = Truncate(reply, contentBudget - userPart.Length);

        return UserLabel + userPart + ReplyLabel + replyPart;
    }

    /// <summary>Chars kept from what Kayleigh said in a stored chat episode.</summary>
    public const int FactualUserCharLimit = 400;

    /// <summary>Chars kept from Victoria's reply in a stored chat episode.</summary>
    public const int FactualReplyCharLimit = 600;

    /// <summary>How many distinct tool names a stored chat episode will list.</summary>
    public const int FactualToolNameLimit = 12;

    /// <summary>
    /// Deterministic chat episode: what Kayleigh said, what Victoria replied, and which tools ran.
    /// This is the chronology record. Feelings stay in the journal.
    /// </summary>
    public static string BuildFactualRecord(
        string? userText,
        string? assistantReply,
        IEnumerable<string>? toolNames = null)
    {
        var user = Truncate((userText ?? string.Empty).Trim(), FactualUserCharLimit);
        var reply = Truncate((assistantReply ?? string.Empty).Trim(), FactualReplyCharLimit);
        var tools = FormatToolNames(toolNames);
        var record = "Kayleigh said: " + user + "\nI replied: " + reply;
        if (tools.Length > 0)
            record += "\nI used: " + tools;
        return record;
    }

    /// <summary>Legacy briefing template used when model authoring fails or returns empty.</summary>
    public static string BuildTemplateFallback(string userText, string assistantReply)
        => $"I heard the user say: {(userText ?? string.Empty).Trim()}\nI replied: {(assistantReply ?? string.Empty).Trim()}";

    private static string FormatToolNames(IEnumerable<string>? toolNames)
    {
        if (toolNames is null)
            return string.Empty;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var names = new List<string>();
        foreach (var name in toolNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var trimmed = name.Trim();
            if (!seen.Add(trimmed))
                continue;
            names.Add(trimmed);
            if (names.Count == FactualToolNameLimit)
                break;
        }

        return string.Join(", ", names);
    }

    /// <summary>Truncates to <paramref name="maxChars"/>; empty when max ≤ 0.</summary>
    public static string Truncate(string text, int maxChars)
    {
        if (maxChars <= 0 || string.IsNullOrEmpty(text))
            return string.Empty;
        return text.Length <= maxChars ? text : text[..maxChars];
    }
}
