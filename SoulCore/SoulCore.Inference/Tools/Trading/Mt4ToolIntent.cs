using System.Text.RegularExpressions;

namespace SoulCore.Inference.Tools.Trading;

/// <summary>
/// Detects high-confidence natural-language intents that should dispatch
/// <c>mt4_status</c> without the user naming the tool (BED-167 /
/// ISSUE-20260729-003). Mirrors <c>WorkflowToolIntent</c>: used for system
/// guidance + Ollama <c>ForceToolName</c> on iteration 0 so models do not
/// escape to <c>task_create</c> / <c>task_get</c> on "status" phrasing.
/// </summary>
public static class Mt4ToolIntent
{
    /// <summary>Matched NL intent → tool to prefer.</summary>
    public enum Kind
    {
        Status
    }

    public readonly record struct Match(Kind Intent, string ToolName);

    // QA-145 AC4: "what's my MT4 status?" / "MT4 status" / "MetaTrader status"
    private static readonly Regex StatusWithPlatform = new(
        @"\b(?:mt4|metatrader(?:\s*\d+)?)\b[\s\S]{0,40}\bstatus\b|\bstatus\b[\s\S]{0,40}\b(?:mt4|metatrader(?:\s*\d+)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Connection / account / bridge phrasing that still means MT4 read status
    private static readonly Regex ConnectionAsk = new(
        @"\b(?:mt4|metatrader(?:\s*\d+)?)\b[\s\S]{0,48}\b(?:connect(?:ed|ion)?|account|bridge|terminal)\b|" +
        @"\b(?:connect(?:ed|ion)?|account|bridge|terminal)\b[\s\S]{0,48}\b(?:mt4|metatrader(?:\s*\d+)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Imperative / force phrasing used in QA prompts
    private static readonly Regex ExplicitTool = new(
        @"\bmt4_status\b|\bcall\s+(?:the\s+)?mt4_status\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Returns true when <paramref name="userText"/> is a clear MT4 status /
    /// connection request. Does <b>not</b> match Victoria task status prompts
    /// (e.g. "what's the status of that task?").
    /// </summary>
    public static bool TryMatch(string? userText, out Match match)
    {
        match = default;
        if (string.IsNullOrWhiteSpace(userText))
            return false;

        var text = userText.Trim();
        if (ExplicitTool.IsMatch(text)
            || StatusWithPlatform.IsMatch(text)
            || ConnectionAsk.IsMatch(text))
        {
            match = new Match(Kind.Status, "mt4_status");
            return true;
        }

        return false;
    }
}
