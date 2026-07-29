using System.Text.Json;

namespace SoulCore.Memory;

/// <summary>
/// Shared pure parse for Victoria workflow step JSON (BED-161 / SLOP-160 F2).
/// Used by <c>workflow_create</c> (soft model errors) and
/// <see cref="SqliteMemoryStore"/> deserialize (exceptions). Owns the step
/// object shape: required non-empty <c>description</c> string + optional
/// <c>tool</c> string/null.
/// </summary>
public static class WorkflowStepJson
{
    /// <summary>
    /// Parse one step object from a JSON array element.
    /// On failure, <paramref name="error"/> is a short reason without a tool
    /// prefix (e.g. <c>must be an object</c>, <c>requires 'description' (string)</c>).
    /// </summary>
    public static bool TryParseElement(
        JsonElement el,
        out WorkflowStep step,
        out string? error)
    {
        step = null!;
        error = null;

        if (el.ValueKind != JsonValueKind.Object)
        {
            error = "must be an object";
            return false;
        }

        if (!el.TryGetProperty("description", out var descProp) || descProp.ValueKind != JsonValueKind.String)
        {
            error = "requires 'description' (string)";
            return false;
        }

        var description = descProp.GetString();
        if (string.IsNullOrWhiteSpace(description))
        {
            error = "'description' must be non-empty";
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
                error = "'tool' must be a string when present";
                return false;
            }
        }

        step = new WorkflowStep(description.Trim(), tool);
        return true;
    }

    /// <summary>
    /// Parse a JSON array of steps. When <paramref name="requireNonEmpty"/> is
    /// true, an empty array fails (create-tool path). Soft errors only —
    /// callers map to <c>ToolResult</c> or exceptions.
    /// </summary>
    public static bool TryParseArray(
        JsonElement stepsProp,
        bool requireNonEmpty,
        out List<WorkflowStep> steps,
        out string? error)
    {
        steps = new List<WorkflowStep>();
        error = null;

        if (stepsProp.ValueKind != JsonValueKind.Array)
        {
            error = "must be a JSON array";
            return false;
        }

        if (requireNonEmpty && stepsProp.GetArrayLength() == 0)
        {
            error = "'steps' must contain at least one step";
            return false;
        }

        var index = 0;
        foreach (var el in stepsProp.EnumerateArray())
        {
            if (!TryParseElement(el, out var step, out var elementError))
            {
                error = $"steps[{index}] {elementError}";
                steps = new List<WorkflowStep>();
                return false;
            }

            steps.Add(step);
            index++;
        }

        return true;
    }
}
