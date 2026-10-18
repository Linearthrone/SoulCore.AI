using System.Text.Json;
using SoulCore.Inference.Presence;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// Model-callable <c>task_create</c> (BED-140 / PROP-17.1). Inserts a row into
/// <c>victoria_tasks</c>. Default status is <c>suspended</c> unless
/// <c>ready=true</c> (then <c>todo</c>). Remove with <c>task_delete</c>.
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
        Description: "Add one concrete to-do with a short title. Do not file Kayleigh's chat or a go-ahead. A go-ahead means work a task already on the list.",
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

        if (WorkContinuation.IsChatLoggedAsTask(title))
        {
            return new ToolResult(
                Success: false,
                Content: "error: task_create refused. That is chat, not a to-do. A go-ahead means work an existing task. Do not file her message as a new task.",
                Data: null);
        }

        string? description = null;
        if (args.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String)
            description = descProp.GetString();

        string? priority = null;
        if (args.TryGetProperty("priority", out var priProp) && priProp.ValueKind == JsonValueKind.String)
            priority = priProp.GetString();

        string? surface = null;
        if (args.TryGetProperty("surface", out var surfProp) && surfProp.ValueKind == JsonValueKind.String)
            surface = surfProp.GetString();

        var ready = false;
        if (args.TryGetProperty("ready", out var readyProp))
        {
            ready = readyProp.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.String => string.Equals(readyProp.GetString(), "true", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        string? status = null;
        if (args.TryGetProperty("status", out var stProp) && stProp.ValueKind == JsonValueKind.String)
            status = stProp.GetString();

        VictoriaTask? task;
        try
        {
            var id = await _tasks.CreateAsync(
                title!, description, priority, surface, ready, status, notes: null, ct).ConfigureAwait(false);
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
                Content: $"error: task_create failed: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }

        if (task is null)
        {
            return new ToolResult(
                Success: false,
                Content: "error: task_create failed: persisted row not found after insert.",
                Data: null);
        }

        return new ToolResult(
            Success: true,
            Content: $"created: id={task.Id}",
            Data: task);
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
            },
            "surface": {
              "type": "string",
              "description": "maestro|browser|vm|local (default local)."
            },
            "ready": {
              "type": "boolean",
              "description": "When true, status is todo; otherwise suspended (PROP-17.1).",
              "default": false
            },
            "status": {
              "type": "string",
              "description": "Optional explicit status. Overrides ready when set."
            }
          },
          "required": ["title"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
