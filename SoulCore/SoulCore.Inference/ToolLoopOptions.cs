namespace SoulCore.Inference;

/// <summary>
/// Optional knobs for one <c>CompleteWithToolsAsync</c> turn (BED-162).
/// Ollama honors <see cref="ForceToolName"/> as wire <c>tool_choice</c> on
/// iteration 0; Hermes ignores this bag (PreferHermes path unchanged).
/// </summary>
public sealed class ToolLoopOptions
{
    /// <summary>
    /// When set and tools are advertised, force this function on the first
    /// agent-loop iteration via OpenAI/Ollama object-form
    /// <c>tool_choice: { type: function, function: { name } }</c>.
    /// Later iterations omit the force (model continues with auto).
    /// </summary>
    public string? ForceToolName { get; init; }
}
