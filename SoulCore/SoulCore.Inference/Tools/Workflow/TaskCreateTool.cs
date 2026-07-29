using System.Text.Json;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Model-callable <c>task_create</c> (BED-140). Inserts a row into
/// <c>victoria_tasks</c> with <c>status='todo'</c> and returns the new id.
/// Separate from PM tickets under <c>docs/agents/tasks/</c>.
/// </summary>
public sealed class TaskCreateTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IVictoriaTaskStore _tasks;

    public TaskCreateTool(IVictoriaTaskStore tasks)
    {
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "task_create",
        Description: "Create a task for Victoria to track.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: task_create expects a JSON object with a 'title' string.",
                Data: null);
        }

        if (!args.TryGetProperty("title", out var titleProp) || titleProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(
                Success: false,
                Content: "error: task_create requires 'title' (string).",
                Data: null);
        }

        var title = titleProp.GetString();
        if (string.IsNullOrWhiteSpace(title))
        {
            return new ToolResult(
                Success: false,
                Content: "error: task_create 'title' must be non-empty.",
                Data: null);
        }

        string? description = null;
        if (args.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String)
            description = descProp.GetString();

        string? priority = null;
        if (args.TryGetProperty("priority", out var priProp) && priProp.ValueKind == JsonValueKind.String)
            priority = priProp.GetString();

        long id;
        try
        {
            id = await _tasks.CreateAsync(title!, description, priority, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: task_create failed: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }

        var resolvedPriority = string.IsNullOrWhiteSpace(priority)
            ? SqliteMemoryStore.DefaultTaskPriority
            : priority!.Trim().ToLowerInvariant();

        return new ToolResult(
            Success: true,
            Content: $"created: id={id}",
            Data: new
            {
                id,
                title = title!.Trim(),
                description = description?.Trim() ?? string.Empty,
                status = SqliteMemoryStore.DefaultTaskStatus,
                priority = resolvedPriority
            });
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "title": {
              "type": "string",
              "description": "Short title for the task."
            },
            "description": {
              "type": "string",
              "description": "Optional longer description of the task."
            },
            "priority": {
              "type": "string",
              "description": "Optional priority (default medium).",
              "default": "medium"
            }
          },
          "required": ["title"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
