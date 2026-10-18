namespace SoulCore.Memory;

/// <summary>
/// Victoria's own lightweight task store (BED-140 / PROP-17.1). Backed by the
/// <c>victoria_tasks</c> SQLite table in the memory DB. This is <b>not</b> the
/// PM ticket system under <c>docs/agents/tasks/</c> — those are human-authored
/// orchestration tickets. Victoria's tasks are model-managed work items she
/// creates and updates via the <c>task_*</c> agent-loop tools.
/// <para>
/// PROP-17.1: statuses include <c>parked</c> and <c>suspended</c>.
/// <see cref="DeleteAsync"/> is available to Kayleigh (Presence DELETE) and
/// to Victoria via the <c>task_delete</c> tool.
/// </para>
/// </summary>
public interface IVictoriaTaskStore
{
    /// <summary>
    /// Insert a task. Default status is <c>suspended</c> unless
    /// <paramref name="ready"/> is true (then <c>todo</c>) or an explicit
    /// allowed <paramref name="status"/> is supplied.
    /// </summary>
    Task<long> CreateAsync(
        string title,
        string? description,
        string? priority,
        CancellationToken cancellationToken = default);

    /// <summary>Create with surface / ready / status / notes (PROP-17.1).</summary>
    Task<long> CreateAsync(
        string title,
        string? description,
        string? priority,
        string? surface,
        bool ready,
        string? status,
        string? notes,
        CancellationToken cancellationToken = default);

    /// <summary>Load a task by id, or <c>null</c> when missing.</summary>
    Task<VictoriaTask?> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update <c>status</c> (and <c>updated_at</c>). Returns <c>false</c> when
    /// the id does not exist. Throws <see cref="ArgumentException"/> for an
    /// invalid status value.
    /// </summary>
    Task<bool> UpdateStatusAsync(long id, string status, CancellationToken cancellationToken = default);

    /// <summary>Park a running task after identical surface failures.</summary>
    Task<bool> ParkAsync(
        long id,
        string reason,
        DateTimeOffset parkUntil,
        CancellationToken cancellationToken = default);

    /// <summary>Release a suspended/parked task to <c>todo</c> and clear park fields.</summary>
    Task<bool> ReleaseAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Suspend a task (stop scheduling; keep notes).</summary>
    Task<bool> SuspendAsync(long id, string? notes = null, CancellationToken cancellationToken = default);

    /// <summary>Record an identical surface failure; returns the new counter.</summary>
    Task<int> RecordIdenticalFailureAsync(
        long id,
        string failureKey,
        CancellationToken cancellationToken = default);

    /// <summary>Clear the identical-failure counter (e.g. after real progress).</summary>
    Task ClearFailureCounterAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Patch surface / notes without changing status.</summary>
    Task<bool> UpdateDetailsAsync(
        long id,
        string? surface = null,
        string? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List tasks, optionally filtered by <paramref name="status"/>. When
    /// <paramref name="status"/> is null/empty, returns all rows ordered by
    /// <c>updated_at DESC</c>, then <c>id DESC</c>.
    /// </summary>
    Task<IReadOnlyList<VictoriaTask>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently remove a task (Presence DELETE or <c>task_delete</c>).
    /// Returns <c>false</c> when the id is missing.
    /// </summary>
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

/// <summary>One row from <c>victoria_tasks</c>.</summary>
public sealed record VictoriaTask(
    long Id,
    string Title,
    string Description,
    string Status,
    string Priority,
    string CreatedAt,
    string UpdatedAt,
    string Surface = "local",
    string ParkReason = "",
    string? ParkUntil = null,
    int IdenticalFailures = 0,
    string LastFailureKey = "",
    string Notes = "");

/// <summary>PROP-17.1 allowed task statuses and surfaces.</summary>
public static class VictoriaTaskRules
{
    public static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "todo", "in_progress", "done", "blocked", "parked", "suspended"
    };

    public static readonly HashSet<string> AllowedSurfaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "maestro", "browser", "vm", "local"
    };

    public const int IdenticalFailureThreshold = 3;
    public static readonly TimeSpan ParkCooldown = TimeSpan.FromMinutes(45);
    public static readonly TimeSpan ReparkCooldown = TimeSpan.FromMinutes(120);

    public static bool IsSchedulable(string? status) =>
        string.Equals(status, "todo", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "in_progress", StringComparison.OrdinalIgnoreCase);

    public static bool IsNeverStarted(string? status) =>
        string.Equals(status, "parked", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "suspended", StringComparison.OrdinalIgnoreCase);
}
