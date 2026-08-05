using System.Text.Json;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Shared JSON arg helpers for Victoria task/workflow tools (TASK-172 / SLOP-160 F1).
/// </summary>
internal static class ToolArgParsing
{
    /// <summary>
    /// Reads a positive integer <c>id</c> from <paramref name="args"/> (number or digit string).
    /// Error strings include <paramref name="toolName"/> so callers need no string rewrite.
    /// </summary>
    public static bool TryReadPositiveId(
        JsonElement args,
        string toolName,
        out long id,
        out string? error)
    {
        id = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("toolName must be non-empty.", nameof(toolName));

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
