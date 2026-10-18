using Microsoft.Data.Sqlite;

namespace SoulCore.Memory.Repositories;

public sealed class SqliteVictoriaResearchRepository : IVictoriaResearchStore
{
    private readonly SqliteMemorySession _session;

    public SqliteVictoriaResearchRepository(SqliteMemorySession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public Task<IReadOnlyList<VictoriaResearchSubject>> ListSubjectsAsync(
        CancellationToken cancellationToken = default) =>
        _session.RunDbAsync(async ct =>
        {
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT s.id, s.slug, s.title, s.summary, s.created_at, s.updated_at,
                       (SELECT COUNT(*) FROM victoria_research_papers p WHERE p.subject_id = s.id) AS paper_count
                FROM victoria_research_subjects s
                ORDER BY s.updated_at DESC, s.id DESC
                """;
            var rows = new List<VictoriaResearchSubject>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                rows.Add(ReadSubject(reader));
            return (IReadOnlyList<VictoriaResearchSubject>)rows;
        }, cancellationToken);

    public Task<VictoriaResearchSubject?> GetSubjectAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        _session.RunDbAsync(ct => LoadSubjectAsync(id, ct), cancellationToken);

    public Task<VictoriaResearchSubject?> GetSubjectBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default) =>
        _session.RunDbAsync(async ct =>
        {
            var key = (slug ?? "").Trim().ToLowerInvariant();
            if (key.Length == 0)
                return null;
            return await LoadSubjectBySlugAsync(key, ct).ConfigureAwait(false);
        }, cancellationToken);

    public Task<VictoriaResearchSubject> OpenOrCreateSubjectAsync(
        string title,
        string? summary = null,
        CancellationToken cancellationToken = default) =>
        _session.RunDbAsync(async ct =>
        {
            var trimmed = (title ?? "").Trim();
            if (trimmed.Length == 0)
                throw new ArgumentException("title must be non-empty.");
            var slug = VictoriaResearchRules.Slugify(trimmed);
            var existing = await LoadSubjectBySlugAsync(slug, ct).ConfigureAwait(false);
            if (existing is not null)
            {
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    await using var patch = _session.Connection.CreateCommand();
                    patch.CommandText =
                        """
                        UPDATE victoria_research_subjects
                        SET summary = $summary,
                            updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                        WHERE id = $id
                        """;
                    patch.Parameters.AddWithValue("$summary", summary.Trim());
                    patch.Parameters.AddWithValue("$id", existing.Id);
                    await patch.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    return (await LoadSubjectAsync(existing.Id, ct).ConfigureAwait(false))!;
                }

                return existing;
            }

            await using var insert = _session.Connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO victoria_research_subjects (slug, title, summary)
                VALUES ($slug, $title, $summary)
                """;
            insert.Parameters.AddWithValue("$slug", slug);
            insert.Parameters.AddWithValue("$title", trimmed);
            insert.Parameters.AddWithValue("$summary", string.IsNullOrWhiteSpace(summary) ? "" : summary.Trim());
            await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            await using var idCmd = _session.Connection.CreateCommand();
            idCmd.CommandText = "SELECT last_insert_rowid();";
            var id = Convert.ToInt64(await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
            return (await LoadSubjectAsync(id, ct).ConfigureAwait(false))!;
        }, cancellationToken);

    public Task<bool> TouchSubjectAsync(long id, CancellationToken cancellationToken = default) =>
        _session.RunDbAsync(async ct =>
        {
            if (id <= 0)
                return false;
            await using var cmd = _session.Connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE victoria_research_subjects
                SET updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                WHERE id = $id
                """;
            cmd.Parameters.AddWithValue("$id", id);
            var n = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return n > 0;
        }, cancellationToken);

    public Task<IReadOnlyList<VictoriaResearchPaper>> ListPapersAsync(
        long? subjectId = null,
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        _session.RunDbAsync(async ct =>
        {
            var take = Math.Clamp(limit, 1, 200);
            await using var cmd = _session.Connection.CreateCommand();
            if (subjectId is { } sid && sid > 0)
            {
                cmd.CommandText =
                    """
                    SELECT id, subject_id, kind, title, abstract, body_apa, bibliography_apa, status, created_at, updated_at
                    FROM victoria_research_papers
                    WHERE subject_id = $sid
                    ORDER BY updated_at DESC, id DESC
                    LIMIT $limit
                    """;
                cmd.Parameters.AddWithValue("$sid", sid);
            }
            else
            {
                cmd.CommandText =
                    """
                    SELECT id, subject_id, kind, title, abstract, body_apa, bibliography_apa, status, created_at, updated_at
                    FROM victoria_research_papers
                    ORDER BY updated_at DESC, id DESC
                    LIMIT $limit
                    """;
            }

            cmd.Parameters.AddWithValue("$limit", take);
            var rows = new List<VictoriaResearchPaper>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                rows.Add(ReadPaper(reader));
            return (IReadOnlyList<VictoriaResearchPaper>)rows;
        }, cancellationToken);

    public Task<VictoriaResearchPaper?> GetPaperAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        _session.RunDbAsync(ct => LoadPaperAsync(id, ct), cancellationToken);

    public Task<VictoriaResearchPaper> UpsertPaperAsync(
        long subjectId,
        string title,
        string kind,
        string? abstractText,
        string? bodyApa,
        string? bibliographyApa,
        string? status,
        long? paperId = null,
        CancellationToken cancellationToken = default) =>
        _session.RunDbAsync(async ct =>
        {
            if (subjectId <= 0)
                throw new ArgumentException("subjectId must be positive.");
            var trimmedTitle = (title ?? "").Trim();
            if (trimmedTitle.Length == 0)
                throw new ArgumentException("title must be non-empty.");
            var normalizedKind = VictoriaResearchRules.NormalizeKind(kind);
            var normalizedStatus = VictoriaResearchRules.NormalizeStatus(status);
            var abs = abstractText?.Trim() ?? "";
            var body = bodyApa?.Trim() ?? "";
            var bib = bibliographyApa?.Trim() ?? "";

            var subject = await LoadSubjectAsync(subjectId, ct).ConfigureAwait(false)
                ?? throw new ArgumentException($"subject id={subjectId} not found.");

            long id;
            if (paperId is { } existingId && existingId > 0)
            {
                var prior = await LoadPaperAsync(existingId, ct).ConfigureAwait(false)
                    ?? throw new ArgumentException($"paper id={existingId} not found.");
                if (prior.SubjectId != subject.Id)
                    throw new ArgumentException("paper does not belong to that subject.");

                await using var update = _session.Connection.CreateCommand();
                update.CommandText =
                    """
                    UPDATE victoria_research_papers
                    SET kind = $kind,
                        title = $title,
                        abstract = $abstract,
                        body_apa = $body,
                        bibliography_apa = $bib,
                        status = $status,
                        updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                    WHERE id = $id
                    """;
                update.Parameters.AddWithValue("$kind", normalizedKind);
                update.Parameters.AddWithValue("$title", trimmedTitle);
                update.Parameters.AddWithValue("$abstract", abs);
                update.Parameters.AddWithValue("$body", body);
                update.Parameters.AddWithValue("$bib", bib);
                update.Parameters.AddWithValue("$status", normalizedStatus);
                update.Parameters.AddWithValue("$id", existingId);
                await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                id = existingId;
            }
            else
            {
                await using var insert = _session.Connection.CreateCommand();
                insert.CommandText =
                    """
                    INSERT INTO victoria_research_papers
                        (subject_id, kind, title, abstract, body_apa, bibliography_apa, status)
                    VALUES
                        ($subject_id, $kind, $title, $abstract, $body, $bib, $status)
                    """;
                insert.Parameters.AddWithValue("$subject_id", subject.Id);
                insert.Parameters.AddWithValue("$kind", normalizedKind);
                insert.Parameters.AddWithValue("$title", trimmedTitle);
                insert.Parameters.AddWithValue("$abstract", abs);
                insert.Parameters.AddWithValue("$body", body);
                insert.Parameters.AddWithValue("$bib", bib);
                insert.Parameters.AddWithValue("$status", normalizedStatus);
                await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                await using var idCmd = _session.Connection.CreateCommand();
                idCmd.CommandText = "SELECT last_insert_rowid();";
                id = Convert.ToInt64(await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
            }

            await using var touch = _session.Connection.CreateCommand();
            touch.CommandText =
                """
                UPDATE victoria_research_subjects
                SET updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                WHERE id = $id
                """;
            touch.Parameters.AddWithValue("$id", subject.Id);
            await touch.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            return (await LoadPaperAsync(id, ct).ConfigureAwait(false))!;
        }, cancellationToken);

    private async Task<VictoriaResearchSubject?> LoadSubjectAsync(long id, CancellationToken ct)
    {
        if (id <= 0)
            return null;
        await using var cmd = _session.Connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT s.id, s.slug, s.title, s.summary, s.created_at, s.updated_at,
                   (SELECT COUNT(*) FROM victoria_research_papers p WHERE p.subject_id = s.id) AS paper_count
            FROM victoria_research_subjects s
            WHERE s.id = $id
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;
        return ReadSubject(reader);
    }

    private async Task<VictoriaResearchSubject?> LoadSubjectBySlugAsync(string slug, CancellationToken ct)
    {
        await using var cmd = _session.Connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT s.id, s.slug, s.title, s.summary, s.created_at, s.updated_at,
                   (SELECT COUNT(*) FROM victoria_research_papers p WHERE p.subject_id = s.id) AS paper_count
            FROM victoria_research_subjects s
            WHERE s.slug = $slug
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$slug", slug);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;
        return ReadSubject(reader);
    }

    private async Task<VictoriaResearchPaper?> LoadPaperAsync(long id, CancellationToken ct)
    {
        if (id <= 0)
            return null;
        await using var cmd = _session.Connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, subject_id, kind, title, abstract, body_apa, bibliography_apa, status, created_at, updated_at
            FROM victoria_research_papers
            WHERE id = $id
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;
        return ReadPaper(reader);
    }

    private static VictoriaResearchSubject ReadSubject(SqliteDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? "" : reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6)));

    private static VictoriaResearchPaper ReadPaper(SqliteDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? "" : reader.GetString(4),
            reader.IsDBNull(5) ? "" : reader.GetString(5),
            reader.IsDBNull(6) ? "" : reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9));
}
