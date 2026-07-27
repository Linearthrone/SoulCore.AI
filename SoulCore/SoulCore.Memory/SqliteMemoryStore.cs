using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Abstractions;

namespace SoulCore.Memory;

/// <summary>
/// SQLite-backed memory + emotion singleton. Applies DBD Schema/001 + Migrations/001 on first open.
/// Also implements <see cref="IVictoriaTaskStore"/> (BED-140) against the
/// <c>victoria_tasks</c> table and <see cref="IVictoriaWorkflowStore"/> (BED-141)
/// against <c>victoria_workflows</c> in the same DB — Victoria's own work items /
/// multi-step plans, separate from PM tickets under <c>docs/agents/tasks/</c>.
/// </summary>
public sealed class SqliteMemoryStore : IMemoryStore, IEmotionState, IMemoryStats, IVictoriaTaskStore, IVictoriaWorkflowStore, IAsyncDisposable, IDisposable
{
    /// <summary>Max embedding rows scanned for in-process cosine recall.</summary>
    public const int SimilarRecallScanCap = 500;

    /// <summary>Allowed <c>victoria_tasks.status</c> values (BED-140).</summary>
    public static readonly HashSet<string> AllowedTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "todo", "in_progress", "done", "blocked"
    };

    /// <summary>Default priority for a newly created Victoria task.</summary>
    public const string DefaultTaskPriority = "medium";

    /// <summary>Default status for a newly created Victoria task.</summary>
    public const string DefaultTaskStatus = "todo";

    private static readonly HashSet<string> AllowedSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "self", "chat", "imported", "observation", "correction", "system"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ILogger<SqliteMemoryStore> _logger;
    private readonly SqliteConnection _connection;
    private readonly object _gate = new();
    private bool _disposed;

    public SqliteMemoryStore(IOptions<MemoryOptions> options, ILogger<SqliteMemoryStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        DatabasePath = options.Value.ResolveDbPath();
        var dir = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString());

        _connection.Open();
        ApplyMigrations();
        IsDatabaseOpen = true;
        _logger.LogInformation("SqliteMemoryStore ready at {DbPath}", DatabasePath);
    }

    /// <summary>Test / round-trip helper: open an explicit path without IOptions.</summary>
    public SqliteMemoryStore(string dbPath, ILogger<SqliteMemoryStore>? logger = null)
        : this(Microsoft.Extensions.Options.Options.Create(new MemoryOptions { DbPath = dbPath }),
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SqliteMemoryStore>.Instance)
    {
    }

    public bool IsDatabaseOpen { get; private set; }

    public string DatabasePath { get; }

    /// <summary>IMemoryStats — re-exposes <see cref="IsDatabaseOpen"/> for the system_info tool.</summary>
    bool IMemoryStats.IsOpen => IsDatabaseOpen;

    /// <summary>
    /// IMemoryStats — count of non-quarantined episodic memories. Returns 0 on
    /// any error (best-effort, used only by <c>system_info</c> for a status line).
    /// </summary>
    public async Task<long> CountEpisodicAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return 0;
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM episodic_memories WHERE is_quarantined = 0;";
            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is null || result is DBNull ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public async Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Episodic content must be non-empty.", nameof(text));

        var source = NormalizeSource(sourceLabel);
        var occurredAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO episodic_memories (content, occurred_at, source, is_quarantined)
            VALUES ($content, $occurred_at, $source, $quarantined);
            """;
        cmd.Parameters.AddWithValue("$content", text.Trim());
        cmd.Parameters.AddWithValue("$occurred_at", occurredAt);
        cmd.Parameters.AddWithValue("$source", source);
        cmd.Parameters.AddWithValue("$quarantined", string.Equals(source, "imported", StringComparison.Ordinal) ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var idCmd = _connection.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid();";
        var result = await idCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null || result is DBNull)
            throw new InvalidOperationException("Failed to obtain episodic_memories row id after insert.");
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public async Task StoreEmbeddingAsync(
        long episodicId,
        float[] vector,
        string model,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(vector);
        if (episodicId <= 0)
            throw new ArgumentOutOfRangeException(nameof(episodicId));
        if (vector.Length == 0)
            throw new ArgumentException("Embedding vector must be non-empty.", nameof(vector));

        var modelName = string.IsNullOrWhiteSpace(model) ? "nomic-embed-text" : model.Trim();
        var blob = VectorSimilarity.ToLittleEndianBlob(vector);
        var createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO episodic_embedding_vectors (episodic_id, model, dims, vector, created_at)
            VALUES ($episodic_id, $model, $dims, $vector, $created_at)
            ON CONFLICT(episodic_id) DO UPDATE SET
                model = excluded.model,
                dims = excluded.dims,
                vector = excluded.vector,
                created_at = excluded.created_at;
            """;
        cmd.Parameters.AddWithValue("$episodic_id", episodicId);
        cmd.Parameters.AddWithValue("$model", modelName);
        cmd.Parameters.AddWithValue("$dims", vector.Length);
        cmd.Parameters.AddWithValue("$vector", blob);
        cmd.Parameters.AddWithValue("$created_at", createdAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (limit <= 0)
            return Array.Empty<(long, string)>();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT e.id, e.content
            FROM episodic_memories e
            LEFT JOIN episodic_embedding_vectors v ON v.episodic_id = e.id
            WHERE e.is_quarantined = 0
              AND v.episodic_id IS NULL
            ORDER BY e.id ASC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        var list = new List<(long Id, string Content)>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add((reader.GetInt64(0), reader.GetString(1)));

        return list;
    }

    public async Task<IReadOnlyList<string>> RecallSimilarAsync(
        float[] queryVector,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(queryVector);
        if (limit <= 0 || queryVector.Length == 0)
            return Array.Empty<string>();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT e.content, v.vector
            FROM episodic_embedding_vectors v
            INNER JOIN episodic_memories e ON e.id = v.episodic_id
            WHERE e.is_quarantined = 0
              AND v.dims = $dims
            ORDER BY e.occurred_at DESC, e.id DESC
            LIMIT $scan_cap;
            """;
        cmd.Parameters.AddWithValue("$dims", queryVector.Length);
        cmd.Parameters.AddWithValue("$scan_cap", SimilarRecallScanCap);

        var candidates = new List<(string Item, float[] Vector)>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var content = reader.GetString(0);
            var blob = (byte[])reader.GetValue(1);
            var vector = VectorSimilarity.FromLittleEndianBlob(blob);
            if (vector.Length == queryVector.Length)
                candidates.Add((content, vector));
        }

        return VectorSimilarity.RankByCosineTopK(queryVector, candidates, limit);
    }

    public async Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (limit <= 0)
            return Array.Empty<string>();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT content FROM episodic_memories
            WHERE is_quarantined = 0
            ORDER BY occurred_at DESC, id DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(reader.GetString(0));

        return list;
    }

    public async Task<IReadOnlyDictionary<string, double>> GetAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT valence, arousal, dominance, components_json
            FROM emotion_state WHERE id = 1;
            """;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("emotion_state singleton row missing (id=1).");

        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["valence"] = reader.GetDouble(0),
            ["arousal"] = reader.GetDouble(1),
            ["dominance"] = reader.GetDouble(2)
        };

        var componentsJson = reader.IsDBNull(3) ? "{}" : reader.GetString(3);
        MergeComponentsJson(componentsJson, map);
        return map;
    }

    /// <summary>Reads <c>emotion_state.revision</c> for the singleton row (id=1).</summary>
    public async Task<long> GetRevisionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT revision FROM emotion_state WHERE id = 1;";
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null || result is DBNull)
            throw new InvalidOperationException("emotion_state singleton row missing (id=1).");
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public async Task SetAsync(IReadOnlyDictionary<string, double> components, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(components);

        var valence = GetOrDefault(components, "valence", 0.0);
        var arousal = GetOrDefault(components, "arousal", 0.0);
        var dominance = GetOrDefault(components, "dominance", 0.5);
        ClampEmotion(ref valence, ref arousal, ref dominance);

        var extras = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in components)
        {
            if (key.Equals("valence", StringComparison.OrdinalIgnoreCase)
                || key.Equals("arousal", StringComparison.OrdinalIgnoreCase)
                || key.Equals("dominance", StringComparison.OrdinalIgnoreCase))
                continue;
            extras[key] = value;
        }

        var componentsJson = JsonSerializer.Serialize(extras, JsonOptions);
        var updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        await using var tx = await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (var update = _connection.CreateCommand())
            {
                update.Transaction = (SqliteTransaction)tx;
                update.CommandText =
                    """
                    UPDATE emotion_state
                    SET valence = $valence,
                        arousal = $arousal,
                        dominance = $dominance,
                        components_json = $components_json,
                        updated_at = $updated_at,
                        revision = revision + 1
                    WHERE id = 1;
                    """;
                update.Parameters.AddWithValue("$valence", valence);
                update.Parameters.AddWithValue("$arousal", arousal);
                update.Parameters.AddWithValue("$dominance", dominance);
                update.Parameters.AddWithValue("$components_json", componentsJson);
                update.Parameters.AddWithValue("$updated_at", updatedAt);
                var rows = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                if (rows != 1)
                    throw new InvalidOperationException("Failed to update emotion_state singleton.");
            }

            await using (var hist = _connection.CreateCommand())
            {
                hist.Transaction = (SqliteTransaction)tx;
                hist.CommandText =
                    """
                    INSERT INTO emotion_state_history
                        (valence, arousal, dominance, components_json, reason)
                    VALUES ($valence, $arousal, $dominance, $components_json, 'update');
                    """;
                hist.Parameters.AddWithValue("$valence", valence);
                hist.Parameters.AddWithValue("$arousal", arousal);
                hist.Parameters.AddWithValue("$dominance", dominance);
                hist.Parameters.AddWithValue("$components_json", componentsJson);
                await hist.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    // ─── IVictoriaTaskStore (BED-140) ─────────────────────────────────────

    /// <inheritdoc />
    public async Task<long> CreateAsync(
        string title,
        string? description,
        string? priority,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task title must be non-empty.", nameof(title));

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var resolvedPriority = string.IsNullOrWhiteSpace(priority)
            ? DefaultTaskPriority
            : priority.Trim().ToLowerInvariant();
        var resolvedDescription = description?.Trim() ?? string.Empty;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO victoria_tasks (title, description, status, priority, created_at, updated_at)
            VALUES ($title, $description, $status, $priority, $created_at, $updated_at);
            """;
        cmd.Parameters.AddWithValue("$title", title.Trim());
        cmd.Parameters.AddWithValue("$description", resolvedDescription);
        cmd.Parameters.AddWithValue("$status", DefaultTaskStatus);
        cmd.Parameters.AddWithValue("$priority", resolvedPriority);
        cmd.Parameters.AddWithValue("$created_at", now);
        cmd.Parameters.AddWithValue("$updated_at", now);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var idCmd = _connection.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid();";
        var result = await idCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null || result is DBNull)
            throw new InvalidOperationException("Failed to obtain victoria_tasks row id after insert.");
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public async Task<VictoriaTask?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (id <= 0)
            return null;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, title, description, status, priority, created_at, updated_at
            FROM victoria_tasks
            WHERE id = $id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return ReadVictoriaTask(reader);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateStatusAsync(long id, string status, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (id <= 0)
            return false;
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Task status must be non-empty.", nameof(status));

        var normalized = status.Trim().ToLowerInvariant();
        if (!AllowedTaskStatuses.Contains(normalized))
        {
            throw new ArgumentException(
                $"Invalid task status '{status}'. Allowed: todo, in_progress, done, blocked.",
                nameof(status));
        }

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE victoria_tasks
            SET status = $status, updated_at = $updated_at
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$status", normalized);
        cmd.Parameters.AddWithValue("$updated_at", now);
        cmd.Parameters.AddWithValue("$id", id);
        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VictoriaTask>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await using var cmd = _connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(status))
        {
            cmd.CommandText =
                """
                SELECT id, title, description, status, priority, created_at, updated_at
                FROM victoria_tasks
                ORDER BY updated_at DESC, id DESC;
                """;
        }
        else
        {
            var normalized = status.Trim().ToLowerInvariant();
            cmd.CommandText =
                """
                SELECT id, title, description, status, priority, created_at, updated_at
                FROM victoria_tasks
                WHERE status = $status
                ORDER BY updated_at DESC, id DESC;
                """;
            cmd.Parameters.AddWithValue("$status", normalized);
        }

        var list = new List<VictoriaTask>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadVictoriaTask(reader));
        }

        return list;
    }

    private static VictoriaTask ReadVictoriaTask(SqliteDataReader reader)
    {
        return new VictoriaTask(
            Id: reader.GetInt64(0),
            Title: reader.GetString(1),
            Description: reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Status: reader.GetString(3),
            Priority: reader.GetString(4),
            CreatedAt: reader.GetString(5),
            UpdatedAt: reader.GetString(6));
    }

    // ─── IVictoriaWorkflowStore (BED-141) ─────────────────────────────────

    /// <inheritdoc />
    public async Task<long> CreateAsync(
        string name,
        IReadOnlyList<WorkflowStep> steps,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workflow name must be non-empty.", nameof(name));
        if (steps is null)
            throw new ArgumentNullException(nameof(steps));
        if (steps.Count == 0)
            throw new ArgumentException("Workflow must have at least one step.", nameof(steps));

        foreach (var step in steps)
        {
            if (step is null || string.IsNullOrWhiteSpace(step.Description))
                throw new ArgumentException("Each workflow step requires a non-empty description.", nameof(steps));
        }

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var stepsJson = SerializeWorkflowSteps(steps);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO victoria_workflows (name, steps_json, current_step, created_at, updated_at)
            VALUES ($name, $steps_json, 0, $created_at, $updated_at);
            """;
        cmd.Parameters.AddWithValue("$name", name.Trim());
        cmd.Parameters.AddWithValue("$steps_json", stepsJson);
        cmd.Parameters.AddWithValue("$created_at", now);
        cmd.Parameters.AddWithValue("$updated_at", now);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var idCmd = _connection.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid();";
        var result = await idCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null || result is DBNull)
            throw new InvalidOperationException("Failed to obtain victoria_workflows row id after insert.");
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    async Task<VictoriaWorkflow?> IVictoriaWorkflowStore.GetAsync(long id, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (id <= 0)
            return null;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, name, steps_json, current_step, created_at, updated_at
            FROM victoria_workflows
            WHERE id = $id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return ReadVictoriaWorkflow(reader);
    }

    /// <inheritdoc />
    public async Task<bool> SetCurrentStepAsync(
        long id,
        int currentStep,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (id <= 0)
            return false;
        if (currentStep < 0)
            throw new ArgumentOutOfRangeException(nameof(currentStep), "current_step must be >= 0.");

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE victoria_workflows
            SET current_step = $current_step, updated_at = $updated_at
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$current_step", currentStep);
        cmd.Parameters.AddWithValue("$updated_at", now);
        cmd.Parameters.AddWithValue("$id", id);
        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }

    private static VictoriaWorkflow ReadVictoriaWorkflow(SqliteDataReader reader)
    {
        var stepsJson = reader.IsDBNull(2) ? "[]" : reader.GetString(2);
        return new VictoriaWorkflow(
            Id: reader.GetInt64(0),
            Name: reader.GetString(1),
            Steps: DeserializeWorkflowSteps(stepsJson),
            CurrentStep: reader.GetInt32(3),
            CreatedAt: reader.GetString(4),
            UpdatedAt: reader.GetString(5));
    }

    internal static string SerializeWorkflowSteps(IReadOnlyList<WorkflowStep> steps)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var s in steps)
            {
                writer.WriteStartObject();
                writer.WriteString("description", s.Description);
                if (!string.IsNullOrWhiteSpace(s.Tool))
                    writer.WriteString("tool", s.Tool!.Trim());
                if (s.Args.ValueKind == JsonValueKind.Object)
                {
                    writer.WritePropertyName("args");
                    s.Args.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static IReadOnlyList<WorkflowStep> DeserializeWorkflowSteps(string stepsJson)
    {
        if (string.IsNullOrWhiteSpace(stepsJson))
            return Array.Empty<WorkflowStep>();

        using var doc = JsonDocument.Parse(stepsJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("victoria_workflows.steps_json must be a JSON array.");

        var list = new List<WorkflowStep>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Each workflow step must be a JSON object.");

            if (!el.TryGetProperty("description", out var descProp) || descProp.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("Each workflow step requires a 'description' string.");

            var description = descProp.GetString() ?? string.Empty;
            string? tool = null;
            if (el.TryGetProperty("tool", out var toolProp) && toolProp.ValueKind == JsonValueKind.String)
            {
                var t = toolProp.GetString();
                if (!string.IsNullOrWhiteSpace(t))
                    tool = t.Trim();
            }

            var args = default(JsonElement);
            if (el.TryGetProperty("args", out var argsProp) && argsProp.ValueKind == JsonValueKind.Object)
                args = argsProp.Clone();

            list.Add(new WorkflowStep(description, tool, args));
        }

        return list;
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_gate)
        {
            if (_disposed) return;
            _connection.Dispose();
            IsDatabaseOpen = false;
            _disposed = true;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void ApplyMigrations()
    {
        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        if (!IsMigrationApplied("001"))
        {
            var schemaSql = ReadEmbedded("SoulCore.Memory.Schema.001_schema.sql");
            ExecuteScript(schemaSql);

            // Ledger row comes only from 001_initial.sql (no INSERT fallback)
            var migrationSql = ReadEmbedded("SoulCore.Memory.Migrations.001_initial.sql");
            ExecuteScript(migrationSql);

            _logger.LogInformation("Applied Memory migration 001_initial to {DbPath}", DatabasePath);
        }
        else
        {
            _logger.LogDebug("Memory migration 001 already applied at {DbPath}", DatabasePath);
        }

        if (!IsMigrationApplied("002"))
        {
            var migration002 = ReadEmbedded("SoulCore.Memory.Migrations.002_embedding_vectors.sql");
            ExecuteScript(migration002);
            _logger.LogInformation("Applied Memory migration 002_embedding_vectors to {DbPath}", DatabasePath);
        }
        else
        {
            _logger.LogDebug("Memory migration 002 already applied at {DbPath}", DatabasePath);
        }

        if (!IsMigrationApplied("003"))
        {
            var migration003 = ReadEmbedded("SoulCore.Memory.Migrations.003_victoria_tasks.sql");
            ExecuteScript(migration003);
            _logger.LogInformation("Applied Memory migration 003_victoria_tasks to {DbPath}", DatabasePath);
        }
        else
        {
            _logger.LogDebug("Memory migration 003 already applied at {DbPath}", DatabasePath);
        }

        if (!IsMigrationApplied("004"))
        {
            var migration004 = ReadEmbedded("SoulCore.Memory.Migrations.004_victoria_workflows.sql");
            ExecuteScript(migration004);
            _logger.LogInformation("Applied Memory migration 004_victoria_workflows to {DbPath}", DatabasePath);
        }
        else
        {
            _logger.LogDebug("Memory migration 004 already applied at {DbPath}", DatabasePath);
        }
    }

    private bool IsMigrationApplied(string version)
    {
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText =
                "SELECT 1 FROM schema_migrations WHERE version = $version LIMIT 1;";
            cmd.Parameters.AddWithValue("$version", version);
            var result = cmd.ExecuteScalar();
            return result is not null && result is not DBNull;
        }
        catch (SqliteException)
        {
            // schema_migrations missing → first run
            return false;
        }
    }

    private void ExecuteScript(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string ReadEmbedded(string logicalName)
    {
        var asm = typeof(SqliteMemoryStore).Assembly;
        using var stream = asm.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded SQL not found: {logicalName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string NormalizeSource(string? sourceLabel)
    {
        if (string.IsNullOrWhiteSpace(sourceLabel))
            return "system";

        var trimmed = sourceLabel.Trim().ToLowerInvariant();
        return AllowedSources.Contains(trimmed) ? trimmed : "system";
    }

    private static double GetOrDefault(IReadOnlyDictionary<string, double> map, string key, double fallback)
    {
        foreach (var (k, v) in map)
        {
            if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                return v;
        }

        return fallback;
    }

    private static void ClampEmotion(ref double valence, ref double arousal, ref double dominance)
    {
        valence = Math.Clamp(valence, -1.0, 1.0);
        arousal = Math.Clamp(arousal, 0.0, 1.0);
        dominance = Math.Clamp(dominance, 0.0, 1.0);
    }

    private static void MergeComponentsJson(string json, Dictionary<string, double> map)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number
                    && prop.Value.TryGetDouble(out var d)
                    && !map.ContainsKey(prop.Name))
                {
                    map[prop.Name] = d;
                }
            }
        }
        catch (JsonException)
        {
            // Keep core VAD columns if components_json is malformed.
        }
    }
}
