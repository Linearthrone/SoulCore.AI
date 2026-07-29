using System.Text.Json;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Model-callable <c>workflow_create</c> (BED-141). Inserts a row into
/// <c>victoria_workflows</c> with <c>current_step=0</c> and returns the new id.
/// A workflow is a named ordered list of steps (description + optional tool).
/// </summary>
public sealed class WorkflowCreateTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IVictoriaWorkflowStore _workflows;

    public WorkflowCreateTool(IVictoriaWorkflowStore workflows)
    {
        _workflows = workflows ?? throw new ArgumentNullException(nameof(workflows));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "workflow_create",
        Description: "Create a multi-step workflow.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: workflow_create expects a JSON object with 'name' and 'steps'.",
                Data: null);
        }

        if (!args.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(
                Success: false,
                Content: "error: workflow_create requires 'name' (string).",
                Data: null);
        }

        var name = nameProp.GetString();
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ToolResult(
                Success: false,
                Content: "error: workflow_create 'name' must be non-empty.",
                Data: null);
        }

        if (!args.TryGetProperty("steps", out var stepsProp) || stepsProp.ValueKind != JsonValueKind.Array)
        {
            return new ToolResult(
                Success: false,
                Content: "error: workflow_create requires 'steps' (array of objects with 'description').",
                Data: null);
        }

        if (!TryParseSteps(stepsProp, out var steps, out var parseError))
        {
            return new ToolResult(Success: false, Content: parseError!, Data: null);
        }

        long id;
        try
        {
            id = await _workflows.CreateAsync(name!, steps, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: workflow_create: {ex.Message}",
                Data: null);
        }
        catch (Exception ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: workflow_create failed: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }

        return new ToolResult(
            Success: true,
            Content: $"created: id={id} name={name!.Trim()} steps={steps.Count}",
            Data: new
            {
                id,
                name = name!.Trim(),
                steps = steps.Count,
                current_step = 0
            });
    }

    internal static bool TryParseSteps(
        JsonElement stepsProp,
        out List<WorkflowStep> steps,
        out string? error)
    {
        if (!WorkflowStepJson.TryParseArray(stepsProp, requireNonEmpty: true, out steps, out var parseError))
        {
            // Preserve prior model-facing prefixes (SLOP F2: shared parse, tool maps errors).
            error = parseError == "'steps' must contain at least one step"
                ? "error: workflow_create 'steps' must contain at least one step."
                : $"error: workflow_create {parseError}.";
            return false;
        }

        error = null;
        return true;
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "name": {
              "type": "string",
              "description": "Short name for the workflow."
            },
            "steps": {
              "type": "array",
              "description": "Ordered list of steps. Each step has a description and optional tool name to call.",
              "items": {
                "type": "object",
                "properties": {
                  "description": {
                    "type": "string",
                    "description": "What this step does."
                  },
                  "tool": {
                    "type": "string",
                    "description": "Optional tool name to call via the registry when this step executes."
                  }
                },
                "required": ["description"]
              }
            }
          },
          "required": ["name", "steps"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
