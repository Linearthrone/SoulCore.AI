using System.Text.Json;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Shared arg parsing for Victoria task/workflow tools (BED-161 / SLOP-160 F1).
/// Keeps model-facing <c>id</c> errors consistent across tools.
/// </summary>
internal static class ToolArgParsing
{
    /// <summary>
    /// Reads a positive integer <c>id</c> from <paramref name="args"/> (number or
    /// numeric string). On failure, <paramref name="error"/> is a model-facing
    /// string that includes <paramref name="toolName"/>.
    /// </summary>
    public static bool TryReadPositiveId(
        JsonElement args,
        string toolName,
        out long id,
        out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        id = 0;
        error = null;

        if (!args.TryGetProperty("id", out var idProp))
        {
            error = $"error: {toolName} requires 'id' (integer).";
            return false;
        }

        if (idProp.ValueKind == JsonValueKind.Number && idProp.TryGetInt64(out id) && id > 0)
            return true;

        if (idProp.ValueKind == JsonValueKind.String
            && long.TryParse(idProp.GetString(), out id)
            && id > 0)
        {
            return true;
        }

        error = $"error: {toolName} 'id' must be a positive integer.";
        id = 0;
        return false;
    }
}
