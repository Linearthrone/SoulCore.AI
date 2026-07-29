using System.Text.Json;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Model-callable <c>task_update_status</c> (BED-140). Updates
/// <c>status</c> + <c>updated_at</c> on a <c>victoria_tasks</c> row.
/// Validates status against <c>todo|in_progress|done|blocked</c>.
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

        if (!ToolArgParsing.TryReadPositiveId(args, "task_update_status", out var id, out var idError))
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
        if (!SqliteMemoryStore.AllowedTaskStatuses.Contains(normalized))
        {
            return new ToolResult(
                Success: false,
                Content: $"error: invalid status '{status}'. Allowed: todo, in_progress, done, blocked.",
                Data: null);
        }

        bool updated;
        try
        {
            updated = await _tasks.UpdateStatusAsync(id, normalized, ct).ConfigureAwait(false);
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

        return new ToolResult(
            Success: true,
            Content: $"updated: id={id} status={normalized}",
            Data: new { id, status = normalized });
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
