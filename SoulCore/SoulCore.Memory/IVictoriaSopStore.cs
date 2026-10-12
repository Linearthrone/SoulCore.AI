namespace SoulCore.Memory;

/// <summary>
/// PROP-17.2 SOP store — charter-shaped procedures (title, when, steps, don'ts).
/// Authority order: tool code → charter → operator settings → her notes as recall.
/// An SOP cannot flip a host gate, mint <c>confirmed</c>, or call <c>guest_shell</c>.
/// </summary>
public interface IVictoriaSopStore
{
    Task<long> CreateAsync(
        string title,
        string whenText,
        string steps,
        string donts,
        CancellationToken cancellationToken = default);

    Task<VictoriaSop?> GetAsync(long id, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        long id,
        string? title = null,
        string? whenText = null,
        string? steps = null,
        string? donts = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VictoriaSop>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>One row from <c>victoria_sops</c>.</summary>
public sealed record VictoriaSop(
    long Id,
    string Title,
    string WhenText,
    string Steps,
    string Donts,
    string CreatedAt,
    string UpdatedAt);
