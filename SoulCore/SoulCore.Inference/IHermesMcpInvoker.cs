using System.Text.Json;

namespace SoulCore.Inference;

/// <summary>
/// Direct Hermes MCP tool invocation (BED-144). Distinct from the agent-loop
/// <c>CompleteWithToolsAsync</c> path: this forces a single MCP tool via
/// OpenAI-compatible <c>/v1/chat/completions</c> + <c>tool_choice</c>, then
/// translates the gateway response into a <see cref="ToolResult"/> for the
/// SoulCore tool-loop.
/// </summary>
/// <remarks>
/// Hermes v0.18.x often runs with <c>tool_execution: "server"</c> — the final
/// chat completion may have <b>no</b> client-visible <c>message.tool_calls</c>;
/// the MCP result lands in <c>message.content</c> (sometimes as leaked JSON).
/// Implementations must recover that shape rather than requiring
/// <c>tool_calls</c>.
/// </remarks>
public interface IHermesMcpInvoker
{
    /// <summary>
    /// Content returned when Hermes is disabled, health fails, or the gateway
    /// is unreachable. Tools with <c>Backend=hermes</c> surface this verbatim
    /// as <c>ToolResult(Success:false, Content:...)</c> — no silent native fallback.
    /// </summary>
    public const string UnavailableMessage = "hermes gateway unavailable";

    /// <summary>
    /// Force-invoke a Hermes-registered MCP tool and translate the response to
    /// <see cref="ToolResult"/>.
    /// </summary>
    /// <param name="mcpToolName">Exact MCP tool name (e.g. <c>computer_use</c>, <c>browser_bridge_capture_tab</c>, <c>mt4_status</c>).</param>
    /// <param name="arguments">JSON object of tool arguments (empty object allowed).</param>
    /// <param name="cancellationToken">Cancellation for health + chat round-trip.</param>
    Task<ToolResult> CallMcpToolAsync(
        string mcpToolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
