using System.Globalization;
using Microsoft.Data.Sqlite;
using SoulCore.Core.Growth;

namespace SoulCore.Memory.Repositories;

/// <summary>PROP-20.37: growth soak / weights / ledger / pending + bond counter queries.</summary>
public sealed class SqliteGrowthRepository : IGrowthStore
{
    /// <summary>Max gap (minutes) between chat turns that still counts as continuous contact.</summary>
    public const double ContactSessionGapMinutes = 20.0;

    private readonly SqliteMemorySession _session;

    public SqliteGrowthRepository(SqliteMemorySession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    public async Task<DateTimeOffset> EnsureSoakStartedAsync(
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        var now = (utcNow ?? DateTimeOffset.UtcNow)
            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        return await _session.RunDbAsync(async ct =>
        {
            await using (var existing = _session.Connection.CreateCommand())
            {
                existing.CommandText = "SELECT started_at FROM growth_soak_state WHERE id = 1;";
                var raw = await existing.ExecuteScalarAsync(ct).ConfigureAwait(false);
                if (raw is string s && TryParseUtc(s, out var started))
                    return started;
            }

            await using (var insert = _session.Connection.CreateCommand())
            {
                insert.CommandText =
                    """
                    INSERT INTO growth_soak_state (id, started_at, created_at, updated_at)
                    VALUES (1, $started_at, $started_at, $started_at);
                    """;
                insert.Parameters.AddWithValue("$started_at", now);
                try
                {
                    await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                catch (SqliteException)
                {
                    // Race: another caller inserted — fall through to read.
                }
            }

            await using var read = _session.Connection.CreateCommand();
            read.CommandText = "SELECT started_at FROM growth_soak_state WHERE id = 1;";
            var again = await read.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (again is string s2 && TryParseUtc(s2, out var started2))
                return started2;

            throw new InvalidOperationException("growth_soak_state row missing after ensure.");
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DateTimeOffset?> GetSoakStartedAtAsync(CancellationToken cancellationToken = default)
    {
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText = "SELECT started_at FROM growth_soak_state WHERE id = 1;";
            var raw = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (raw is string s && TryParseUtc(s, out var started))
                return (DateTimeOffset?)started;
            return null;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GrowthTraitLine>> ListTraitWeightsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT trait_id, weight, band, prompt_line, ledger_reason
                FROM growth_trait_weights
                ORDER BY CASE trait_id
                    WHEN 'responsiveness' THEN 1
                    WHEN 'initiative' THEN 2
                    WHEN 'openness' THEN 3
                    ELSE 9 END;
                """;
            var list = new List<GrowthTraitLine>(3);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var id = reader.GetString(0);
                var name = char.ToUpperInvariant(id[0]) + id[1..];
                list.Add(new GrowthTraitLine(
                    id,
                    name,
                    reader.GetDouble(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4)));
            }

            if (list.Count == 0)
                return GrowthTraits.DefaultSeedTraits;

            return list;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetTraitWeightAsync(
        string traitId,
        double weight,
        string band,
        string ledgerReason,
        CancellationToken cancellationToken = default)
    {
        var id = NormalizeTraitId(traitId);
        var clamped = GrowthBands.ClampWeight(weight);
        var updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE growth_trait_weights
                SET weight = $weight,
                    band = $band,
                    ledger_reason = $ledger_reason,
                    updated_at = $updated_at
                WHERE trait_id = $trait_id;
                """;
            cmd.Parameters.AddWithValue("$weight", clamped);
            cmd.Parameters.AddWithValue("$band", band);
            cmd.Parameters.AddWithValue("$ledger_reason", ledgerReason);
            cmd.Parameters.AddWithValue("$updated_at", updatedAt);
            cmd.Parameters.AddWithValue("$trait_id", id);
            var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (rows != 1)
                throw new InvalidOperationException($"Unknown growth trait_id '{id}'.");
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> AppendLedgerAsync(
        string traitId,
        double fromWeight,
        double toWeight,
        string fromBand,
        string toBand,
        string reason,
        string source,
        CancellationToken cancellationToken = default)
    {
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO growth_delta_ledger
                    (trait_id, from_weight, to_weight, from_band, to_band, reason, source)
                VALUES
                    ($trait_id, $from_weight, $to_weight, $from_band, $to_band, $reason, $source);
                """;
            cmd.Parameters.AddWithValue("$trait_id", NormalizeTraitId(traitId));
            cmd.Parameters.AddWithValue("$from_weight", fromWeight);
            cmd.Parameters.AddWithValue("$to_weight", toWeight);
            cmd.Parameters.AddWithValue("$from_band", fromBand);
            cmd.Parameters.AddWithValue("$to_band", toBand);
            cmd.Parameters.AddWithValue("$reason", reason);
            cmd.Parameters.AddWithValue("$source", source);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            await using var idCmd = _session.Connection.CreateCommand();
            idCmd.CommandText = "SELECT last_insert_rowid();";
            var result = await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return result is null or DBNull
                ? 0
                : Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GrowthLedgerEntry>> ListLedgerAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 500);
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, trait_id, from_weight, to_weight, from_band, to_band, reason, source, recorded_at
                FROM growth_delta_ledger
                ORDER BY id DESC
                LIMIT $limit;
                """;
            cmd.Parameters.AddWithValue("$limit", take);
            var list = new List<GrowthLedgerEntry>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new GrowthLedgerEntry(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetDouble(2),
                    reader.GetDouble(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8)));
            }

            return list;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> CreatePendingAsync(
        string traitId,
        double fromWeight,
        double toWeight,
        string fromBand,
        string toBand,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return await _session.RunDbAsync(async ct =>
        {
            // Dedupe: one open pending per trait.
            await using (var existing = _session.Connection.CreateCommand())
            {
                existing.CommandText =
                    """
                    SELECT id FROM growth_pending_proposals
                    WHERE trait_id = $trait_id AND status = 'pending'
                    ORDER BY id DESC LIMIT 1;
                    """;
                existing.Parameters.AddWithValue("$trait_id", NormalizeTraitId(traitId));
                var found = await existing.ExecuteScalarAsync(ct).ConfigureAwait(false);
                if (found is not null and not DBNull)
                {
                    var id = Convert.ToInt64(found, CultureInfo.InvariantCulture);
                    await using var upd = _session.Connection.CreateCommand();
                    upd.CommandText =
                        """
                        UPDATE growth_pending_proposals
                        SET from_weight = $from_weight,
                            to_weight = $to_weight,
                            from_band = $from_band,
                            to_band = $to_band,
                            reason = $reason
                        WHERE id = $id;
                        """;
                    upd.Parameters.AddWithValue("$from_weight", fromWeight);
                    upd.Parameters.AddWithValue("$to_weight", toWeight);
                    upd.Parameters.AddWithValue("$from_band", fromBand);
                    upd.Parameters.AddWithValue("$to_band", toBand);
                    upd.Parameters.AddWithValue("$reason", reason);
                    upd.Parameters.AddWithValue("$id", id);
                    await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    return id;
                }
            }

            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO growth_pending_proposals
                    (trait_id, from_weight, to_weight, from_band, to_band, reason, status)
                VALUES
                    ($trait_id, $from_weight, $to_weight, $from_band, $to_band, $reason, 'pending');
                """;
            cmd.Parameters.AddWithValue("$trait_id", NormalizeTraitId(traitId));
            cmd.Parameters.AddWithValue("$from_weight", fromWeight);
            cmd.Parameters.AddWithValue("$to_weight", toWeight);
            cmd.Parameters.AddWithValue("$from_band", fromBand);
            cmd.Parameters.AddWithValue("$to_band", toBand);
            cmd.Parameters.AddWithValue("$reason", reason);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            await using var idCmd = _session.Connection.CreateCommand();
            idCmd.CommandText = "SELECT last_insert_rowid();";
            var result = await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return result is null or DBNull
                ? 0
                : Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GrowthPendingProposal>> ListPendingAsync(
        CancellationToken cancellationToken = default)
    {
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, trait_id, from_weight, to_weight, from_band, to_band, reason, status, created_at, resolved_at
                FROM growth_pending_proposals
                WHERE status = 'pending'
                ORDER BY id ASC;
                """;
            return await ReadPendingAsync(cmd, ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GrowthPendingProposal?> GetPendingAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _session.RunDbAsync<GrowthPendingProposal?>(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, trait_id, from_weight, to_weight, from_band, to_band, reason, status, created_at, resolved_at
                FROM growth_pending_proposals
                WHERE id = $id
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            var list = await ReadPendingAsync(cmd, ct).ConfigureAwait(false);
            return list.Count == 0 ? null : list[0];
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ResolvePendingAsync(
        long id,
        string status,
        CancellationToken cancellationToken = default)
    {
        if (status is not ("confirmed" or "dismissed"))
            throw new ArgumentException("status must be confirmed or dismissed.", nameof(status));

        var resolvedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        return await _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE growth_pending_proposals
                SET status = $status, resolved_at = $resolved_at
                WHERE id = $id AND status = 'pending';
                """;
            cmd.Parameters.AddWithValue("$status", status);
            cmd.Parameters.AddWithValue("$resolved_at", resolvedAt);
            cmd.Parameters.AddWithValue("$id", id);
            var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return rows == 1;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BondCounterSnapshot> ComputeBondCountersAsync(
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken = default)
    {
        var start = windowStartUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var end = windowEndUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        return await _session.RunDbAsync(async ct =>
        {
            var contactMinutes = await ComputeContactMinutesAsync(start, end, ct).ConfigureAwait(false);
            var daysWithContact = await CountDistinctContactDaysAsync(start, end, ct).ConfigureAwait(false);
            var entrusted = await CountScalarAsync(
                "SELECT COUNT(*) FROM victoria_tasks;", ct).ConfigureAwait(false);
            var corrections = await CountScalarAsync(
                """
                SELECT COUNT(*) FROM episodic_memories
                WHERE source = 'correction'
                  AND occurred_at >= $start AND occurred_at <= $end;
                """,
                ct, start, end).ConfigureAwait(false);
            var reaches = await CountHerInitiatedReachesAsync(start, end, ct).ConfigureAwait(false);
            var journals = await CountScalarAsync(
                """
                SELECT COUNT(*) FROM victoria_journal_entries
                WHERE occurred_at >= $start AND occurred_at <= $end;
                """,
                ct, start, end).ConfigureAwait(false);

            return new BondCounterSnapshot(
                contactMinutes,
                daysWithContact,
                (int)entrusted,
                (int)corrections,
                (int)reaches,
                (int)journals);
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<double> ComputeContactMinutesAsync(string start, string end, CancellationToken ct)
    {
        await using var cmd = _session.Connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT occurred_at FROM chat_messages
            WHERE occurred_at >= $start AND occurred_at <= $end
            ORDER BY occurred_at ASC, id ASC;
            """;
        cmd.Parameters.AddWithValue("$start", start);
        cmd.Parameters.AddWithValue("$end", end);

        var stamps = new List<DateTimeOffset>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (TryParseUtc(reader.GetString(0), out var ts))
                stamps.Add(ts);
        }

        if (stamps.Count == 0)
        {
            // Fallback: episodic chat rows when transcript is empty.
            await using var epi = _session.Connection.CreateCommand();
            epi.CommandText =
                """
                SELECT occurred_at FROM episodic_memories
                WHERE source IN ('chat', 'correction')
                  AND occurred_at >= $start AND occurred_at <= $end
                ORDER BY occurred_at ASC, id ASC;
                """;
            epi.Parameters.AddWithValue("$start", start);
            epi.Parameters.AddWithValue("$end", end);
            await using var er = await epi.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await er.ReadAsync(ct).ConfigureAwait(false))
            {
                if (TryParseUtc(er.GetString(0), out var ts))
                    stamps.Add(ts);
            }
        }

        double minutes = 0;
        for (var i = 1; i < stamps.Count; i++)
        {
            var gap = (stamps[i] - stamps[i - 1]).TotalMinutes;
            if (gap > 0 && gap <= ContactSessionGapMinutes)
                minutes += gap;
        }

        // Lonely single-message contact days still count as a brief presence.
        if (stamps.Count > 0 && minutes < 1.0)
            minutes = Math.Max(minutes, stamps.Count * 0.5);

        return Math.Round(minutes, 1);
    }

    private async Task<int> CountDistinctContactDaysAsync(string start, string end, CancellationToken ct)
    {
        // Prefer user chat turns; fall back to episodic chat/correction.
        await using var cmd = _session.Connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT COUNT(*) FROM (
                SELECT DISTINCT substr(occurred_at, 1, 10) AS d
                FROM chat_messages
                WHERE role = 'user'
                  AND occurred_at >= $start AND occurred_at <= $end
                UNION
                SELECT DISTINCT substr(occurred_at, 1, 10) AS d
                FROM episodic_memories
                WHERE source IN ('chat', 'correction')
                  AND occurred_at >= $start AND occurred_at <= $end
            );
            """;
        cmd.Parameters.AddWithValue("$start", start);
        cmd.Parameters.AddWithValue("$end", end);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is null or DBNull ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private async Task<long> CountHerInitiatedReachesAsync(string start, string end, CancellationToken ct)
    {
        var fromChat = await CountScalarAsync(
            """
            SELECT COUNT(*) FROM chat_messages
            WHERE role = 'assistant'
              AND channel = 'proactive'
              AND occurred_at >= $start AND occurred_at <= $end;
            """,
            ct, start, end).ConfigureAwait(false);

        var fromEpi = await CountScalarAsync(
            """
            SELECT COUNT(*) FROM episodic_memories
            WHERE content LIKE '[Proactive]%'
              AND occurred_at >= $start AND occurred_at <= $end;
            """,
            ct, start, end).ConfigureAwait(false);

        return fromChat + fromEpi;
    }

    private async Task<long> CountScalarAsync(
        string sql,
        CancellationToken ct,
        string? start = null,
        string? end = null)
    {
        await using var cmd = _session.Connection.CreateCommand();
        cmd.CommandText = sql;
        if (start is not null)
            cmd.Parameters.AddWithValue("$start", start);
        if (end is not null)
            cmd.Parameters.AddWithValue("$end", end);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is null or DBNull ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<GrowthPendingProposal>> ReadPendingAsync(
        SqliteCommand cmd,
        CancellationToken ct)
    {
        var list = new List<GrowthPendingProposal>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new GrowthPendingProposal(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetDouble(2),
                reader.GetDouble(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return list;
    }

    private static string NormalizeTraitId(string traitId)
    {
        var id = traitId.Trim().ToLowerInvariant();
        if (!GrowthTraits.IsNudgeable(id))
            throw new ArgumentException($"Unknown growth trait '{traitId}'.", nameof(traitId));
        return id;
    }

    private static bool TryParseUtc(string raw, out DateTimeOffset value)
    {
        if (DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out value))
            return true;
        value = default;
        return false;
    }
}
