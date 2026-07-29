using System.Text.Json;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Model-callable <c>workflow_get</c> (BED-141). Returns a
/// <c>victoria_workflows</c> row by id (including steps + current_step), or
/// <see cref="ToolResult.Success"/> false when missing.
/// </summary>
public sealed class WorkflowGetTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IVictoriaWorkflowStore _workflows;

    public WorkflowGetTool(IVictoriaWorkflowStore workflows)
    {
        _workflows = workflows ?? throw new ArgumentNullException(nameof(workflows));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "workflow_get",
        Description: "Get a workflow by id (with current step).",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: workflow_get expects a JSON object with an 'id' integer.",
                Data: null);
        }

        if (!ToolArgParsing.TryReadPositiveId(args, "workflow_get", out var id, out var error))
        {
            return new ToolResult(Success: false, Content: error!, Data: null);
        }

        VictoriaWorkflow? workflow;
        try
        {
            workflow = await _workflows.GetAsync(id, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: workflow_get failed: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }

        if (workflow is null)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: workflow id={id} not found.",
                Data: new { id });
        }

        return new ToolResult(
            Success: true,
            Content: FormatWorkflow(workflow),
            Data: workflow);
    }

    internal static string FormatWorkflow(VictoriaWorkflow workflow)
    {
        var done = workflow.CurrentStep >= workflow.Steps.Count;
        var next = done
            ? "complete"
            : $"next={workflow.CurrentStep}/{workflow.Steps.Count}";
        return $"workflow id={workflow.Id} name={workflow.Name} steps={workflow.Steps.Count} current_step={workflow.CurrentStep} ({next})";
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "id": {
              "type": "integer",
              "description": "Workflow id returned by workflow_create."
            }
          },
          "required": ["id"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
