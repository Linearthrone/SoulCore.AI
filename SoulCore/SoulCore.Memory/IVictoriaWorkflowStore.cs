using System.Text.Json;

namespace SoulCore.Memory;

/// <summary>
/// Victoria's own lightweight workflow store (BED-141). Backed by the
/// <c>victoria_workflows</c> SQLite table in the memory DB. A workflow is a
/// named ordered list of steps (description + optional tool name + optional
/// tool args). Execution is model-initiated via <c>workflow_execute</c> — not
/// auto-run by SoulLoop.
/// </summary>
public interface IVictoriaWorkflowStore
{
    /// <summary>
    /// Insert a workflow with <c>current_step=0</c>. Returns the new row id.
    /// </summary>
    Task<long> CreateAsync(
        string name,
        IReadOnlyList<WorkflowStep> steps,
        CancellationToken cancellationToken = default);

    /// <summary>Load a workflow by id, or <c>null</c> when missing.</summary>
    Task<VictoriaWorkflow?> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist a new <c>current_step</c> (and <c>updated_at</c>). Returns
    /// <c>false</c> when the id does not exist.
    /// </summary>
    Task<bool> SetCurrentStepAsync(
        long id,
        int currentStep,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One step in a Victoria workflow (ordered list, not a DAG).
/// <paramref name="Args"/> is an optional JSON object of nested tool parameters
/// (<see cref="JsonValueKind.Undefined"/> / non-object when absent). When args
/// are missing, <c>workflow_execute</c> maps <see cref="Description"/> into the
/// target tool's primary string parameter (BED-159).
/// </summary>
public sealed record WorkflowStep(string Description, string? Tool, JsonElement Args = default);

/// <summary>One row from <c>victoria_workflows</c>.</summary>
public sealed record VictoriaWorkflow(
    long Id,
    string Name,
    IReadOnlyList<WorkflowStep> Steps,
    int CurrentStep,
    string CreatedAt,
    string UpdatedAt);
