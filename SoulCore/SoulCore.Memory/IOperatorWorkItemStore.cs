namespace SoulCore.Memory;

/// <summary>
/// PROP-22.1: operator-authored tasks/projects (Presence glass shell). Backed by
/// <c>operator_work_items</c> (migration 016). Does not replace <c>victoria_tasks</c>.
/// </summary>
public interface IOperatorWorkItemStore
{
    Task<IReadOnlyList<OperatorWorkItem>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Insert a row; <see cref="OperatorWorkItem.Id"/> on <paramref name="item"/> is ignored. Returns the new id.</summary>
    Task<long> CreateAsync(OperatorWorkItem item, CancellationToken cancellationToken = default);

    Task<OperatorWorkItem?> GetAsync(long id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed record OperatorWorkItem(
    long Id,
    string Kind,
    string Title,
    string? Description,
    string? Details,
    string? DeadlineUtc,
    int Priority,
    bool NoCheckin,
    string? PhaseCheckpointsJson,
    string? DeliverablesJson,
    bool NeedsExtraPermissions,
    bool OutsideCollaboration,
    string Status,
    string CreatedAt,
    string UpdatedAt);

public static class OperatorWorkItemRules
{
    public static readonly HashSet<string> AllowedKinds = new(StringComparer.Ordinal) { "task", "project" };
    public static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal) { "open", "done", "cancelled" };
}
