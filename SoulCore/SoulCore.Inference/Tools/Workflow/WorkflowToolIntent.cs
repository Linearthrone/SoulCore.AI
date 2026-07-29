using System.Text.RegularExpressions;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Detects high-confidence natural-language intents that should dispatch
/// Victoria workflow tools without the user naming the tool (BED-162 /
/// ISSUE-20260729-001). Used for system guidance + Ollama
/// <c>tool_choice</c> forcing on iteration 0.
/// </summary>
public static class WorkflowToolIntent
{
    /// <summary>Matched NL intent → tool to prefer.</summary>
    public enum Kind
    {
        Create,
        Execute
    }

    public readonly record struct Match(Kind Intent, string ToolName);

    // AC5: "create a workflow to: 1) recall a memory, 2) speak the memory"
    private static readonly Regex CreatePattern = new(
        @"\bcreate\s+(?:a\s+|an\s+|the\s+)?workflow\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // AC6/AC7: "run that workflow" / "run that workflow again" / "execute the workflow"
    private static readonly Regex ExecutePattern = new(
        @"\b(?:run|execute|start|resume)\s+(?:that\s+|this\s+|the\s+|my\s+)?workflow\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Returns true when <paramref name="userText"/> is a clear create or
    /// execute workflow request. Create wins over execute if both somehow match.
    /// </summary>
    public static bool TryMatch(string? userText, out Match match)
    {
        match = default;
        if (string.IsNullOrWhiteSpace(userText))
            return false;

        var text = userText.Trim();
        if (CreatePattern.IsMatch(text))
        {
            match = new Match(Kind.Create, "workflow_create");
            return true;
        }

        if (ExecutePattern.IsMatch(text))
        {
            match = new Match(Kind.Execute, "workflow_execute");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Soft-infer a nested tool name from a step description when the model
    /// omits <c>tool</c> (QA-142 AC5 phrasing: recall / speak).
    /// </summary>
    public static string? InferToolFromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var d = description.Trim().ToLowerInvariant();

        // Speak first — "speak the recalled memory" must not match recall.
        if (Regex.IsMatch(d, @"\b(speak|tts)\b")
            || d.Contains("say aloud", StringComparison.Ordinal))
            return "speak";

        if (Regex.IsMatch(d, @"\brecall\b")
            && Regex.IsMatch(d, @"\bmemor(?:y|ies|ies)?\b"))
            return "recall_memory";

        if (Regex.IsMatch(d, @"\bstore\b")
            && Regex.IsMatch(d, @"\bmemor(?:y|ies)?\b"))
            return "store_memory";

        return null;
    }
}
