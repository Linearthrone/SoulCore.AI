using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

public sealed record TaskWriteResult(bool Ok, string? Problem, PresenceTaskRow? Task = null);

/// <summary>
/// Operator task ledger against Host <c>/tasks</c>. Victoria also has <c>task_*</c> tools.
/// </summary>
public sealed class SoulCoreTaskClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseListPayload(json);
        }
        catch (Exception ex) when (
            ex is HttpRequestException
                or TaskCanceledException
                or OperationCanceledException
                or JsonException
                or NotSupportedException)
        {
            return new TaskListFetch(Array.Empty<PresenceTaskRow>(), $"Tasks unavailable: {ex.GetType().Name}");
        }
    }

    /// <summary>Parse Host GET /tasks JSON. Missing <c>tasks</c> is an error, not an empty ledger.</summary>
    internal static TaskListFetch ParseListPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new TaskListFetch(Array.Empty<PresenceTaskRow>(), "Task list response was empty.");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new TaskListFetch(Array.Empty<PresenceTaskRow>(), "Task list response was not an object.");

            if (!TryGetPropertyIgnoreCase(root, "tasks", out var tasksEl))
                return new TaskListFetch(Array.Empty<PresenceTaskRow>(), "Task list response missing tasks[].");

            if (tasksEl.ValueKind == JsonValueKind.Null)
                return new TaskListFetch(Array.Empty<PresenceTaskRow>(), "Task list response had null tasks[].");

            if (tasksEl.ValueKind != JsonValueKind.Array)
                return new TaskListFetch(Array.Empty<PresenceTaskRow>(), "Task list response tasks was not an array.");

            var rows = new List<PresenceTaskRow>();
            foreach (var el in tasksEl.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                    continue;
                rows.Add(new PresenceTaskRow
                {
                    Id = ReadLong(el, "id"),
                    Title = ReadString(el, "title"),
                    Status = ReadString(el, "status"),
                    Surface = ReadString(el, "surface")
                });
            }

            return new TaskListFetch(rows, null);
        }
        catch (JsonException ex)
        {
            return new TaskListFetch(Array.Empty<PresenceTaskRow>(), $"Task list JSON error: {ex.Message}");
        }
    }

    public async Task<TaskWriteResult> CreateAsync(
        string title,
        bool ready = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return new TaskWriteResult(false, "Title is required.");
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return new TaskWriteResult(false, "Tasks stay on this machine.");

        try
        {
            using var response = await _http.PostAsJsonAsync(
                TasksUri(),
                new { title = title.Trim(), ready },
                JsonOpts,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new TaskWriteResult(false, await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false));

            var body = await response.Content.ReadFromJsonAsync<TaskWriteDto>(JsonOpts, cancellationToken)
                .ConfigureAwait(false);
            return new TaskWriteResult(true, null, ToRow(body?.Task) ?? new PresenceTaskRow
            {
                Id = body?.Id ?? 0,
                Title = title.Trim(),
                Status = ready ? "todo" : "suspended"
            });
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return new TaskWriteResult(false, $"Create failed: {ex.GetType().Name}");
        }
    }

    public async Task<TaskWriteResult> SuspendAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return new TaskWriteResult(false, "Invalid task.");

        try
        {
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(TasksUri(id, "suspend"), content, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new TaskWriteResult(false, await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false));
            return new TaskWriteResult(true, null);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new TaskWriteResult(false, $"Suspend failed: {ex.GetType().Name}");
        }
    }

    public async Task<TaskWriteResult> ReleaseAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return new TaskWriteResult(false, "Invalid task.");

        try
        {
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(TasksUri(id, "release"), content, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new TaskWriteResult(false, await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false));
            return new TaskWriteResult(true, null);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new TaskWriteResult(false, $"Release failed: {ex.GetType().Name}");
        }
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return false;

        try
        {
            using var response = await _http.DeleteAsync(TasksUri(id), cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return false;
        }
    }

    public void Dispose() => _http.Dispose();

    private static Uri TasksUri(long? id = null, string? action = null)
    {
        var path = id is { } n
            ? (string.IsNullOrWhiteSpace(action) ? $"/tasks/{n}" : $"/tasks/{n}/{action}")
            : "/tasks";
        return new($"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}{path}");
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                if (TryGetPropertyIgnoreCase(doc.RootElement, "error", out var err)
                    && err.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(err.GetString()))
                    return err.GetString()!;
            }
        }
        catch
        {
            // fall through
        }

        return response.StatusCode == System.Net.HttpStatusCode.NotFound
            ? "Task APIs need a Host restart."
            : $"Tasks HTTP {(int)response.StatusCode}";
    }

    private static PresenceTaskRow? ToRow(TaskDto? t) =>
        t is null
            ? null
            : new PresenceTaskRow
            {
                Id = t.Id,
                Title = t.Title ?? "",
                Status = t.Status ?? "",
                Surface = t.Surface ?? ""
            };

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string name, out JsonElement value)
    {
        if (root.TryGetProperty(name, out value))
            return true;
        foreach (var prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static long ReadLong(JsonElement el, string name)
    {
        if (!TryGetPropertyIgnoreCase(el, name, out var p))
            return 0;
        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetInt64(out var n) => n,
            JsonValueKind.String when long.TryParse(p.GetString(), out var n) => n,
            _ => 0
        };
    }

    private static string ReadString(JsonElement el, string name)
    {
        if (!TryGetPropertyIgnoreCase(el, name, out var p) || p.ValueKind != JsonValueKind.String)
            return "";
        return p.GetString() ?? "";
    }

    private sealed class TaskWriteDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("task")]
        public TaskDto? Task { get; set; }
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
