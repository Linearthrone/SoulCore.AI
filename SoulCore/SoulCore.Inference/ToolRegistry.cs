using System.Collections.Concurrent;
using System.Text.Json;

namespace SoulCore.Inference;

/// <summary>
/// Default <see cref="IToolRegistry"/>. DI collects every registered <see cref="ITool"/>
/// as <c>IEnumerable&lt;ITool&gt;</c> via constructor injection; this builds an
/// O(1) name → tool map and dispatches <c>tool_calls</c> by <see cref="ToolDefinition.Name"/>.
/// </summary>
/// <remarks>
/// <para>
/// Host boots clean with zero tools registered — an empty <c>IEnumerable&lt;ITool&gt;</c>
/// yields an empty registry, and <see cref="GetDefinitions"/> returns an empty list.
/// <see cref="ExecuteAsync"/> on an unknown name returns a failed
/// <see cref="ToolResult"/> (does not throw) so the agent loop can feed the error
/// back to the model instead of crashing the turn.
/// </para>
/// <para>
/// Duplicate tool names are detected at construction (fail-fast) — the registry
/// is a singleton built once at Host startup, so a configuration bug surfacing
/// there is preferable to silently shadowing a tool.
/// </para>
/// </remarks>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly IReadOnlyDictionary<string, ITool> _byName;
    private readonly IReadOnlyList<ToolDefinition> _definitions;

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        var list = tools is null
            ? new List<ITool>()
            : new List<ITool>(tools);

        var byName = new ConcurrentDictionary<string, ITool>(StringComparer.Ordinal);
        var definitions = new List<ToolDefinition>(list.Count);

        foreach (var tool in list)
        {
            if (tool is null) continue;
            var name = tool.Definition.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException(
                    $"Tool of type '{tool.GetType().FullName}' has an empty Definition.Name.");
            }
            if (!byName.TryAdd(name, tool))
            {
                throw new InvalidOperationException(
                    $"Duplicate tool name '{name}'. Already registered by " +
                    $"'{byName[name].GetType().FullName}'; cannot also register " +
                    $"'{tool.GetType().FullName}'.");
            }
            definitions.Add(tool.Definition);
        }

        _byName = byName;
        _definitions = definitions;
    }

    public IReadOnlyList<ToolDefinition> GetDefinitions() => _definitions;

    public async Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || !_byName.TryGetValue(name, out var tool))
        {
            return new ToolResult(
                Success: false,
                Content: $"Unknown tool '{name ?? "<null>"}'. Available: {string.Join(", ", _byName.Keys)}.",
                Data: null);
        }

        try
        {
            return await tool.ExecuteAsync(args, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"Tool '{name}' threw: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }
    }
}
