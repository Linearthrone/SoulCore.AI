using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SoulCore.Memory.Repositories;

public sealed class SqliteVictoriaWorkflowRepository : IVictoriaWorkflowStore
{

    private readonly SqliteMemorySession _session;
    public SqliteVictoriaWorkflowRepository(SqliteMemorySession session) => _session = session ?? throw new ArgumentNullException(nameof(session));


/// <inheritdoc />
public async Task<long> CreateAsync(
    string name,
    IReadOnlyList<WorkflowStep> steps,
    CancellationToken cancellationToken = default)
{

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
    return await _session.RunDbAsync(async ct =>
    {
    await using var cmd = _session.Connection.CreateCommand();
    cmd.CommandText =
        """
        INSERT INTO victoria_workflows (name, steps_json, current_step, created_at, updated_at)
        VALUES ($name, $steps_json, 0, $created_at, $updated_at);
        """;
    cmd.Parameters.AddWithValue("$name", name.Trim());
    cmd.Parameters.AddWithValue("$steps_json", stepsJson);
    cmd.Parameters.AddWithValue("$created_at", now);
    cmd.Parameters.AddWithValue("$updated_at", now);
    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

    await using var idCmd = _session.Connection.CreateCommand();
    idCmd.CommandText = "SELECT last_insert_rowid();";
    var result = await idCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
    if (result is null || result is DBNull)
        throw new InvalidOperationException("Failed to obtain victoria_workflows row id after insert.");
    return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }, cancellationToken).ConfigureAwait(false);
}

/// <inheritdoc />
public async Task<VictoriaWorkflow?> GetAsync(long id, CancellationToken cancellationToken)
{

    if (id <= 0)
        return null;
    return await _session.RunDbAsync(async ct =>
    {
    await using var cmd = _session.Connection.CreateCommand();
    cmd.CommandText =
        """
        SELECT id, name, steps_json, current_step, created_at, updated_at
        FROM victoria_workflows
        WHERE id = $id
        LIMIT 1;
        """;
    cmd.Parameters.AddWithValue("$id", id);

    await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
    if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        return null;

    return ReadVictoriaWorkflow(reader);
    }, cancellationToken).ConfigureAwait(false);
}

/// <inheritdoc />
public async Task<bool> SetCurrentStepAsync(
    long id,
    int currentStep,
    CancellationToken cancellationToken = default)
{

    if (id <= 0)
        return false;
    if (currentStep < 0)
        throw new ArgumentOutOfRangeException(nameof(currentStep), "current_step must be >= 0.");

    var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    return await _session.RunDbAsync(async ct =>
    {
    await using var cmd = _session.Connection.CreateCommand();
    cmd.CommandText =
        """
        UPDATE victoria_workflows
        SET current_step = $current_step, updated_at = $updated_at
        WHERE id = $id;
        """;
    cmd.Parameters.AddWithValue("$current_step", currentStep);
    cmd.Parameters.AddWithValue("$updated_at", now);
    cmd.Parameters.AddWithValue("$id", id);
    var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    return rows > 0;
    }, cancellationToken).ConfigureAwait(false);
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
    var index = 0;
    foreach (var el in doc.RootElement.EnumerateArray())
    {
        if (!WorkflowStepJson.TryParseStep(el, index, out var step, out var error))
            throw new InvalidOperationException(error!);

        list.Add(step);
        index++;
    }

    return list;
}


}
