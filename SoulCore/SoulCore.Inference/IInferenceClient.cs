namespace SoulCore.Inference;

/// <summary>
/// Local LLM client (Ollama). Real calls via <c>OllamaInferenceClient</c>; null stub when disabled.
/// </summary>
public interface IInferenceClient
{
    /// <param name="systemPreamble">Optional Ollama <c>system</c> field (e.g. emotion influence). No secrets.</param>
    /// <param name="maxTokens">Optional override for Ollama <c>num_predict</c>; null uses configured MaxTokens.</param>
    Task<string> CompleteAsync(
        string prompt,
        string? systemPreamble = null,
        CancellationToken cancellationToken = default,
        int? maxTokens = null);

    /// <summary>
    /// Agent loop: POST <c>/api/chat</c> with the <paramref name="tools"/>
    /// array, parse <c>message.tool_calls</c>, dispatch each via
    /// <paramref name="tools"/> registry... dispatch is the host's job, not
    /// the model's. This method owns the loop: it sends <c>messages+tools</c>,
    /// receives <c>tool_calls</c>, executes them through
    /// <see cref="IToolRegistry.ExecuteAsync"/>, appends
    /// <c>{ role:"tool", name, content:result.Content }</c> results, and
    /// re-prompts until the model returns plain text or
    /// <see cref="Config.InferenceOptions.MaxToolIterations"/> is hit.
    /// <para>
    /// <paramref name="tools"/> may be empty (model decides to act with no
    /// tools available → loop is a single round-trip returning text). The
    /// single-shot <see cref="CompleteAsync"/> path is the fallback for
    /// non-tool chat and is intentionally not changed by this method.
    /// </para>
    /// <para>
    /// Shared surface — BED-127 (Hermes tool-loop) must keep
    /// <c>HermesHttpClient.CompleteWithToolsAsync</c> byte-compatible with
    /// this signature so the host can swap backends. <see cref="NullInferenceClient"/>
    /// returns a deterministic stub without network.
    /// </para>
    /// </summary>
    /// <param name="messages">Full conversation (system + user turns). The loop appends assistant + tool turns.</param>
    /// <param name="tools">Tool schemas built from <see cref="IToolRegistry.GetDefinitions"/>. May be empty.</param>
    /// <param name="registry">Tool dispatch surface. May be empty-registry (no tools callable).</param>
    /// <param name="cancellationToken">Cancellation for the whole loop.</param>
    /// <returns>Final assistant text (last non-empty turn, or a capped marker when iterations hit the cap).</returns>
    Task<string> CompleteWithToolsAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        IToolRegistry registry,
        CancellationToken cancellationToken = default);
}
