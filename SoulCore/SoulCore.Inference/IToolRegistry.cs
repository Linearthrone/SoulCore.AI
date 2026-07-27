using System.Text.Json;

namespace SoulCore.Inference;

/// <summary>
/// Tool registry — the dispatch surface both inference paths
/// (Ollama native <c>/api/chat</c> and Hermes <c>/v1/chat/completions</c>)
/// converge on. The Host builds the <c>tools[]</c> array sent to the model
/// from <see cref="GetDefinitions"/>, dispatches <c>tool_calls</c> via
/// <see cref="ExecuteAsync"/>, and feeds results back into the agent loop.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// All registered tool definitions, in registration order.
    /// Returns an empty list when no tools are registered (valid — Host
    /// boots clean with zero tools).
    /// </summary>
    IReadOnlyList<ToolDefinition> GetDefinitions();

    /// <summary>
    /// Dispatch a tool call by <see cref="ToolDefinition.Name"/>.
    /// Implementations must tolerate arbitrary <paramref name="args"/> JSON
    /// (the model produces it) and return a <see cref="ToolResult"/> rather
    /// than throwing for routine failures (e.g. unknown tool, bad args).
    /// </summary>
    /// <param name="name">Exact <see cref="ToolDefinition.Name"/> to dispatch.</param>
    /// <param name="args">Model-produced arguments as a JSON value.</param>
    /// <param name="cancellationToken">Cancellation for the agent loop.</param>
    Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default);
}

/// <summary>
/// OpenAI/Ollama tool descriptor. <see cref="Parameters"/> holds a JSON Schema
/// object (e.g. <c>{" "type":"object", "properties": {...}, "required": [...] }</c>)
/// serialized to <see cref="JsonElement"/> so it can be forwarded verbatim into
/// the <c>tools[]</c> array sent to the model.
/// </summary>
public sealed record ToolDefinition(string Name, string Description, JsonElement Parameters);

/// <summary>
/// Result of a tool execution. <see cref="Content"/> is the human/agent-readable
/// string fed back into the chat as the <c>role:"tool"</c> message; <see cref="Data"/>
/// is an optional structured payload for host-side use (not serialized to the model).
/// </summary>
public sealed record ToolResult(bool Success, string Content, object? Data = null);
