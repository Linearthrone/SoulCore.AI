namespace SoulCore.Inference;

/// <summary>
/// Optional knobs for one <c>CompleteWithToolsAsync</c> turn (BED-162 / BED-165 / BED-168).
/// Ollama honors <see cref="ForceToolName"/> with an exclusive
/// <c>tools[]</c> (forced tool only) + hard object-form <c>tool_choice</c> on
/// OpenAI-compat <c>/v1/chat/completions</c>, and refuses to execute any other
/// tool name while force is active. Text-only under force does not end the
/// loop (BED-168): soft-dispatch <c>workflow_execute</c> when a session
/// workflow id is known, otherwise one forced retry nudge. BED-180 also
/// pre-dispatches <c>desktop_open_app</c> (no LLM wait) for pure open/launch
/// prompts. Hermes <c>CompleteWithToolsAsync</c> ignores this bag
/// (PreferHermes Avenue B routes the tool-loop to Ollama).
/// </summary>
public sealed class ToolLoopOptions
{
    /// <summary>
    /// When set and present in the advertised tools, while force is pending:
    /// (1) wires only this function in <c>tools[]</c>,
    /// (2) hard-forces <c>tool_choice: { type: function, function: { name } }</c>
    /// via Ollama <c>/v1/chat/completions</c>,
    /// (3) never executes a non-matching tool_call name (returns Success:false),
    /// (4) on text-only: soft-dispatch <c>workflow_execute</c> / <c>desktop_open_app</c>
    /// when args are known, else one retry nudge (BED-168 / BED-180).
    /// Force is consumed after the first tool_calls round is handled; later
    /// iterations restore the full tool set (auto).
    /// For <c>desktop_open_app</c>, a pure open/launch NL turn is pre-dispatched
    /// before any Ollama round-trip (BED-180).
    /// </summary>
    public string? ForceToolName { get; init; }
}
