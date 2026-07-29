using System.Text.Json;
using SoulCore.Inference;

namespace SoulCore.Inference.Tools.Trading;

/// <summary>
/// Hermes MCP path for MT4 tools (BED-138) via BED-144 <see cref="IHermesMcpInvoker"/>.
/// </summary>
public sealed class HermesMt4Bridge : IMt4Bridge
{
    private readonly IHermesMcpInvoker _hermes;

    public HermesMt4Bridge(IHermesMcpInvoker hermes)
    {
        _hermes = hermes ?? throw new ArgumentNullException(nameof(hermes));
    }

    public Task<ToolResult> InvokeAsync(
        string mcpToolName,
        JsonElement args,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mcpToolName))
            return Task.FromResult(new ToolResult(false, "error: mt4 mcp tool name required", null));

        var payload = args.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? HermesToolRouting.EmptyArgs()
            : args;

        return _hermes.CallMcpToolAsync(mcpToolName.Trim(), payload, ct);
    }
}
