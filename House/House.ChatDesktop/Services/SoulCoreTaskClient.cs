using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace House.ChatDesktop.Services;

public sealed class PresenceTaskRow
{
    public long Id { get; init; }
    public string Title { get; init; } = "";
    public string Status { get; init; } = "";
    public string Surface { get; init; } = "";

    public string Line
    {
        get
        {
            var title = string.IsNullOrWhiteSpace(Title) ? "(untitled)" : Title.Trim();
            var status = string.IsNullOrWhiteSpace(Status) ? "unknown" : Status.Trim();
            var surface = string.IsNullOrWhiteSpace(Surface) ? "" : $" · {Surface.Trim()}";
            return $"#{Id}  {status}{surface} — {title}";
        }
    }
}

public sealed record TaskListFetch(IReadOnlyList<PresenceTaskRow> Tasks, string? Problem);

/// <summary>
/// Operator task ledger. Delete is a Presence action; there is no model tool for it.
/// </summary>
public sealed class SoulCoreTaskClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(4) };

    public async Task<TaskListFetch> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return new TaskListFetch(Array.Empty<PresenceTaskRow>(), "Tasks stay on this machine.");

        try
        {
            using var response = await _http.GetAsync(TasksUri(), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new TaskListFetch(
                    Array.Empty<PresenceTaskRow>(),
                    response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "Task list needs a Host restart."
                        : $"Tasks HTTP {(int)response.StatusCode}");
            }

            var dto = await response.Content.ReadFromJsonAsync<TaskListDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var rows = (dto?.Tasks ?? new List<TaskDto>())
                .Select(t => new PresenceTaskRow
                {
                    Id = t.Id,
                    Title = t.Title ?? "",
                    Status = t.Status ?? "",
                    Surface = t.Surface ?? ""
                })
                .ToList();
            return new TaskListFetch(rows, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new TaskListFetch(Array.Empty<PresenceTaskRow>(), "Tasks unavailable.");
        }
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return false;

        using var response = await _http.DeleteAsync(TasksUri(id), cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public void Dispose() => _http.Dispose();

    private static Uri TasksUri(long? id = null) =>
        new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/tasks{(id is { } n ? $"/{n}" : "")}");

    private sealed class TaskListDto
    {
        [JsonPropertyName("tasks")]
        public List<TaskDto>? Tasks { get; set; }
    }

    private sealed class TaskDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("surface")]
        public string? Surface { get; set; }
    }
}
