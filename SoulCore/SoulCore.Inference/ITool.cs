using System.Text.Json;

namespace SoulCore.Inference;

/// <summary>
/// Per-tool contract. Each concrete tool (BED-131+) is a small class
/// registered as a DI singleton; <see cref="ToolRegistry"/> collects all
/// <see cref="ITool"/> instances and dispatches by <see cref="Definition"/>'s
/// <see cref="ToolDefinition.Name"/>.
/// </summary>
public interface ITool
{
    /// <summary>Static descriptor (name, description, JSON-Schema parameters) for this tool.</summary>
    ToolDefinition Definition { get; }

    /// <summary>
    /// Execute the tool with model-produced <paramref name="args"/>.
    /// Return a <see cref="ToolResult"/> rather than throwing for routine
    /// failures; the registry wraps unexpected exceptions into a failed
    /// <see cref="ToolResult"/> so the agent loop never crashes on a tool.
    /// </summary>
    Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default);
}
