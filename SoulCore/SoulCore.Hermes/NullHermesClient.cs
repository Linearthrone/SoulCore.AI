using System.Text.Json;
using SoulCore.Inference;

namespace SoulCore.Hermes;

/// <summary>
/// Stub — returns empty; no network / no Hermes gateway. The tool-loop stub
/// returns a deterministic capped marker so callers that exercise the loop
/// without a gateway (e.g. <c>Hermes:Enabled=false</c>, unit tests of host
/// wiring) never block on a network call and can detect "hermes disabled" via
/// the returned text. Mirrors <see cref="SoulCore.Inference.NullInferenceClient"/>'s
/// <c>ToolLoopStubReply</c> pattern.
/// </summary>
public sealed class NullHermesClient : IHermesClient
{
    /// <summary>
    /// Deterministic reply for the disabled-hermes path. Keeps the
    /// <c>appsettings Hermes:Enabled=false</c> branch testable without a gateway.
    /// </summary>
    public const string ToolLoopStubReply = "[hermes disabled: tool-loop not invoked]";

    public Task<string> ChatAsync(
        string message,
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

    /// <inheritdoc />
    /// <remarks>
    /// BED-144: disabled Hermes must not silently succeed MCP tool calls —
    /// adapters with <c>Backend=hermes</c> surface
    /// <see cref="IHermesMcpInvoker.UnavailableMessage"/>.
    /// </remarks>
    public Task<ToolResult> CallMcpToolAsync(
        string mcpToolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mcpToolName))
            throw new ArgumentException("mcpToolName must be non-empty.", nameof(mcpToolName));

        return Task.FromResult(new ToolResult(
            Success: false,
            Content: IHermesMcpInvoker.UnavailableMessage,
            Data: new { mcpToolName, hermes = "disabled" }));
    }

    public Task<string> GetHealthAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);
}
