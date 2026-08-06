using System.Text.Json;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Model-callable <c>task_update_status</c> (BED-140). Updates
/// <c>status</c> + <c>updated_at</c> on a <c>victoria_tasks</c> row.
/// Allowed statuses (<c>todo|in_progress|done|blocked</c>) are enforced by
/// <see cref="IVictoriaTaskStore.UpdateStatusAsync"/> (<see cref="ArgumentException"/>).
/// </summary>
public sealed class TaskUpdateStatusTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IVictoriaTaskStore _tasks;

    public TaskUpdateStatusTool(IVictoriaTaskStore tasks)
    {
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "task_update_status",
        Description: "Update task status.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: task_update_status expects a JSON object with 'id' and 'status'.",
                Data: null);
        }

        if (!ToolArgParsing.TryReadPositiveId(args, Definition.Name, out var id, out var idError))
        {
            return new ToolResult(Success: false, Content: idError!, Data: null);
        }

        if (!args.TryGetProperty("status", out var statusProp) || statusProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(
                Success: false,
                Content: "error: task_update_status requires 'status' (string: todo|in_progress|done|blocked).",
                Data: null);
        }

        var status = statusProp.GetString();
        if (string.IsNullOrWhiteSpace(status))
        {
            return new ToolResult(
                Success: false,
                Content: "error: task_update_status 'status' must be non-empty (todo|in_progress|done|blocked).",
                Data: null);
        }

        var normalized = status.Trim().ToLowerInvariant();

        bool updated;
        VictoriaTask? task = null;
        try
        {
            updated = await _tasks.UpdateStatusAsync(id, normalized, ct).ConfigureAwait(false);
            if (updated)
                task = await _tasks.GetAsync(id, ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: {ex.Message}",
                Data: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: task_update_status failed: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }

        if (!updated)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: task id={id} not found.",
                Data: new { id });
        }

        var resolvedStatus = task?.Status ?? normalized;
        return new ToolResult(
            Success: true,
            Content: $"updated: id={id} status={resolvedStatus}",
            Data: task is not null ? task : new { id, status = resolvedStatus });
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "id": {
              "type": "integer",
              "description": "Task id to update."
            },
            "status": {
              "type": "string",
              "description": "todo|in_progress|done|blocked"
            }
          },
          "required": ["id", "status"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
