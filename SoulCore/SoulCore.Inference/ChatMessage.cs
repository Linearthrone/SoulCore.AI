using System.Text.Json;

namespace SoulCore.Inference;

/// <summary>
/// One chat message in the agent loop. Maps to Ollama <c>/api/chat</c> and
/// OpenAI <c>/v1/chat/completions</c> message shapes. Both inference paths
/// (Ollama native, BED-126; Hermes, BED-127) consume this type so the host
/// builds the conversation once and forwards it to either backend.
/// <para>
/// For tool-result messages, set <see cref="Role"/> to <c>"tool"</c> and
/// <see cref="Name"/> to the tool name (Ollama requires <c>name</c> on tool
/// messages). For assistant tool-call messages, set <see cref="ToolCalls"/>
/// and leave <see cref="Content"/> as the (possibly empty) assistant text.
/// </para>
/// </summary>
public sealed record ChatMessage
{
    /// <summary>One of <c>system</c>, <c>user</c>, <c>assistant</c>, <c>tool</c>.</summary>
    public string Role { get; init; } = "user";

    /// <summary>Message text. May be null/empty for assistant tool-call turns.</summary>
    public string? Content { get; init; }

    /// <summary>Tool name — required when <see cref="Role"/> is <c>"tool"</c>.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Tool calls the assistant emitted. Set only on <c>assistant</c> turns;
    /// null for plain text turns. Forwarded verbatim back to the model on
    /// re-prompt so the conversation shape matches what the model produced.
    /// </summary>
    public IReadOnlyList<ChatToolCall>? ToolCalls { get; init; }
}

/// <summary>
/// One assistant tool call. Ollama/OpenAI shape: <c>{ "function": { "name", "arguments" } }</c>.
/// <see cref="Function.Arguments"/> is a parsed JSON value (object form); the
/// host normalizes string-form arguments defensively before constructing this.
/// </summary>
public sealed record ChatToolCall
{
    public ChatFunctionCall Function { get; init; } = new();
}

/// <summary>Function name + parsed arguments for a tool call.</summary>
public sealed record ChatFunctionCall
{
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Parsed arguments as a JSON value (object form). Null/Undefined when the
    /// model emitted no arguments; the registry receives <c>default(JsonElement)</c>
    /// in that case.
    /// </summary>
    public JsonElement? Arguments { get; init; }
}
