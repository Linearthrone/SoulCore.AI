using System.Text;
using System.Text.Json;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Model-callable <c>task_list</c> (BED-140). Lists all
/// <c>victoria_tasks</c> rows, or filters by optional <c>status</c>.
/// </summary>
public sealed class TaskListTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IVictoriaTaskStore _tasks;

    public TaskListTool(IVictoriaTaskStore tasks)
    {
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "task_list",
        Description: "List tasks (optionally filtered by status).",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        // Allow missing/null args object — no required fields.
        string? status = null;
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("status", out var statusProp)
            && statusProp.ValueKind == JsonValueKind.String)
        {
            status = statusProp.GetString();
            if (string.IsNullOrWhiteSpace(status))
                status = null;
            else
                status = status.Trim().ToLowerInvariant();
        }
        else if (args.ValueKind is not JsonValueKind.Object and not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            return new ToolResult(
                Success: false,
                Content: "error: task_list expects a JSON object (optional 'status' string).",
                Data: null);
        }

        IReadOnlyList<VictoriaTask> rows;
        try
        {
            rows = await _tasks.ListAsync(status, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: task_list failed: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }

        if (rows.Count == 0)
        {
            var emptyMsg = status is null
                ? "no tasks."
                : $"no tasks with status={status}.";
            return new ToolResult(
                Success: true,
                Content: emptyMsg,
                Data: new { count = 0, status, tasks = Array.Empty<object>() });
        }

        return new ToolResult(
            Success: true,
            Content: FormatList(rows, status),
            Data: new { count = rows.Count, status, tasks = rows });
    }

    private static string FormatList(IReadOnlyList<VictoriaTask> rows, string? status)
    {
        var sb = new StringBuilder(64 + rows.Count * 80);
        sb.Append(rows.Count).Append(" task").Append(rows.Count == 1 ? "" : "s");
        if (status is not null)
            sb.Append(" (status=").Append(status).Append(')');
        sb.Append(':');
        foreach (var t in rows)
        {
            sb.Append("\n[").Append(t.Id).Append("] ")
              .Append(t.Status).Append(' ')
              .Append(t.Priority).Append(" — ")
              .Append(t.Title);
        }
        return sb.ToString();
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "status": {
              "type": "string",
              "description": "Optional filter: todo|in_progress|done|blocked."
            }
          }
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
