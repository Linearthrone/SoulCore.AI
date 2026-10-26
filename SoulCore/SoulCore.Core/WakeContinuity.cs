using System.Text;

namespace SoulCore.Core;

/// <summary>
/// Chronology Victoria is handed on every turn, including the first turn after Host restarts.
/// The conversation transcript is the current thread. Similar episodic recall stays separate.
/// </summary>
public static class WakeContinuity
{
    /// <summary>Cap for each quoted line in the preamble block.</summary>
    public const int LineCharLimit = 500;

    /// <summary>One transcript row reduced to what the preamble needs.</summary>
    public readonly record struct ExchangeLine(string Role, string Content, string? OccurredAt);

    /// <summary>
    /// Latest completed exchange. Empty when the tail has no user or assistant text.
    /// </summary>
    public static string BuildLeftOffBlock(IReadOnlyList<ExchangeLine> recentOldestFirst)
    {
        if (recentOldestFirst is null || recentOldestFirst.Count == 0)
            return string.Empty;

        string? user = null;
        string? assistant = null;
        string? at = null;

        for (var i = recentOldestFirst.Count - 1; i >= 0; i--)
        {
            var line = recentOldestFirst[i];
            if (string.IsNullOrWhiteSpace(line.Content))
                continue;

            if (assistant is null && IsAssistant(line.Role))
            {
                assistant = line.Content.Trim();
                at = string.IsNullOrWhiteSpace(line.OccurredAt) ? null : line.OccurredAt.Trim();
                continue;
            }

            if (assistant is not null && IsUser(line.Role))
            {
                user = line.Content.Trim();
                break;
            }
        }

        if (user is null && assistant is null)
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append("[Where we left off]\n");
        sb.Append("This is the current thread. When Kayleigh asks you to continue, resume from this conversation.\n");
        if (at is not null)
            sb.Append("Last exchange: ").Append(at).Append('\n');
        if (user is not null)
            sb.Append("Kayleigh: ").Append(Truncate(user)).Append('\n');
        if (assistant is not null)
            sb.Append("Victoria: ").Append(Truncate(assistant)).Append('\n');
        sb.Append('\n');
        return sb.ToString();
    }

    private static bool IsUser(string? role) =>
        string.Equals(role, "user", StringComparison.OrdinalIgnoreCase);

    private static bool IsAssistant(string? role) =>
        string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string text) =>
        text.Length <= LineCharLimit ? text : text[..LineCharLimit];
}
