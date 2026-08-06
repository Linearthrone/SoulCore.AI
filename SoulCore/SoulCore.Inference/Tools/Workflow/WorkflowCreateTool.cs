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
        Description:
            "Create a multi-step Victoria workflow and persist it. " +
            "Call this whenever the user asks to create a workflow / multi-step plan " +
            "(e.g. \"create a workflow to: 1) recall a memory, 2) speak the memory\"). " +
            "Do not answer with prose-only plans — invoke this tool. " +
            "Each step needs a description; set tool to a registry name (recall_memory, speak, …) when the step should call a tool.",
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
        steps = new List<WorkflowStep>();
        error = null;

        if (stepsProp.GetArrayLength() == 0)
        {
            error = "error: workflow_create 'steps' must contain at least one step.";
            return false;
        }

        var index = 0;
        foreach (var el in stepsProp.EnumerateArray())
        {
            if (!WorkflowStepJson.TryParseStep(el, index, out var step, out var stepError))
            {
                error = $"error: workflow_create {stepError}";
                steps = new List<WorkflowStep>();
                return false;
            }

            // BED-162: when the model omits tool but the description clearly
            // names a known action (recall memory / speak), infer it so
            // workflow_execute nested dispatch stays useful (ISSUE-005 path).
            // Inference stays on create/ingress only — not in WorkflowStepJson.
            if (string.IsNullOrWhiteSpace(step.Tool))
            {
                var inferred = WorkflowToolIntent.InferToolFromDescription(step.Description);
                if (!string.IsNullOrWhiteSpace(inferred))
                    step = step with { Tool = inferred };
            }

            steps.Add(step);
            index++;
        }

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
              "description": "Short snake_or_kebab name for the workflow (e.g. recall_and_speak_memory)."
            },
            "steps": {
              "type": "array",
              "description": "Ordered list of steps from the user's numbered plan. Include at least one step. For 'recall a memory' use tool=recall_memory; for 'speak the memory' use tool=speak.",
              "items": {
                "type": "object",
                "properties": {
                  "description": {
                    "type": "string",
                    "description": "What this step does (user wording is fine). When tool args are omitted, description is mapped into the tool's primary string parameter at execute time."
                  },
                  "tool": {
                    "type": "string",
                    "description": "Registry tool to call when this step executes (e.g. recall_memory, speak). Strongly preferred when the user names an action that matches a tool."
                  },
                  "args": {
                    "type": "object",
                    "description": "Optional JSON object of arguments for the nested tool. Missing required string fields are filled from description."
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
