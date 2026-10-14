using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Abstractions;
using SoulCore.Memory.Repositories;

namespace SoulCore.Memory;

public sealed class SqliteMemoryStore :
    IMemoryStore,
    IEmotionState,
    IMemoryStats,
    IVictoriaTaskStore,
    IVictoriaWorkflowStore,
    IVictoriaJournalStore,
    IVictoriaSopStore,
    IAsyncDisposable,
    IDisposable
{
    public static readonly HashSet<string> AllowedJournalBookIds = SqliteVictoriaJournalRepository.AllowedJournalBookIds;
    public const int SimilarRecallScanCap = SqliteEpisodicMemoryRepository.SimilarRecallScanCap;
    public static readonly HashSet<string> AllowedTaskStatuses = SqliteVictoriaTaskRepository.AllowedTaskStatuses;
    public const string DefaultTaskPriority = SqliteVictoriaTaskRepository.DefaultTaskPriority;
    public const string DefaultTaskStatus = SqliteVictoriaTaskRepository.DefaultTaskStatus;

    private readonly SqliteMemorySession _session;
    private readonly SqliteEpisodicMemoryRepository _episodic;
    private readonly SqliteEmotionRepository _emotion;
    private readonly SqliteVictoriaTaskRepository _tasks;
    private readonly SqliteVictoriaWorkflowRepository _workflows;
    private readonly SqliteVictoriaJournalRepository _journals;
    private readonly SqliteVictoriaSopRepository _sops;

    public SqliteMemoryStore(IOptions<MemoryOptions> options, ILogger<SqliteMemoryStore> logger)
        : this(new SqliteMemorySession(options, Microsoft.Extensions.Logging.Abstractions.NullLogger<SqliteMemorySession>.Instance))
    {
        _ = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SqliteMemoryStore(string dbPath, ILogger<SqliteMemoryStore>? logger = null)
        : this(new SqliteMemorySession(dbPath, Microsoft.Extensions.Logging.Abstractions.NullLogger<SqliteMemorySession>.Instance))
    {
        _ = logger;
    }

    public SqliteMemoryStore(SqliteMemorySession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _episodic = new(_session);
        _emotion = new(_session);
        _tasks = new(_session);
        _workflows = new(_session);
        _journals = new(_session);
        _sops = new(_session);
    }

    public bool IsDatabaseOpen => _episodic.IsDatabaseOpen;
    public string DatabasePath => _episodic.DatabasePath;
    bool IMemoryStats.IsOpen => ((IMemoryStats)_episodic).IsOpen;

    public Task<long> CountEpisodicAsync(CancellationToken ct = default) => _episodic.CountEpisodicAsync(ct);
    public Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken ct = default) =>
        _episodic.WriteEpisodicAsync(text, sourceLabel, ct);
    public Task StoreEmbeddingAsync(long id, float[] v, string m, CancellationToken ct = default) =>
        _episodic.StoreEmbeddingAsync(id, v, m, ct);
    public Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(int limit, CancellationToken ct = default) =>
        _episodic.ListEpisodicsMissingEmbeddingsAsync(limit, ct);
    public Task<IReadOnlyList<string>> RecallSimilarAsync(float[] q, int limit, CancellationToken ct = default) =>
        _episodic.RecallSimilarAsync(q, limit, ct);
    public Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken ct = default) =>
        _episodic.RecallRecentAsync(limit, ct);
    public Task<IReadOnlyDictionary<string, double>> GetAsync(CancellationToken ct = default) => _emotion.GetAsync(ct);
    public Task<long> GetRevisionAsync(CancellationToken ct = default) => _emotion.GetRevisionAsync(ct);
    public Task SetAsync(IReadOnlyDictionary<string, double> c, CancellationToken ct = default) => _emotion.SetAsync(c, ct);

    public Task<long> CreateAsync(string title, string? d, string? p, CancellationToken ct = default) =>
        _tasks.CreateAsync(title, d, p, ct);
    public Task<long> CreateAsync(
        string title, string? d, string? p, string? surface, bool ready, string? status, string? notes,
        CancellationToken ct = default) =>
        _tasks.CreateAsync(title, d, p, surface, ready, status, notes, ct);
    public Task<VictoriaTask?> GetAsync(long id, CancellationToken ct = default) => _tasks.GetAsync(id, ct);
    public Task<bool> UpdateStatusAsync(long id, string s, CancellationToken ct = default) =>
        _tasks.UpdateStatusAsync(id, s, ct);
    public Task<bool> ParkAsync(long id, string reason, DateTimeOffset parkUntil, CancellationToken ct = default) =>
        _tasks.ParkAsync(id, reason, parkUntil, ct);
    public Task<bool> ReleaseAsync(long id, CancellationToken ct = default) => _tasks.ReleaseAsync(id, ct);
    public Task<bool> SuspendAsync(long id, string? notes = null, CancellationToken ct = default) =>
        _tasks.SuspendAsync(id, notes, ct);
    public Task<int> RecordIdenticalFailureAsync(long id, string failureKey, CancellationToken ct = default) =>
        _tasks.RecordIdenticalFailureAsync(id, failureKey, ct);
    public Task ClearFailureCounterAsync(long id, CancellationToken ct = default) =>
        _tasks.ClearFailureCounterAsync(id, ct);
    public Task<bool> UpdateDetailsAsync(long id, string? surface = null, string? notes = null, CancellationToken ct = default) =>
        _tasks.UpdateDetailsAsync(id, surface, notes, ct);
    public Task<IReadOnlyList<VictoriaTask>> ListAsync(string? s = null, CancellationToken ct = default) =>
        _tasks.ListAsync(s, ct);
    public Task<bool> DeleteAsync(long id, CancellationToken ct = default) => _tasks.DeleteAsync(id, ct);

    public Task<long> CreateAsync(string name, IReadOnlyList<WorkflowStep> steps, CancellationToken ct = default) =>
        _workflows.CreateAsync(name, steps, ct);
    Task<VictoriaWorkflow?> IVictoriaWorkflowStore.GetAsync(long id, CancellationToken ct) =>
        _workflows.GetAsync(id, ct);
    public Task<bool> SetCurrentStepAsync(long id, int step, CancellationToken ct = default) =>
        _workflows.SetCurrentStepAsync(id, step, ct);

    public Task<IReadOnlyList<VictoriaJournalBook>> ListBooksAsync(CancellationToken ct = default) =>
        _journals.ListBooksAsync(ct);
    public Task<VictoriaJournalBook?> GetBookAsync(string bookId, CancellationToken ct = default) =>
        _journals.GetBookAsync(bookId, ct);
    public Task<long> WriteEntryAsync(
        string bookId, string body, string? moodJson = null, string? tagsJson = null,
        string? source = null, string? occurredAt = null, CancellationToken ct = default) =>
        _journals.WriteEntryAsync(bookId, body, moodJson, tagsJson, source, occurredAt, ct);
    public Task<IReadOnlyList<VictoriaJournalEntry>> ListEntriesAsync(
        string? bookId = null, int limit = 20, CancellationToken ct = default) =>
        _journals.ListEntriesAsync(bookId, limit, ct);

    public Task<long> CreateAsync(
        string title, string whenText, string steps, string donts, CancellationToken ct = default) =>
        _sops.CreateAsync(title, whenText, steps, donts, ct);
    Task<VictoriaSop?> IVictoriaSopStore.GetAsync(long id, CancellationToken ct) => _sops.GetAsync(id, ct);
    public Task<bool> UpdateAsync(
        long id, string? title = null, string? whenText = null, string? steps = null, string? donts = null,
        CancellationToken ct = default) =>
        _sops.UpdateAsync(id, title, whenText, steps, donts, ct);
    public Task<IReadOnlyList<VictoriaSop>> ListSopsAsync(CancellationToken ct = default) => _sops.ListAsync(ct);
    Task<IReadOnlyList<VictoriaSop>> IVictoriaSopStore.ListAsync(CancellationToken ct) => _sops.ListAsync(ct);

    internal static string SerializeWorkflowSteps(IReadOnlyList<WorkflowStep> s) =>
        SqliteVictoriaWorkflowRepository.SerializeWorkflowSteps(s);
    internal static IReadOnlyList<WorkflowStep> DeserializeWorkflowSteps(string j) =>
        SqliteVictoriaWorkflowRepository.DeserializeWorkflowSteps(j);

    public void Dispose() => _session.Dispose();
    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
