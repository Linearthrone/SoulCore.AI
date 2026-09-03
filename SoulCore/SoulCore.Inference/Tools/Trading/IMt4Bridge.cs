using System.Text.Json;

namespace SoulCore.Inference.Tools.Trading;

/// <summary>
/// Backend surface for MT4 bridge tools (BED-138). SoulCore <see cref="ITool"/>
/// wrappers enforce security gates (AllowMt4Read / AllowMt4Trade / per-trade
/// confirm / SL required) <b>before</b> calling this bridge — the bridge must
/// never be invoked when a gate is closed.
/// </summary>
/// <remarks>
/// Default implementation is <see cref="LlmodHttpMt4Bridge"/> which routes to
/// LLMOD MCP HTTP on shadow (<c>Mt4Backend=llmod</c> or <c>native</c> alias).
/// Optional <c>llmod</c> HTTP bridge when <c>Mt4Backend=llmod</c>; otherwise unavailable.
/// </remarks>
public interface IMt4Bridge
{
    /// <summary>
    /// Invoke an MCP-side MT4 tool by its Hermes name (e.g. <c>mt4_status</c>,
    /// <c>mt4_execute_trade</c>). Arguments are the model-produced JSON object
    /// (minus SoulCore-only keys like <c>confirmed</c> which gates strip).
    /// </summary>
    Task<ToolResult> InvokeAsync(string mcpToolName, JsonElement args, CancellationToken ct = default);
}
