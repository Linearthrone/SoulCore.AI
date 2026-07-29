using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Detects high-confidence natural-language intents that should dispatch
/// Victoria workflow tools without the user naming the tool (BED-162 /
/// ISSUE-20260729-001). Used for system guidance + Ollama
/// <c>tool_choice</c> forcing on iteration 0. BED-168 also extracts a
/// session workflow id for ForceTool soft-dispatch of <c>workflow_execute</c>.
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

    // Tool result / prose forms: "created: id=42 …", "workflow id=42 …"
    private static readonly Regex WorkflowIdInText = new(
        @"(?:created:\s*id=|workflow\s+id=)(\d+)\b",
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

    /// <summary>
    /// BED-168: find the most recent workflow id in session tool results /
    /// prior tool-call arguments so ForceTool can soft-dispatch
    /// <c>workflow_execute</c> when the model returns clarification prose.
    /// </summary>
    /// <param name="messageTexts">Newest-last content strings (tool / assistant / user).</param>
    /// <param name="toolCallArgumentObjects">Optional prior <c>function.arguments</c> objects.</param>
    public static bool TryFindLatestWorkflowId(
        IEnumerable<string?>? messageTexts,
        IEnumerable<JsonElement?>? toolCallArgumentObjects,
        out long id)
    {
        id = 0;
        long? found = null;

        if (toolCallArgumentObjects is not null)
        {
            foreach (var args in toolCallArgumentObjects)
            {
                if (args is not { } el || el.ValueKind != JsonValueKind.Object)
                    continue;
                if (!el.TryGetProperty("id", out var idProp))
                    continue;
                if (TryReadLongId(idProp, out var parsed))
                    found = parsed;
            }
        }

        if (messageTexts is not null)
        {
            foreach (var text in messageTexts)
            {
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                foreach (System.Text.RegularExpressions.Match m in WorkflowIdInText.Matches(text))
                {
                    if (long.TryParse(
                            m.Groups[1].Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var parsed)
                        && parsed > 0)
                    {
                        found = parsed;
                    }
                }
            }
        }

        if (found is null || found.Value <= 0)
            return false;

        id = found.Value;
        return true;
    }

    private static bool TryReadLongId(JsonElement idProp, out long id)
    {
        id = 0;
        switch (idProp.ValueKind)
        {
            case JsonValueKind.Number:
                if (idProp.TryGetInt64(out id) && id > 0)
                    return true;
                if (idProp.TryGetDouble(out var d) && d > 0 && Math.Abs(d - Math.Truncate(d)) < double.Epsilon)
                {
                    id = (long)d;
                    return id > 0;
                }
                return false;
            case JsonValueKind.String:
                return long.TryParse(
                           idProp.GetString(),
                           NumberStyles.Integer,
                           CultureInfo.InvariantCulture,
                           out id)
                       && id > 0;
            default:
                return false;
        }
    }
}
