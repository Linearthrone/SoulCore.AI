using System.Text.Json;
using SoulCore.Inference.Presence;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools.Workflow;

/// <summary>
/// PROP-17.1: release a suspended/parked task to <c>todo</c>.
/// </summary>
public sealed class TaskReleaseTool : ITool
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """
        {"type":"object","properties":{"id":{"type":"integer"}},"required":["id"]}
        """).RootElement.Clone();

    private readonly IVictoriaTaskStore _tasks;

    public TaskReleaseTool(IVictoriaTaskStore tasks)
    {
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "task_release",
        Description: "Release a suspended or parked task to todo so it can start when its surface is up.",
        Parameters: Schema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!ToolArgParsing.TryReadPositiveId(args, Definition.Name, out var id, out var idError))
            return new ToolResult(false, idError!, null);

        var ok = await _tasks.ReleaseAsync(id, ct).ConfigureAwait(false);
        if (!ok)
            return new ToolResult(false, $"error: task id={id} not found.", null);
        var row = await _tasks.GetAsync(id, ct).ConfigureAwait(false);
        return new ToolResult(true, $"released: id={id} status=todo", row);
    }
}

/// <summary>
/// PROP-17.1: suspend a task (stop; keep notes). Clears running pointer when it matches.
/// </summary>
public sealed class TaskSuspendTool : ITool
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """
        {"type":"object","properties":{"id":{"type":"integer"},"notes":{"type":"string"}},"required":["id"]}
        """).RootElement.Clone();

    private readonly IVictoriaTaskStore _tasks;
    private readonly IAssignedWorkStore? _assigned;

    public TaskSuspendTool(IVictoriaTaskStore tasks, IAssignedWorkStore? assigned = null)
    {
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _assigned = assigned;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "task_suspend",
        Description: "Suspend a task (visible, never auto-started). Keeps notes.",
        Parameters: Schema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!ToolArgParsing.TryReadPositiveId(args, Definition.Name, out var id, out var idError))
            return new ToolResult(false, idError!, null);

        string? notes = null;
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("notes", out var n)
            && n.ValueKind == JsonValueKind.String)
            notes = n.GetString();

        var ok = await _tasks.SuspendAsync(id, notes, ct).ConfigureAwait(false);
        if (!ok)
            return new ToolResult(false, $"error: task id={id} not found.", null);

        if (_assigned?.RunningId == id)
            _assigned.Clear();

        var row = await _tasks.GetAsync(id, ct).ConfigureAwait(false);
        return new ToolResult(true, $"suspended: id={id}", row);
    }
}

/// <summary>
/// Permanently remove a task. Victoria and Kayleigh (Presence DELETE) both may call this.
/// Clears the assigned-work pointer when it matches.
/// </summary>
public sealed class TaskDeleteTool : ITool
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """
        {"type":"object","properties":{"id":{"type":"integer"}},"required":["id"]}
        """).RootElement.Clone();

    private readonly IVictoriaTaskStore _tasks;
    private readonly IAssignedWorkStore? _assigned;

    public TaskDeleteTool(IVictoriaTaskStore tasks, IAssignedWorkStore? assigned = null)
    {
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _assigned = assigned;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "task_delete",
        Description: "Permanently delete a task by id. Use when the work is gone or was filed by mistake.",
        Parameters: Schema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!ToolArgParsing.TryReadPositiveId(args, Definition.Name, out var id, out var idError))
            return new ToolResult(false, idError!, null);

        var ok = await _tasks.DeleteAsync(id, ct).ConfigureAwait(false);
        if (!ok)
            return new ToolResult(false, $"error: task id={id} not found.", null);

        if (_assigned?.RunningId == id)
            _assigned.Clear();

        return new ToolResult(true, $"deleted: id={id}", new { id, deleted = true });
    }
}
