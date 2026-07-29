namespace SoulCore.Inference;

/// <summary>
/// Optional knobs for one <c>CompleteWithToolsAsync</c> turn (BED-162 / BED-165).
/// Ollama honors <see cref="ForceToolName"/> on iteration 0 with an exclusive
/// <c>tools[]</c> (forced tool only) + hard object-form <c>tool_choice</c> on
/// OpenAI-compat <c>/v1/chat/completions</c>, and refuses to execute any other
/// tool name on that iteration. Hermes <c>CompleteWithToolsAsync</c> ignores
/// this bag (PreferHermes Avenue B routes the tool-loop to Ollama).
/// </summary>
public sealed class ToolLoopOptions
{
    /// <summary>
    /// When set and present in the advertised tools, iteration 0:
    /// (1) wires only this function in <c>tools[]</c>,
    /// (2) hard-forces <c>tool_choice: { type: function, function: { name } }</c>
    /// via Ollama <c>/v1/chat/completions</c>,
    /// (3) never executes a non-matching tool_call name (returns Success:false).
    /// Later iterations restore the full tool set (auto).
    /// </summary>
    public string? ForceToolName { get; init; }
}
