namespace SoulCore.Memory;

/// <summary>
/// Victoria's own lightweight task store (BED-140). Backed by the
/// <c>victoria_tasks</c> SQLite table in the memory DB. This is <b>not</b> the
/// PM ticket system under <c>docs/agents/tasks/</c> — those are human-authored
/// orchestration tickets. Victoria's tasks are model-managed work items she
/// creates and updates via the <c>task_*</c> agent-loop tools.
/// </summary>
public interface IVictoriaTaskStore
{
    /// <summary>
    /// Insert a task with <c>status='todo'</c>. Returns the new row id.
    /// </summary>
    Task<long> CreateAsync(
        string title,
        string? description,
        string? priority,
        CancellationToken cancellationToken = default);

    /// <summary>Load a task by id, or <c>null</c> when missing.</summary>
    Task<VictoriaTask?> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update <c>status</c> (and <c>updated_at</c>). Returns <c>false</c> when
    /// the id does not exist. Throws <see cref="ArgumentException"/> for an
    /// invalid status value.
    /// </summary>
    Task<bool> UpdateStatusAsync(long id, string status, CancellationToken cancellationToken = default);

    /// <summary>
    /// List tasks, optionally filtered by <paramref name="status"/>. When
    /// <paramref name="status"/> is null/empty, returns all rows ordered by
    /// <c>updated_at DESC</c>, then <c>id DESC</c>.
    /// </summary>
    Task<IReadOnlyList<VictoriaTask>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default);
}

/// <summary>One row from <c>victoria_tasks</c>.</summary>
public sealed record VictoriaTask(
    long Id,
    string Title,
    string Description,
    string Status,
    string Priority,
    string CreatedAt,
    string UpdatedAt);
