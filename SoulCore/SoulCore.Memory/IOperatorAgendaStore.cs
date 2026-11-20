namespace SoulCore.Memory;

/// <summary>
/// PROP-22.1: week reminders for the operator (Presence calendar). Backed by
/// <c>operator_agenda_items</c> (migration 016).
/// </summary>
public interface IOperatorAgendaStore
{
    Task<IReadOnlyList<OperatorAgendaItem>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Insert a row; <see cref="OperatorAgendaItem.Id"/> on <paramref name="item"/> is ignored. Returns the new id.</summary>
    Task<long> CreateAsync(OperatorAgendaItem item, CancellationToken cancellationToken = default);

    Task<OperatorAgendaItem?> GetAsync(long id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

/// <param name="Weekday">0 = Monday … 6 = Sunday.</param>
/// <param name="TimeLocal"><c>HH:mm</c> local time.</param>
public sealed record OperatorAgendaItem(
    long Id,
    string Title,
    int Weekday,
    string TimeLocal,
    string? Notes,
    string CreatedAt);
