using Microsoft.Extensions.Logging;
using SoulCore.Core.Persona;

namespace SoulCore.Host.Persona;

/// <summary>
/// Single-active persona session. Framed with a loaded map so multi-active later
/// is additive. Pack / trait edits take effect on the next chat turn.
/// </summary>
public sealed class ActivePersonaSession : IPersonaSession
{
    private readonly IPersonaPackStore _store;
    private readonly IPersonaStoreHub _stores;
    private readonly ILogger<ActivePersonaSession> _logger;
    private readonly IForegroundToolLoopGate? _toolLoops;
    private readonly object _gate = new();
    private PersonaPack _active;
    private readonly Dictionary<string, PersonaPack> _loaded =
        new(StringComparer.OrdinalIgnoreCase);

    public ActivePersonaSession(
        IPersonaPackStore store,
        IPersonaStoreHub stores,
        ILogger<ActivePersonaSession> logger,
        IForegroundToolLoopGate? toolLoops = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _stores = stores ?? throw new ArgumentNullException(nameof(stores));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _toolLoops = toolLoops;
        _active = PersonaPack.CreateBlank();
        _loaded[_active.PersonaId] = _active.Clone();
    }

    public string ActivePersonaId
    {
        get
        {
            lock (_gate)
                return _active.PersonaId;
        }
    }

    public PersonaPack GetActive()
    {
        lock (_gate)
            return _active.Clone();
    }

    public bool TryGet(string personaId, out PersonaPack pack)
    {
        pack = null!;
        if (string.IsNullOrWhiteSpace(personaId))
            return false;

        lock (_gate)
        {
            if (_loaded.TryGetValue(personaId.Trim(), out var found))
            {
                pack = found.Clone();
                return true;
            }
        }

        return false;
    }

    public IReadOnlyDictionary<string, PersonaPack> GetLoadedSnapshot()
    {
        lock (_gate)
        {
            return _loaded.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Clone(),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _store.EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        var activeId = await _store.GetActivePersonaIdAsync(cancellationToken).ConfigureAwait(false);
        var pack = await _store.GetAsync(activeId, cancellationToken).ConfigureAwait(false)
            ?? await _store.GetAsync(PersonaPack.BlankPersonaId, cancellationToken).ConfigureAwait(false)
            ?? PersonaPack.CreateBlank();

        lock (_gate)
        {
            _active = pack;
            _loaded.Clear();
            _loaded[pack.PersonaId] = pack.Clone();
        }

        // PROP-15.2: open quarantined SQLite/vector paths for the active brain.
        await _stores.SwitchActiveAsync(pack.PersonaId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Persona session ready: active={PersonaId} display={DisplayName} db={DbPath}",
            pack.PersonaId,
            pack.DisplayName,
            _stores.ResolveMemoryDbPath(pack.PersonaId));
    }

    public async Task SetActiveAsync(string personaId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        string previousId;
        lock (_gate)
            previousId = _active.PersonaId;

        await _store.SetActivePersonaIdAsync(personaId, cancellationToken).ConfigureAwait(false);
        var pack = await _store.GetAsync(personaId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Unknown personaId '{personaId}'.");

        lock (_gate)
        {
            _active = pack;
            // v1: keep only the active entry warm; map shape stays for multi-active later.
            _loaded.Clear();
            _loaded[pack.PersonaId] = pack.Clone();
        }

        // PROP-16.6: an in-flight tool loop pinned to the previous foreground dies here.
        // Background chats never register, so they keep running.
        if (!string.Equals(previousId, pack.PersonaId, StringComparison.OrdinalIgnoreCase))
            _toolLoops?.Cancel(previousId);

        await _stores.SwitchActiveAsync(pack.PersonaId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Active persona switched to {PersonaId} (next chat turn); stores reopened at {DbPath}",
            pack.PersonaId,
            _stores.ResolveMemoryDbPath(pack.PersonaId));
    }

    public async Task ReloadActiveAsync(CancellationToken cancellationToken = default)
    {
        string id;
        lock (_gate)
            id = _active.PersonaId;

        var pack = await _store.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Active persona '{id}' missing from store.");

        lock (_gate)
        {
            _active = pack;
            _loaded[pack.PersonaId] = pack.Clone();
        }
    }
}
