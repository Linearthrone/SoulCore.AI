namespace SoulCore.Inference;

/// <summary>
/// Stub — returns empty; no network / no LLM. The tool-loop stub returns a
/// deterministic capped marker so callers that exercise the loop without a
/// model (e.g. <c>Inference:Enabled=false</c>, unit tests of host wiring)
/// never block on a network call and can detect "inference disabled" via the
/// returned text.
/// </summary>
public sealed class NullInferenceClient : IInferenceClient
{
    /// <summary>
    /// Deterministic reply for the disabled-inference path. Keeps the
    /// <c>appsettings Enabled=false</c> branch testable without a model.
    /// </summary>
    public const string ToolLoopStubReply = "[inference disabled: tool-loop not invoked]";

    public Task<string> CompleteAsync(
        string prompt,
        string? systemPreamble = null,
        CancellationToken cancellationToken = default,
        int? maxTokens = null)
        => Task.FromResult(string.Empty);

    public Task<string> CompleteWithToolsAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        IToolRegistry registry,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ToolLoopStubReply);
}
