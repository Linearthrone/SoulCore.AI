using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SoulCore.Memory;

namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-17.1: durable pointer to the running victoria_tasks row.
/// File shape is only <c>{ "runningId": &lt;long|null&gt; }</c>.
/// </summary>
public interface IAssignedWorkStore
{
    /// <summary>Currently running task id, or null when idle.</summary>
    long? RunningId { get; }

    /// <summary>Legacy view for chat activity lines (title + status derived from the ledger).</summary>
    AssignedWork? Current { get; }

    void SetRunning(long? id);
    void Project(VictoriaTask task);
    void Clear();

    /// <summary>
    /// Migrate legacy active/waiting slot into a victoria_tasks row + pointer.
    /// Safe to call at Host start.
    /// </summary>
    Task MigrateLegacyAsync(IVictoriaTaskStore tasks, CancellationToken cancellationToken = default);
}

/// <summary>Compatibility projection used by activity / chat copy.</summary>
public sealed class AssignedWork
{
    public long Id { get; init; }
    public string Text { get; init; } = "";
    public string Status { get; init; } = "in_progress";
    public string? Note { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class FileAssignedWorkStore : IAssignedWorkStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly object _gate = new();
    private long? _runningId;
    private AssignedWork? _projection;
    private LegacyDto? _legacyPending;

    public FileAssignedWorkStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SoulCore",
                "assigned-work.json")
            : path;
        Load();
    }

    public long? RunningId
    {
        get
        {
            lock (_gate)
                return _runningId;
        }
    }

    public AssignedWork? Current
    {
        get
        {
            lock (_gate)
                return _projection;
        }
    }

    public void SetRunning(long? id)
    {
        lock (_gate)
        {
            _runningId = id is > 0 ? id : null;
            if (_runningId is null)
                _projection = null;
            SavePointer();
        }
    }

    /// <summary>Update the in-memory projection after loading a task row.</summary>
    public void Project(VictoriaTask task)
    {
        lock (_gate)
        {
            _runningId = task.Id;
            _projection = new AssignedWork
            {
                Id = task.Id,
                Text = string.IsNullOrWhiteSpace(task.Title) ? task.Description : task.Title,
                Status = task.Status,
                Note = string.IsNullOrWhiteSpace(task.ParkReason) ? task.Notes : task.ParkReason,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            SavePointer();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _runningId = null;
            _projection = null;
            SavePointer();
        }
    }

    public async Task MigrateLegacyAsync(IVictoriaTaskStore tasks, CancellationToken cancellationToken = default)
    {
        LegacyDto? legacy;
        lock (_gate)
        {
            legacy = _legacyPending;
            _legacyPending = null;
        }

        if (legacy is null || string.IsNullOrWhiteSpace(legacy.Text))
            return;
        if (!string.Equals(legacy.Status, "active", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(legacy.Status, "waiting", StringComparison.OrdinalIgnoreCase))
            return;

        var status = string.Equals(legacy.Status, "waiting", StringComparison.OrdinalIgnoreCase)
            ? "blocked"
            : "in_progress";
        var id = await tasks.CreateAsync(
            title: legacy.Text.Trim().Length > 80 ? legacy.Text.Trim()[..77] + "..." : legacy.Text.Trim(),
            description: legacy.Text.Trim(),
            priority: "high",
            surface: InferSurface(legacy.Text),
            ready: true,
            status: status,
            notes: legacy.Note,
            cancellationToken).ConfigureAwait(false);

        if (string.Equals(status, "in_progress", StringComparison.OrdinalIgnoreCase))
        {
            var row = await tasks.GetAsync(id, cancellationToken).ConfigureAwait(false);
            if (row is not null)
                Project(row);
            else
                SetRunning(id);
        }
        else
        {
            Clear();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;
            var raw = File.ReadAllText(_path);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return;

            // New shape: { runningId }
            if (root.TryGetProperty("runningId", out var rid))
            {
                if (rid.ValueKind == JsonValueKind.Null)
                    _runningId = null;
                else if (rid.ValueKind == JsonValueKind.Number && rid.TryGetInt64(out var n) && n > 0)
                    _runningId = n;
                else if (rid.ValueKind == JsonValueKind.String
                         && long.TryParse(rid.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)
                         && s > 0)
                    _runningId = s;
                return;
            }

            // Legacy shape: { text, status, note, updatedAt }
            var legacy = JsonSerializer.Deserialize<LegacyDto>(raw, Json);
            if (legacy is null || string.IsNullOrWhiteSpace(legacy.Text))
                return;
            if (!string.Equals(legacy.Status, "active", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(legacy.Status, "waiting", StringComparison.OrdinalIgnoreCase))
                return;
            _legacyPending = legacy;
            // Temporary projection until MigrateLegacyAsync runs.
            _projection = new AssignedWork
            {
                Text = legacy.Text.Trim(),
                Status = legacy.Status.Trim().ToLowerInvariant(),
                Note = legacy.Note,
                UpdatedAt = legacy.UpdatedAt
            };
        }
        catch (Exception)
        {
            // Corrupt pointer must not block Host start.
        }
    }

    private void SavePointer()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var dto = new PointerDto { RunningId = _runningId };
            File.WriteAllText(_path, JsonSerializer.Serialize(dto, Json));
        }
        catch (Exception)
        {
            // A failed clear must not throw out of a chat turn.
        }
    }

    private static string InferSurface(string text)
    {
        var t = text.ToLowerInvariant();
        if (t.Contains("maestro", StringComparison.Ordinal))
            return "maestro";
        if (t.Contains("browser", StringComparison.Ordinal) || t.Contains("firefox", StringComparison.Ordinal)
            || t.Contains("http", StringComparison.Ordinal))
            return "browser";
        if (t.Contains("vm", StringComparison.Ordinal) || t.Contains("guest", StringComparison.Ordinal)
            || t.Contains("sandbox", StringComparison.Ordinal))
            return "vm";
        return "local";
    }

    private sealed class PointerDto
    {
        [JsonPropertyName("runningId")]
        public long? RunningId { get; set; }
    }

    private sealed class LegacyDto
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
