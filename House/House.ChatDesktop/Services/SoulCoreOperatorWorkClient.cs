using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace House.ChatDesktop.Services;

/// <summary>PROP-22.8 — one row of <c>/operator/work-items</c> (the operator's own tasks + projects).</summary>
public sealed class OperatorWorkItem
{
    public long Id { get; init; }
    public string Kind { get; init; } = "task";
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public string? Details { get; init; }
    public string? DeadlineUtc { get; init; }
    public int Priority { get; init; } = 3;
    public bool NoCheckin { get; init; }
    public string? PhaseCheckpointsJson { get; init; }
    public string? DeliverablesJson { get; init; }
    public bool NeedsExtraPermissions { get; init; }
    public bool OutsideCollaboration { get; init; }
    public string Status { get; init; } = "open";

    public bool IsProject => string.Equals(Kind, "project", StringComparison.OrdinalIgnoreCase);

    public string KindLabel => IsProject ? "Project" : "Task";

    /// <summary>Local, readable deadline — empty when none was set.</summary>
    public string DeadlineText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DeadlineUtc))
                return "";
            return DateTimeOffset.TryParse(
                DeadlineUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var when)
                ? "due " + when.ToLocalTime().ToString("MMM d · h:mm tt", CultureInfo.CurrentCulture)
                : "due " + DeadlineUtc;
        }
    }

    /// <summary>Second line under the title: priority, deadline, flags.</summary>
    public string MetaLine
    {
        get
        {
            var parts = new List<string> { $"P{Priority}" };
            if (!string.IsNullOrEmpty(DeadlineText)) parts.Add(DeadlineText);
            if (NoCheckin) parts.Add("no check-in");
            if (NeedsExtraPermissions) parts.Add("needs permissions");
            if (OutsideCollaboration) parts.Add("outside help");
            if (!string.Equals(Status, "open", StringComparison.OrdinalIgnoreCase)) parts.Add(Status);
            return string.Join(" · ", parts);
        }
    }
}

public sealed record OperatorWorkListFetch(IReadOnlyList<OperatorWorkItem> Items, string? Problem);

public sealed record OperatorWriteResult(bool Ok, string? Problem, long Id = 0);

/// <summary>What the form collects. Project-only fields are ignored for <c>task</c>.</summary>
public sealed class OperatorWorkItemDraft
{
    public bool IsProject { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset? Deadline { get; init; }
    public int Priority { get; init; } = 3;
    public bool NoCheckin { get; init; }
    public IReadOnlyList<string> PhaseCheckpoints { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Deliverables { get; init; } = Array.Empty<string>();
    public bool NeedsExtraPermissions { get; init; }
    public bool OutsideCollaboration { get; init; }
}

/// <summary>Host <c>/operator/work-items</c> (PROP-22 §3.1). Separate from Victoria's own <c>/tasks</c>.</summary>
public sealed class SoulCoreOperatorWorkClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<OperatorWorkListFetch> ListAsync(CancellationToken ct = default)
    {
        if (!OperatorHttp.IsLoopback)
            return new OperatorWorkListFetch(Array.Empty<OperatorWorkItem>(), "Work items stay on this machine.");

        try
        {
            using var request = OperatorHttp.Request(HttpMethod.Get, "/operator/work-items");
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new OperatorWorkListFetch(
                    Array.Empty<OperatorWorkItem>(),
                    await OperatorHttp.ErrorTextAsync(response, ct).ConfigureAwait(false));
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseList(json);
        }
        catch (Exception ex) when (OperatorHttp.IsExpectedFailure(ex))
        {
            return new OperatorWorkListFetch(Array.Empty<OperatorWorkItem>(), $"Work items unavailable: {ex.GetType().Name}");
        }
    }

    public async Task<OperatorWriteResult> CreateAsync(OperatorWorkItemDraft draft, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(draft.Title))
            return new OperatorWriteResult(false, "Title is required.");
        if (!OperatorHttp.IsLoopback)
            return new OperatorWriteResult(false, "Work items stay on this machine.");

        try
        {
            using var request = OperatorHttp.Request(HttpMethod.Post, "/operator/work-items", BuildCreateBody(draft));
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new OperatorWriteResult(false, await OperatorHttp.ErrorTextAsync(response, ct).ConfigureAwait(false));

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return new OperatorWriteResult(true, null, OperatorHttp.Long(doc.RootElement, "id"));
        }
        catch (Exception ex) when (OperatorHttp.IsExpectedFailure(ex))
        {
            return new OperatorWriteResult(false, $"Create failed: {ex.GetType().Name}");
        }
    }

    public async Task<OperatorWriteResult> DeleteAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0)
            return new OperatorWriteResult(false, "Invalid item.");
        if (!OperatorHttp.IsLoopback)
            return new OperatorWriteResult(false, "Work items stay on this machine.");

        try
        {
            using var request = OperatorHttp.Request(HttpMethod.Delete, $"/operator/work-items/{id}");
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? new OperatorWriteResult(true, null, id)
                : new OperatorWriteResult(false, await OperatorHttp.ErrorTextAsync(response, ct).ConfigureAwait(false));
        }
        catch (Exception ex) when (OperatorHttp.IsExpectedFailure(ex))
        {
            return new OperatorWriteResult(false, $"Delete failed: {ex.GetType().Name}");
        }
    }

    /// <summary>Body per contract §3.1 (snake_case columns). Project-only columns are omitted for tasks.</summary>
    internal static Dictionary<string, object?> BuildCreateBody(OperatorWorkItemDraft d)
    {
        var body = new Dictionary<string, object?>
        {
            ["kind"] = d.IsProject ? "project" : "task",
            ["title"] = d.Title.Trim(),
            ["priority"] = Math.Clamp(d.Priority, 1, 5),
            ["no_checkin"] = d.NoCheckin ? 1 : 0,
            ["needs_extra_permissions"] = d.NeedsExtraPermissions ? 1 : 0,
            ["outside_collaboration"] = d.OutsideCollaboration ? 1 : 0,
            ["status"] = "open"
        };

        if (!string.IsNullOrWhiteSpace(d.Description))
            body["description"] = d.Description.Trim();
        if (!string.IsNullOrWhiteSpace(d.Details))
            body["details"] = d.Details.Trim();
        if (d.Deadline is { } deadline)
        {
            body["deadline_utc"] = deadline.UtcDateTime.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                CultureInfo.InvariantCulture);
        }

        if (d.IsProject)
        {
            if (d.PhaseCheckpoints.Count > 0)
                body["phase_checkpoints_json"] = JsonSerializer.Serialize(d.PhaseCheckpoints);
            if (d.Deliverables.Count > 0)
                body["deliverables_json"] = JsonSerializer.Serialize(d.Deliverables);
        }

        return body;
    }

    internal static OperatorWorkListFetch ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new OperatorWorkListFetch(Array.Empty<OperatorWorkItem>(), "Work item response was empty.");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                return new OperatorWorkListFetch(Array.Empty<OperatorWorkItem>(), "Work item response missing items[].");
            }

            var rows = new List<OperatorWorkItem>();
            foreach (var el in items.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                    continue;
                rows.Add(new OperatorWorkItem
                {
                    Id = OperatorHttp.Long(el, "id"),
                    Kind = OperatorHttp.Str(el, "kind") is { Length: > 0 } k ? k : "task",
                    Title = OperatorHttp.Str(el, "title"),
                    Description = OperatorHttp.StrOrNull(el, "description"),
                    Details = OperatorHttp.StrOrNull(el, "details"),
                    DeadlineUtc = OperatorHttp.StrOrNull(el, "deadline_utc"),
                    Priority = OperatorHttp.Int(el, "priority", 3),
                    NoCheckin = OperatorHttp.Bool(el, "no_checkin"),
                    PhaseCheckpointsJson = OperatorHttp.StrOrNull(el, "phase_checkpoints_json"),
                    DeliverablesJson = OperatorHttp.StrOrNull(el, "deliverables_json"),
                    NeedsExtraPermissions = OperatorHttp.Bool(el, "needs_extra_permissions"),
                    OutsideCollaboration = OperatorHttp.Bool(el, "outside_collaboration"),
                    Status = OperatorHttp.Str(el, "status") is { Length: > 0 } s ? s : "open"
                });
            }

            return new OperatorWorkListFetch(rows, null);
        }
        catch (JsonException ex)
        {
            return new OperatorWorkListFetch(Array.Empty<OperatorWorkItem>(), $"Work item JSON error: {ex.Message}");
        }
    }

    public void Dispose() => _http.Dispose();
}
