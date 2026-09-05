using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SoulCore.Core.Abstractions;

namespace SoulCore.Memory.Repositories;

public sealed class SqliteEmotionRepository : IEmotionState
{
    private static readonly JsonSerializerOptions JsonOptions = new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=false};
    private readonly SqliteMemorySession _session;
    public SqliteEmotionRepository(SqliteMemorySession session) => _session = session ?? throw new ArgumentNullException(nameof(session));

public async Task<IReadOnlyDictionary<string, double>> GetAsync(CancellationToken cancellationToken = default)
    {

        return await _session.RunDbAsync(async ct =>
        {
        await using var cmd = _session.Connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT valence, arousal, dominance, components_json
            FROM emotion_state WHERE id = 1;
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
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
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads <c>emotion_state.revision</c> for the singleton row (id=1).</summary>
    public async Task<long> GetRevisionAsync(CancellationToken cancellationToken = default)
    {

        return await _session.RunDbAsync(async ct =>
        {
        await using var cmd = _session.Connection.CreateCommand();
        cmd.CommandText = "SELECT revision FROM emotion_state WHERE id = 1;";
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (result is null || result is DBNull)
            throw new InvalidOperationException("emotion_state singleton row missing (id=1).");
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAsync(IReadOnlyDictionary<string, double> components, CancellationToken cancellationToken = default)
    {

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
        await _session.RunDbAsync(async ct =>
        {
        await using var tx = await _session.Connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await using (var update = _session.Connection.CreateCommand())
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
                var rows = await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                if (rows != 1)
                    throw new InvalidOperationException("Failed to update emotion_state singleton.");
            }

            await using (var hist = _session.Connection.CreateCommand())
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
                await hist.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await tx.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
        }, cancellationToken).ConfigureAwait(false);
    }

    
    private static double GetOrDefault(IReadOnlyDictionary<string, double> map, string key, double fallback)
    {
        foreach (var (k, v) in map) if (k.Equals(key, StringComparison.OrdinalIgnoreCase)) return v;
        return fallback;
    }
    private static void ClampEmotion(ref double valence, ref double arousal, ref double dominance)
    {
        valence = Math.Clamp(valence, -1.0, 1.0); arousal = Math.Clamp(arousal, 0.0, 1.0); dominance = Math.Clamp(dominance, 0.0, 1.0);
    }
    private static void MergeComponentsJson(string json, Dictionary<string, double> map)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return;
        try { using var doc = JsonDocument.Parse(json); foreach (var prop in doc.RootElement.EnumerateObject()) if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var d) && !map.ContainsKey(prop.Name)) map[prop.Name] = d; }
        catch (JsonException) { }
    }
