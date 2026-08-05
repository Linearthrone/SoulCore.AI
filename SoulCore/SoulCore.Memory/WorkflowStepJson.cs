using System.Text.Json;

namespace SoulCore.Memory;

/// <summary>
/// Shared pure parse for Victoria workflow step JSON objects (TASK-172 / SLOP-160 F2).
/// Does not run description→tool inference — that stays on create/ingress only.
/// </summary>
public static class WorkflowStepJson
{
    /// <summary>
    /// Parse one step element: non-empty <c>description</c>, optional <c>tool</c> string,
    /// optional <c>args</c> object. On failure, <paramref name="error"/> is a short reason
    /// (no tool-name prefix); callers map to <c>ToolResult</c> or exceptions.
    /// </summary>
    public static bool TryParseStep(
        JsonElement el,
        int index,
        out WorkflowStep step,
        out string? error)
    {
        step = null!;
        error = null;

        if (el.ValueKind != JsonValueKind.Object)
        {
            error = $"steps[{index}] must be an object.";
            return false;
        }

        if (!el.TryGetProperty("description", out var descProp) || descProp.ValueKind != JsonValueKind.String)
        {
            error = $"steps[{index}] requires 'description' (string).";
            return false;
        }

        var description = descProp.GetString();
        if (string.IsNullOrWhiteSpace(description))
        {
            error = $"steps[{index}] 'description' must be non-empty.";
            return false;
        }

        string? tool = null;
        if (el.TryGetProperty("tool", out var toolProp))
        {
            if (toolProp.ValueKind == JsonValueKind.Null)
            {
                tool = null;
            }
            else if (toolProp.ValueKind == JsonValueKind.String)
            {
                var t = toolProp.GetString();
                if (!string.IsNullOrWhiteSpace(t))
                    tool = t.Trim();
            }
            else
            {
                error = $"steps[{index}] 'tool' must be a string when present.";
                return false;
            }
        }

        var args = default(JsonElement);
        if (el.TryGetProperty("args", out var argsProp))
        {
            if (argsProp.ValueKind == JsonValueKind.Null)
            {
                args = default;
            }
            else if (argsProp.ValueKind == JsonValueKind.Object)
            {
                args = argsProp.Clone();
            }
            else
            {
                error = $"steps[{index}] 'args' must be a JSON object when present.";
                return false;
            }
        }

        step = new WorkflowStep(description.Trim(), tool, args);
        return true;
    }
}
