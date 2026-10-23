using SoulCore.Core.Abstractions;
using SoulCore.Core.Charter;
using SoulCore.Memory;

namespace SoulCore.Host.Persona;

/// <summary>
/// PROP-15.2: persona-keyed memory / charter / journal store access.
/// v1 keeps a single active open bundle; every accessor takes <c>personaId</c>
/// so multi-active later is additive (map lookup) rather than a rewrite.
/// </summary>
public interface IPersonaStoreHub : IAsyncDisposable
{
    /// <summary>Persona id whose SQLite bundle is currently open.</summary>
    string ActivePersonaId { get; }

    /// <summary>Resolved absolute memory DB path for <paramref name="personaId"/>.</summary>
    string ResolveMemoryDbPath(string personaId);

    /// <summary>Resolved absolute vector directory for <paramref name="personaId"/>.</summary>
    string ResolveVectorDirectory(string personaId);

    /// <summary>
    /// Tear down the prior active bundle (if any) and open quarantined stores
    /// for <paramref name="personaId"/>. SoulLoop / charter / chat must use
    /// the resulting active stores only.
    /// </summary>
    Task SwitchActiveAsync(string personaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Episodic store for <paramref name="personaId"/>.
    /// PROP-16.3: opens that persona's database even when a different persona
    /// is the active bundle. Charter, journal, and the other accessors stay
    /// single-active.
    /// </summary>
    IMemoryStore GetMemoryStore(string personaId);
    IMemoryStats GetMemoryStats(string personaId);
    IEmotionState GetEmotionState(string personaId);
    IVictoriaJournalStore GetJournalStore(string personaId);
    IVictoriaTaskStore GetTaskStore(string personaId);
    IVictoriaWorkflowStore GetWorkflowStore(string personaId);
    /// <summary>
    /// Durable transcript for <paramref name="personaId"/>.
    /// PROP-16.6: opens that persona's database even when a different persona
    /// is the foreground bundle, so a background chat can append its own thread.
    /// </summary>
    IChatTranscriptStore GetTranscriptStore(string personaId);
    ICharter GetCharter(string personaId);

    /// <summary>
    /// Active charter service for Host health / identity admin reads
    /// (<see cref="CharterService.GetLockCountsAsync"/> etc.).
    /// </summary>
    CharterService GetCharterService(string personaId);
}
