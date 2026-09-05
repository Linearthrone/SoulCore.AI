using System.Globalization;
using Microsoft.Data.Sqlite;
using SoulCore.Core.Abstractions;

namespace SoulCore.Memory.Repositories;

public sealed class SqliteEpisodicMemoryRepository : IMemoryStore, IMemoryStats
{
    public const int SimilarRecallScanCap = 500;
    private readonly SqliteMemorySession _session;
    public SqliteEpisodicMemoryRepository(SqliteMemorySession session) => _session = session ?? throw new ArgumentNullException(nameof(session));
    public bool IsDatabaseOpen => _session.IsDatabaseOpen;
    public string DatabasePath => _session.DatabasePath;
    bool IMemoryStats.IsOpen => _session.IsDatabaseOpen;

public async Task<long> CountEpisodicAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _session.RunDbAsync(async ct =>
            {
                await using var cmd = _session.Connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM episodic_memories WHERE is_quarantined = 0;";
                var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                return result is null || result is DBNull ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return 0;
        }
    }


    public async Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Episodic content must be non-empty.", nameof(text));

        var source = MemorySourceNormalizer.Normalize(sourceLabel);
        var occurredAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        return await _session.RunDbAsync(async ct =>
        {
        await using var cmd = _session.Connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO episodic_memories (content, occurred_at, source, is_quarantined)
            VALUES ($content, $occurred_at, $source, $quarantined);
            """;
        cmd.Parameters.AddWithValue("$content", text.Trim());
        cmd.Parameters.AddWithValue("$occurred_at", occurredAt);
        cmd.Parameters.AddWithValue("$source", source);
        cmd.Parameters.AddWithValue("$quarantined", string.Equals(source, "imported", StringComparison.Ordinal) ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await using var idCmd = _session.Connection.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid();";
        var result = await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (result is null || result is DBNull)
            throw new InvalidOperationException("Failed to obtain episodic_memories row id after insert.");
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task StoreEmbeddingAsync(
        long episodicId,
        float[] vector,
        string model,
        CancellationToken cancellationToken = default)
    {

        ArgumentNullException.ThrowIfNull(vector);
        if (episodicId <= 0)
            throw new ArgumentOutOfRangeException(nameof(episodicId));
        if (vector.Length == 0)
            throw new ArgumentException("Embedding vector must be non-empty.", nameof(vector));

        var modelName = string.IsNullOrWhiteSpace(model) ? "nomic-embed-text" : model.Trim();
        var blob = VectorSimilarity.ToLittleEndianBlob(vector);
        var createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        await _session.RunDbAsync(async ct =>
        {
        await using var cmd = _session.Connection.CreateCommand();
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
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {

        if (limit <= 0)
            return Array.Empty<(long, string)>();
        return await _session.RunDbAsync(async ct =>
        {
        await using var cmd = _session.Connection.CreateCommand();
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
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            list.Add((reader.GetInt64(0), reader.GetString(1)));

        return list;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> RecallSimilarAsync(
        float[] queryVector,
        int limit,
        CancellationToken cancellationToken = default)
    {

        ArgumentNullException.ThrowIfNull(queryVector);
        if (limit <= 0 || queryVector.Length == 0)
            return Array.Empty<string>();
        return await _session.RunDbAsync(async ct =>
        {
        await using var cmd = _session.Connection.CreateCommand();
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
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var content = reader.GetString(0);
            var blob = (byte[])reader.GetValue(1);
            var vector = VectorSimilarity.FromLittleEndianBlob(blob);
            if (vector.Length == queryVector.Length)
                candidates.Add((content, vector));
        }

        return VectorSimilarity.RankByCosineTopK(queryVector, candidates, limit);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default)
    {

        if (limit <= 0)
            return Array.Empty<string>();
        return await _session.RunDbAsync(async ct =>
        {
        await using var cmd = _session.Connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT content FROM episodic_memories
            WHERE is_quarantined = 0
            ORDER BY occurred_at DESC, id DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            list.Add(reader.GetString(0));

        return list;
        }, cancellationToken).ConfigureAwait(false);
    }

    
}
