namespace SoulCore.Core;

/// <summary>
/// Builds the tight prompt for model-authored first-person chat episodic memory,
/// plus the deterministic template fallback when the authoring call fails.
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

    /// <summary>Legacy briefing template used when model authoring fails or returns empty.</summary>
    public static string BuildTemplateFallback(string userText, string assistantReply)
        => $"I heard the user say: {(userText ?? string.Empty).Trim()}\nI replied: {(assistantReply ?? string.Empty).Trim()}";

    /// <summary>Truncates to <paramref name="maxChars"/>; empty when max ≤ 0.</summary>
    public static string Truncate(string text, int maxChars)
    {
        if (maxChars <= 0 || string.IsNullOrEmpty(text))
            return string.Empty;
        return text.Length <= maxChars ? text : text[..maxChars];
    }
}
