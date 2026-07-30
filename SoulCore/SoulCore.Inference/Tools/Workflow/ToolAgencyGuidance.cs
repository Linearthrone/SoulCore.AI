namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Compact system guidance appended on the tool-loop path so qwen2.5 does not
/// answer workflow create/run requests in prose only (BED-162 /
/// ISSUE-20260729-001). Kept short — sits after identity/memory/emotion.
/// </summary>
public static class ToolAgencyGuidance
{
    public const string WorkflowBlock =
        "[Tools]\n" +
        "When the user asks to create a multi-step workflow (e.g. \"create a workflow to: 1) … 2) …\"), " +
        "you MUST call workflow_create with a name and steps — do not only describe the plan in prose. " +
        "Prefer setting each step's tool (e.g. recall_memory, speak) when the user names those actions; " +
        "put the step wording in description (and optional args).\n" +
        "When the user asks to run or re-run a workflow (e.g. \"run that workflow\", \"run that workflow again\"), " +
        "you MUST call workflow_execute with the workflow id from prior tool results in this session and all=true " +
        "for a full run. Do not ask which workflow if prior history already created one.\n" +
        "When the user asks for MT4 / MetaTrader status, connection, account, or bridge state " +
        "(e.g. \"what's my MT4 status?\"), you MUST call mt4_status with {}. " +
        "Do not call task_create or task_get for MT4/MetaTrader status — those are Victoria task tools only.";

    /// <summary>
    /// Appends the workflow agency block when missing. Idempotent for retries.
    /// </summary>
    public static string AppendToPreamble(string? contextPreamble)
    {
        var baseText = string.IsNullOrWhiteSpace(contextPreamble)
            ? string.Empty
            : contextPreamble.TrimEnd();

        if (baseText.Contains("[Tools]", StringComparison.Ordinal))
            return baseText;

        if (baseText.Length == 0)
            return WorkflowBlock;

        return baseText + "\n\n" + WorkflowBlock;
    }
}
