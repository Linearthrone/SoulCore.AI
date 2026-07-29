using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Abstractions;

namespace SoulCore.Memory;

/// <summary>
/// SQLite-backed memory + emotion singleton. Applies DBD Schema/001 + Migrations/001 on first open.
/// </summary>
public sealed class SqliteMemoryStore : IMemoryStore, IEmotionState, IMemoryStats, IAsyncDisposable, IDisposable
{
    /// <summary>Max embedding rows scanned for in-process cosine recall.</summary>
    public const int SimilarRecallScanCap = 500;

    private static readonly HashSet<string> AllowedSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "self", "chat", "imported", "observation", "correction", "system", "model"
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
            var migration003 = ReadEmbedded("SoulCore.Memory.Migrations.003_episodic_source_model.sql");
            ExecuteScript(migration003);
            _logger.LogInformation("Applied Memory migration 003_episodic_source_model to {DbPath}", DatabasePath);
        }
        else
        {
            _logger.LogDebug("Memory migration 003 already applied at {DbPath}", DatabasePath);
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
