using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Model-callable <c>workflow_execute</c> (BED-141). Advances a workflow by
/// executing the next step (<c>all=false</c>) or all remaining steps
/// (<c>all=true</c>). When a step names a <c>tool</c>, it is dispatched via
/// <see cref="IToolRegistry"/> with an empty args object. Description-only
/// steps succeed with the description as content. Reached-end returns
/// <c>Success:false</c> with "workflow complete" (soft completion, not an error).
/// </summary>
/// <remarks>
/// Resolves <see cref="IToolRegistry"/> lazily via <see cref="IServiceProvider"/>
/// (same DI-cycle pattern as <c>ListToolsTool</c>): constructing
/// <see cref="ToolRegistry"/> from <c>IEnumerable&lt;ITool&gt;</c> must not
/// require this tool to already hold the registry singleton.
/// </remarks>
public sealed class WorkflowExecuteTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();
    private static readonly JsonElement EmptyToolArgs = JsonDocument.Parse("{}").RootElement.Clone();

    /// <summary>Tools that must not be nested as workflow steps (recursion guard).</summary>
    private static readonly HashSet<string> ForbiddenStepTools = new(StringComparer.Ordinal)
    {
        "workflow_execute",
        "workflow_create"
    };

    private readonly IVictoriaWorkflowStore _workflows;
    private readonly IServiceProvider? _provider;
    private readonly IToolRegistry? _explicitRegistry;

    /// <summary>
    /// DI ctor — resolves <see cref="IToolRegistry"/> lazily at execution time
    /// to break the singleton construction cycle (see class remarks).
    /// </summary>
    public WorkflowExecuteTool(IVictoriaWorkflowStore workflows, IServiceProvider provider)
    {
        _workflows = workflows ?? throw new ArgumentNullException(nameof(workflows));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    private WorkflowExecuteTool(IVictoriaWorkflowStore workflows, IToolRegistry registry)
    {
        _workflows = workflows ?? throw new ArgumentNullException(nameof(workflows));
        _explicitRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>Test factory — injects a registry directly (no DI).</summary>
    public static WorkflowExecuteTool CreateForTests(IVictoriaWorkflowStore workflows, IToolRegistry registry)
        => new(workflows, registry);

    public ToolDefinition Definition { get; } = new(
        Name: "workflow_execute",
        Description: "Execute the next step(s) of a workflow.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: workflow_execute expects a JSON object with an 'id' integer.",
                Data: null);
        }

        if (!WorkflowGetTool.TryReadId(args, out var id, out var idError))
        {
            // Rephrase for this tool name.
            var msg = idError!.Replace("workflow_get", "workflow_execute", StringComparison.Ordinal);
            return new ToolResult(Success: false, Content: msg, Data: null);
        }

        var all = false;
        if (args.TryGetProperty("all", out var allProp))
        {
            if (allProp.ValueKind == JsonValueKind.True) all = true;
            else if (allProp.ValueKind == JsonValueKind.False) all = false;
            else if (allProp.ValueKind == JsonValueKind.String
                     && bool.TryParse(allProp.GetString(), out var parsed))
                all = parsed;
            else
            {
                return new ToolResult(
                    Success: false,
                    Content: "error: workflow_execute 'all' must be a boolean when present.",
                    Data: null);
            }
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
                Content: $"error: workflow_execute failed: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }

        if (workflow is null)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: workflow id={id} not found.",
                Data: new { id });
        }

        if (workflow.CurrentStep >= workflow.Steps.Count)
        {
            return new ToolResult(
                Success: false,
                Content: "workflow complete",
                Data: new
                {
                    id = workflow.Id,
                    name = workflow.Name,
                    current_step = workflow.CurrentStep,
                    steps = workflow.Steps.Count,
                    complete = true
                });
        }

        var registry = _explicitRegistry ?? _provider!.GetRequiredService<IToolRegistry>();

        if (!all)
        {
            var single = await ExecuteOneStepAsync(workflow, registry, ct).ConfigureAwait(false);
            return single;
        }

        var results = new List<object>();
        var sb = new StringBuilder();
        sb.AppendLine($"workflow id={workflow.Id} execute all:");

        var cursor = workflow;
        while (cursor.CurrentStep < cursor.Steps.Count)
        {
            var stepResult = await ExecuteOneStepAsync(cursor, registry, ct).ConfigureAwait(false);
            results.Add(stepResult.Data ?? new { content = stepResult.Content, success = stepResult.Success });
            sb.Append(" - ").Append(stepResult.Content).AppendLine();

            // Soft complete mid-run should not happen; break if store advanced past end
            // or if execute returned the complete marker.
            if (!stepResult.Success && string.Equals(stepResult.Content, "workflow complete", StringComparison.Ordinal))
                break;

            var refreshed = await _workflows.GetAsync(id, ct).ConfigureAwait(false);
            if (refreshed is null)
            {
                return new ToolResult(
                    Success: false,
                    Content: $"error: workflow id={id} disappeared during execute-all.",
                    Data: null);
            }

            cursor = refreshed;
        }

        var final = await _workflows.GetAsync(id, ct).ConfigureAwait(false);
        return new ToolResult(
            Success: true,
            Content: sb.ToString().TrimEnd(),
            Data: new
            {
                id,
                name = workflow.Name,
                current_step = final?.CurrentStep ?? cursor.CurrentStep,
                complete = final is not null && final.CurrentStep >= final.Steps.Count,
                results
            });
    }

    private async Task<ToolResult> ExecuteOneStepAsync(
        VictoriaWorkflow workflow,
        IToolRegistry registry,
        CancellationToken ct)
    {
        if (workflow.CurrentStep >= workflow.Steps.Count)
        {
            return new ToolResult(
                Success: false,
                Content: "workflow complete",
                Data: new
                {
                    id = workflow.Id,
                    current_step = workflow.CurrentStep,
                    complete = true
                });
        }

        var index = workflow.CurrentStep;
        var step = workflow.Steps[index];
        object? toolData = null;
        string content;
        var success = true;

        if (string.IsNullOrWhiteSpace(step.Tool))
        {
            content = $"step {index}: {step.Description}";
        }
        else
        {
            var toolName = step.Tool!.Trim();
            if (ForbiddenStepTools.Contains(toolName))
            {
                content = $"step {index}: refused nested tool '{toolName}' — {step.Description}";
                success = false;
                toolData = new { tool = toolName, refused = true };
            }
            else
            {
                ToolResult toolResult;
                try
                {
                    toolResult = await registry.ExecuteAsync(toolName, EmptyToolArgs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    toolResult = new ToolResult(
                        Success: false,
                        Content: $"Tool '{toolName}' threw: {ex.GetType().Name}: {ex.Message}",
                        Data: null);
                }

                success = toolResult.Success;
                content = $"step {index}: tool={toolName} → {toolResult.Content} ({step.Description})";
                toolData = new
                {
                    tool = toolName,
                    tool_success = toolResult.Success,
                    tool_content = toolResult.Content,
                    tool_data = toolResult.Data
                };
            }
        }

        var nextStep = index + 1;
        try
        {
            var updated = await _workflows.SetCurrentStepAsync(workflow.Id, nextStep, ct).ConfigureAwait(false);
            if (!updated)
            {
                return new ToolResult(
                    Success: false,
                    Content: $"error: failed to advance workflow id={workflow.Id}.",
                    Data: null);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: workflow_execute advance failed: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }

        return new ToolResult(
            Success: success,
            Content: content,
            Data: new
            {
                id = workflow.Id,
                name = workflow.Name,
                step_index = index,
                description = step.Description,
                tool = step.Tool,
                current_step = nextStep,
                complete = nextStep >= workflow.Steps.Count,
                tool_result = toolData
            });
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
            },
            "all": {
              "type": "boolean",
              "description": "When true, execute all remaining steps in sequence. Default false (one step).",
              "default": false
            }
          },
          "required": ["id"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
