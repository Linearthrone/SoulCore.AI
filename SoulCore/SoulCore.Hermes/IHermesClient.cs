using SoulCore.Inference;

namespace SoulCore.Hermes;

/// <summary>
/// Hermes OpenAI-compatible tool-loop client.
/// Secrets (if any) via env / user-secrets only — never committed config.
/// Extends <see cref="IHermesMcpInvoker"/> so desktop/browser/trading tools
/// (BED-144) can force-invoke MCP tools through the same client instance.
/// </summary>
public interface IHermesClient : IHermesMcpInvoker
{
    /// <param name="systemPreamble">Optional system message (e.g. emotion influence). No secrets.</param>
    /// <param name="maxTokens">Optional override for completion max_tokens; null uses configured MaxTokens.</param>
    Task<string> ChatAsync(
        string message,
        string? systemPreamble = null,
        CancellationToken cancellationToken = default,
        int? maxTokens = null);

    /// <summary>
    /// Agent loop: POST <c>/v1/chat/completions</c> with the
    /// <paramref name="tools"/> array and <c>tool_choice</c>, parse
    /// OpenAI-compatible <c>choices[0].message.tool_calls</c>, dispatch each
    /// via <see cref="IToolRegistry.ExecuteAsync"/>, append
    /// <c>{ role:"tool", tool_call_id, name, content }</c> results, and
    /// re-prompt until the model returns plain text or
    /// <see cref="Config.InferenceOptions.MaxToolIterations"/> is hit.
    /// <para>
    /// Byte-compatible with <see cref="IInferenceClient.CompleteWithToolsAsync"/>
    /// (BED-126) so the host can swap Ollama / Hermes backends without changing
    /// the call site. <see cref="NullHermesClient"/> returns a deterministic
    /// stub without network.
    /// </para>
    /// <para>
    /// Includes the qwen2.5 content-embedded-JSON fallback parser
    /// (ISSUE-20260726-001): when <c>tool_calls</c> is null/empty, the loop
    /// attempts to parse <c>choices[0].message.content</c> as a JSON object
    /// matching <c>{ "name":"...", "arguments":{...} }</c> and dispatches it
    /// as a tool call when <c>name</c> matches a registered tool.
    /// </para>
    /// </summary>
    /// <param name="messages">Full conversation (system + user turns). The loop appends assistant + tool turns.</param>
    /// <param name="tools">Tool schemas built from <see cref="IToolRegistry.GetDefinitions"/>. May be empty.</param>
    /// <param name="registry">Tool dispatch surface. May be empty-registry (no tools callable).</param>
    /// <param name="cancellationToken">Cancellation for the whole loop.</param>
    /// <param name="loopOptions">
    /// Optional per-turn knobs (BED-162). Hermes ignores
    /// <see cref="ToolLoopOptions.ForceToolName"/> — PreferHermes keeps
    /// <c>HermesOptions.ToolChoice</c> only.
    /// </param>
    /// <returns>Final assistant text (last non-empty turn, or a capped marker when iterations hit the cap).</returns>
    Task<string> CompleteWithToolsAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        IToolRegistry registry,
        CancellationToken cancellationToken = default,
        ToolLoopOptions? loopOptions = null);

    /// <summary>Optional health probe (HermesHttpClient). Null stub returns empty.</summary>
    Task<string> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// BED-164 Avenue B PreferHermes preflight: Hermes is <b>MCP-only</b>
    /// (<see cref="IHermesMcpInvoker.CallMcpToolAsync"/>). Probes gateway health
    /// and API key readiness <b>without</b> sending <c>tools[]</c> /
    /// <c>CompleteWithToolsAsync</c> (hermes-agent 0.18.2 is
    /// <c>tool_execution: server</c> and does not expose client
    /// <c>tool_calls</c> for Host ITool dispatch).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown with <see cref="IHermesMcpInvoker.UnavailableMessage"/> (or a
    /// missing-key message) when PreferHermes must fail-fast.
    /// </exception>
    Task EnsureMcpReadyAsync(CancellationToken cancellationToken = default);
}
