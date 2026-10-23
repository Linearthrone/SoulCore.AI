using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Charter;
using SoulCore.Memory;
using SoulCore.Memory.Repositories;

namespace SoulCore.Host.Persona;

/// <summary>
/// Opens / tears down per-persona quarantined SQLite (+ vector dir) stores.
/// Single-active v1; accessors remain keyed by <c>personaId</c>.
/// </summary>
public sealed class PersonaStoreHub : IPersonaStoreHub
{
    private readonly string _personasRoot;
    private readonly ILogger<PersonaStoreHub> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sideGate = new();
    private readonly Dictionary<string, SqliteMemoryStore> _sideMemory = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SideTranscript> _sideTranscript = new(StringComparer.OrdinalIgnoreCase);
    private PersonaStoreBundle? _active;
    private bool _disposed;

    public PersonaStoreHub(IOptions<PersonaOptions> options, ILogger<PersonaStoreHub> logger)
    {
        _personasRoot = (options?.Value ?? new PersonaOptions()).ResolveRootDirectory();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Test helper: construct against an explicit personas root.</summary>
    public PersonaStoreHub(string personasRoot, ILogger<PersonaStoreHub>? logger = null)
    {
        _personasRoot = Path.GetFullPath(personasRoot);
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PersonaStoreHub>.Instance;
    }

    public string PersonasRoot => _personasRoot;

    public string ActivePersonaId
    {
        get
        {
            ThrowIfDisposed();
            var bundle = _active
                ?? throw new InvalidOperationException("PersonaStoreHub has no active persona yet. Call SwitchActiveAsync first.");
            return bundle.PersonaId;
        }
    }

    public string ResolveMemoryDbPath(string personaId) =>
        PersonaMemoryPaths.ResolveMemoryDbPath(_personasRoot, personaId);

    public string ResolveVectorDirectory(string personaId) =>
        PersonaMemoryPaths.ResolveVectorDirectory(_personasRoot, personaId);

    public async Task SwitchActiveAsync(string personaId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        var id = PersonaMemoryPaths.NormalizePersonaId(personaId);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_active is not null
                && string.Equals(_active.PersonaId, id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Drop side connections before the active bundle opens the same file.
            CloseSideMemory(id);
            CloseSideTranscript(id);

            var next = new PersonaStoreBundle(id, _personasRoot);
            var previous = _active;
            _active = next;

            if (previous is not null)
            {
                try
                {
                    await previous.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Persona store tear-down failed for {PersonaId} (new active={NewId})",
                        previous.PersonaId,
                        id);
                }
            }

            _logger.LogInformation(
                "Persona stores active={PersonaId} db={DbPath} vectorDir={VectorDir}",
                id,
                next.DatabasePath,
                next.VectorDirectory);
        }
        finally
        {
            _gate.Release();
        }
    }

    public IMemoryStore GetMemoryStore(string personaId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        var id = PersonaMemoryPaths.NormalizePersonaId(personaId);

        // Active bundle wins so chat / SoulLoop keep the single open session.
        var active = _active;
        if (active is not null && string.Equals(active.PersonaId, id, StringComparison.OrdinalIgnoreCase))
            return active.Episodic;

        // PROP-16.3: archive must write this persona's file while another persona is active.
        // Other hub accessors stay on RequireBundle and still refuse a non-active id.
        lock (_sideGate)
        {
            ThrowIfDisposed();
            active = _active;
            if (active is not null && string.Equals(active.PersonaId, id, StringComparison.OrdinalIgnoreCase))
                return active.Episodic;

            if (_sideMemory.TryGetValue(id, out var cached))
                return cached;

            var path = ResolveMemoryDbPath(id);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var opened = new SqliteMemoryStore(path);
            _sideMemory[id] = opened;
            return opened;
        }
    }

    public IMemoryStats GetMemoryStats(string personaId) => RequireBundle(personaId).Episodic;

    public IEmotionState GetEmotionState(string personaId) => RequireBundle(personaId).Emotion;

    public IVictoriaJournalStore GetJournalStore(string personaId) => RequireBundle(personaId).Journals;

    public IVictoriaTaskStore GetTaskStore(string personaId) => RequireBundle(personaId).Tasks;

    public IVictoriaWorkflowStore GetWorkflowStore(string personaId) => RequireBundle(personaId).Workflows;

    public IChatTranscriptStore GetTranscriptStore(string personaId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        var id = PersonaMemoryPaths.NormalizePersonaId(personaId);

        var active = _active;
        if (active is not null && string.Equals(active.PersonaId, id, StringComparison.OrdinalIgnoreCase))
            return active.Transcript;

        // PROP-16.6: a background chat appends this persona's transcript without switching foreground.
        lock (_sideGate)
        {
            ThrowIfDisposed();
            active = _active;
            if (active is not null && string.Equals(active.PersonaId, id, StringComparison.OrdinalIgnoreCase))
                return active.Transcript;

            if (_sideTranscript.TryGetValue(id, out var cached))
                return cached.Transcript;

            var path = ResolveMemoryDbPath(id);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var opened = new SideTranscript(path);
            _sideTranscript[id] = opened;
            return opened.Transcript;
        }
    }

    public ICharter GetCharter(string personaId) => RequireBundle(personaId).Charter;

    public CharterService GetCharterService(string personaId) => RequireBundle(personaId).Charter;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;
            _disposed = true;
            CloseAllSideMemory();
            CloseAllSideTranscript();
            if (_active is not null)
            {
                await _active.DisposeAsync().ConfigureAwait(false);
                _active = null;
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private void CloseSideMemory(string personaId)
    {
        lock (_sideGate)
        {
            if (_sideMemory.Remove(personaId, out var side))
                side.Dispose();
        }
    }

    private void CloseAllSideMemory()
    {
        lock (_sideGate)
        {
            foreach (var side in _sideMemory.Values)
                side.Dispose();
            _sideMemory.Clear();
        }
    }

    private void CloseSideTranscript(string personaId)
    {
        lock (_sideGate)
        {
            if (_sideTranscript.Remove(personaId, out var side))
                side.Dispose();
        }
    }

    private void CloseAllSideTranscript()
    {
        lock (_sideGate)
        {
            foreach (var side in _sideTranscript.Values)
                side.Dispose();
            _sideTranscript.Clear();
        }
    }

    private sealed class SideTranscript : IDisposable
    {
        private readonly SqliteMemorySession _session;

        public SideTranscript(string databasePath)
        {
            _session = new SqliteMemorySession(databasePath);
            Transcript = new SqliteChatTranscriptRepository(_session);
        }

        public SqliteChatTranscriptRepository Transcript { get; }

        public void Dispose() => _session.Dispose();
    }

    private PersonaStoreBundle RequireBundle(string personaId)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        var id = PersonaMemoryPaths.NormalizePersonaId(personaId);
        var bundle = _active
            ?? throw new InvalidOperationException("PersonaStoreHub has no active persona yet. Call SwitchActiveAsync first.");

        if (!string.Equals(bundle.PersonaId, id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Persona '{id}' is not the active store bundle (active='{bundle.PersonaId}'). " +
                "v1 is single-active; call SwitchActiveAsync before accessing another persona's stores.");
        }

        return bundle;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PersonaStoreHub));
    }
}
