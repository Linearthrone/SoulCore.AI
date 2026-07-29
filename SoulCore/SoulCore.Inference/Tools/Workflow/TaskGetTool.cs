using System.Text.Json;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Model-callable <c>task_get</c> (BED-140). Returns a <c>victoria_tasks</c>
/// row by id, or <see cref="ToolResult.Success"/> false when missing.
/// </summary>
public sealed class TaskGetTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IVictoriaTaskStore _tasks;

    public TaskGetTool(IVictoriaTaskStore tasks)
    {
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "task_get",
        Description: "Get a task by id.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: task_get expects a JSON object with an 'id' integer.",
                Data: null);
        }

        if (!TryReadId(args, out var id, out var error))
        {
            return new ToolResult(Success: false, Content: error!, Data: null);
        }

        VictoriaTask? task;
        try
        {
            task = await _tasks.GetAsync(id, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: task_get failed: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }

        if (task is null)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: task id={id} not found.",
                Data: new { id });
        }

        return new ToolResult(
            Success: true,
            Content: FormatTask(task),
            Data: task);
    }

    internal static bool TryReadId(JsonElement args, out long id, out string? error)
    {
        id = 0;
        error = null;

        if (!args.TryGetProperty("id", out var idProp))
        {
            error = "error: task_get requires 'id' (integer).";
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

        error = "error: task_get 'id' must be a positive integer.";
        id = 0;
        return false;
    }

    internal static string FormatTask(VictoriaTask task)
    {
        return $"task id={task.Id} status={task.Status} priority={task.Priority} title={task.Title}";
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "id": {
              "type": "integer",
              "description": "Task id returned by task_create."
            }
          },
          "required": ["id"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
