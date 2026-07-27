namespace SoulCore.Core.Abstractions;

/// <summary>
/// Narrow read-only stats surface for <c>system_info</c> (BED-133). Lives in
/// Core.Abstractions so the Inference project (which references Core but not
/// Memory) can consume it without taking a Memory dependency. Implemented
/// additively by <c>SqliteMemoryStore</c> — does not extend <c>IMemoryStore</c>
/// so existing test stubs are unaffected.
/// </summary>
public interface IMemoryStats
{
    /// <summary>True when the backing store is open and queryable.</summary>
    bool IsOpen { get; }

    /// <summary>Count of non-quarantined episodic memories, or 0 on error.</summary>
    Task<long> CountEpisodicAsync(CancellationToken cancellationToken = default);
}
