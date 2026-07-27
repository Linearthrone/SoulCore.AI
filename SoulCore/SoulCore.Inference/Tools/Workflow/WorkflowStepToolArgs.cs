using System.Text.Json;
using System.Text.Json.Nodes;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Builds nested tool-call arguments for a workflow step (BED-159 / ISSUE-005).
/// Prefers explicit <c>step.args</c>; fills missing primary string parameters from
/// <c>step.description</c> using the target tool's JSON Schema (and known tool →
/// param aliases). Avoids dispatching required-arg tools with empty <c>{}</c>.
/// </summary>
public static class WorkflowStepToolArgs
{
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    /// <summary>
    /// Preferred property names when mapping <c>description</c> into a string
    /// tool parameter (highest priority first).
    /// </summary>
    private static readonly string[] PreferredStringParams =
    {
        "query", "text", "content", "message", "title", "prompt", "name", "emotion", "target"
    };

    /// <summary>Fast-path aliases for tools Victoria uses in workflows today.</summary>
    private static readonly Dictionary<string, string> KnownToolPrimaryParam =
        new(StringComparer.Ordinal)
        {
            ["recall_memory"] = "query",
            ["speak"] = "text",
            ["store_memory"] = "content",
            ["set_emotion"] = "emotion",
            ["play_animation"] = "name",
            ["look_at"] = "target",
            ["task_create"] = "title",
            ["task_update_status"] = "status"
        };

    /// <summary>
    /// Resolve the JSON object to pass to <see cref="IToolRegistry.ExecuteAsync"/>.
    /// </summary>
    public static JsonElement Resolve(WorkflowStep step, IToolRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(registry);

        var toolName = step.Tool?.Trim();
        ToolDefinition? def = null;
        if (!string.IsNullOrEmpty(toolName))
        {
            foreach (var d in registry.GetDefinitions())
            {
                if (string.Equals(d.Name, toolName, StringComparison.Ordinal))
                {
                    def = d;
                    break;
                }
            }
        }

        return Resolve(step, def);
    }

    /// <summary>
    /// Resolve args given an optional tool definition (tests may pass a stub def).
    /// </summary>
    public static JsonElement Resolve(WorkflowStep step, ToolDefinition? def)
    {
        ArgumentNullException.ThrowIfNull(step);

        var root = new JsonObject();

        if (step.Args.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in step.Args.EnumerateObject())
                root[prop.Name] = JsonNode.Parse(prop.Value.GetRawText());
        }

        var description = step.Description?.Trim();
        if (!string.IsNullOrEmpty(description))
        {
            var target = PickDescriptionTarget(step.Tool, def, root);
            if (target is not null && root[target] is null)
                root[target] = description;
        }

        if (root.Count == 0)
            return EmptyObject;

        return JsonDocument.Parse(root.ToJsonString()).RootElement.Clone();
    }

    /// <summary>
    /// Choose which string property should receive <c>description</c> when that
    /// property is not already present in <paramref name="existing"/>.
    /// Returns <c>null</c> when no fill is appropriate (tool needs no string args,
    /// or all candidate slots are already set).
    /// </summary>
    public static string? PickDescriptionTarget(
        string? toolName,
        ToolDefinition? def,
        JsonObject existing)
    {
        ArgumentNullException.ThrowIfNull(existing);

        // Known alias wins when unset — covers QA-142 recall_memory / speak
        // even if the registry lookup fails for any reason.
        if (!string.IsNullOrWhiteSpace(toolName)
            && KnownToolPrimaryParam.TryGetValue(toolName.Trim(), out var known)
            && existing[known] is null)
        {
            return known;
        }

        if (def is null)
            return null;

        var schema = def.Parameters;
        if (schema.ValueKind != JsonValueKind.Object)
            return null;

        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (schema.TryGetProperty("properties", out var props)
            && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in props.EnumerateObject())
                properties[p.Name] = p.Value;
        }

        var required = new HashSet<string>(StringComparer.Ordinal);
        if (schema.TryGetProperty("required", out var req)
            && req.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in req.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    var name = el.GetString();
                    if (!string.IsNullOrEmpty(name))
                        required.Add(name!);
                }
            }
        }

        static bool IsStringSchema(JsonElement propSchema)
        {
            if (propSchema.ValueKind != JsonValueKind.Object)
                return false;
            if (!propSchema.TryGetProperty("type", out var typeEl))
                return false;
            if (typeEl.ValueKind == JsonValueKind.String)
                return string.Equals(typeEl.GetString(), "string", StringComparison.Ordinal);
            if (typeEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in typeEl.EnumerateArray())
                {
                    if (t.ValueKind == JsonValueKind.String
                        && string.Equals(t.GetString(), "string", StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        // 1) Preferred ∩ required ∩ unset ∩ string
        foreach (var name in PreferredStringParams)
        {
            if (!required.Contains(name)) continue;
            if (existing[name] is not null) continue;
            if (!properties.TryGetValue(name, out var propSchema)) continue;
            if (!IsStringSchema(propSchema)) continue;
            return name;
        }

        // 2) Any required string unset (first in schema order)
        foreach (var name in required)
        {
            if (existing[name] is not null) continue;
            if (!properties.TryGetValue(name, out var propSchema)) continue;
            if (!IsStringSchema(propSchema)) continue;
            return name;
        }

        // 3) Preferred ∩ optional unset ∩ string (helps tools with no required[])
        foreach (var name in PreferredStringParams)
        {
            if (existing[name] is not null) continue;
            if (!properties.TryGetValue(name, out var propSchema)) continue;
            if (!IsStringSchema(propSchema)) continue;
            return name;
        }

        return null;
    }
}
