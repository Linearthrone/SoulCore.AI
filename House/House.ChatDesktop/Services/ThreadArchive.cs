namespace House.ChatDesktop.Services;

/// <summary>
/// PROP-16.3b: archive posts a caller-built summary. A failed post does not hide the thread.
/// No model call.
/// </summary>
public static class ThreadArchive
{
    public const int SummaryMaxCharacters = 2000;

    public static string BuildSummary(string? lastUserLine, string? lastAssistantLine)
    {
        var user = (lastUserLine ?? string.Empty).Trim();
        var assistant = (lastAssistantLine ?? string.Empty).Trim();
        string summary;
        if (user.Length == 0 && assistant.Length == 0)
            summary = "Archived.";
        else if (user.Length == 0)
            summary = assistant;
        else if (assistant.Length == 0)
            summary = user;
        else
            summary = user + "\n" + assistant;

        if (summary.Length <= SummaryMaxCharacters)
            return summary;
        return summary[..SummaryMaxCharacters];
    }

    /// <summary>
    /// Posts the summary for <paramref name="personaId"/>. On success, records the hide mark.
    /// On a false result or a throw, the mark stays where it was.
    /// </summary>
    public static async Task<ThreadArchiveResult> ExecuteAsync(
        ChatHistoryStore store,
        string personaId,
        Func<string, CancellationToken, Task<bool>> post,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(post);

        if (string.IsNullOrWhiteSpace(personaId))
            return new ThreadArchiveResult(false, string.Empty, string.Empty);

        var key = ChatTranscriptKey.ForPersona(personaId);
        var summary = BuildSummary(
            store.LatestVisibleText(key, "user"),
            store.LatestVisibleText(key, "assistant"));

        bool posted;
        try
        {
            posted = await post(summary, cancellationToken);
        }
        catch (Exception)
        {
            return new ThreadArchiveResult(false, summary, key);
        }

        if (!posted)
            return new ThreadArchiveResult(false, summary, key);

        store.HideThroughLatest(key);
        return new ThreadArchiveResult(true, summary, key);
    }
}

public sealed record ThreadArchiveResult(bool Hidden, string Summary, string TranscriptKey);
