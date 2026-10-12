using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoulCore.Inference.Presence;

/// <summary>
/// One piece of work Kayleigh asked for that should keep going while she is quiet.
/// </summary>
public sealed class AssignedWork
{
    public string Text { get; init; } = "";
    public string Status { get; init; } = "active";
    public string? Note { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Durable open assignment. Survives a host restart so a lesson does not die with the chat turn.
/// </summary>
public interface IAssignedWorkStore
{
    AssignedWork? Current { get; }
    void SetActive(string text);
    void MarkWaiting(string note);
    void Clear();
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
    private AssignedWork? _current;

    public FileAssignedWorkStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SoulCore",
                "assigned-work.json")
            : path;
        _current = Load();
    }

    public AssignedWork? Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public void SetActive(string text)
    {
        var body = (text ?? "").Trim();
        if (body.Length == 0)
            return;
        lock (_gate)
        {
            _current = new AssignedWork
            {
                Text = body,
                Status = "active",
                UpdatedAt = DateTimeOffset.UtcNow
            };
            Save();
        }
    }

    public void MarkWaiting(string note)
    {
        lock (_gate)
        {
            if (_current is null)
                return;
            var n = (note ?? "").Trim();
            if (n.Length > 240)
                n = n[..240];
            _current = new AssignedWork
            {
                Text = _current.Text,
                Status = "waiting",
                Note = n,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            Save();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _current = null;
            SaveCleared();
        }
    }

    private AssignedWork? Load()
    {
        try
        {
            if (!File.Exists(_path))
                return null;
            var dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(_path), Json);
            if (dto is null || !string.Equals(dto.Status, "active", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(dto.Status, "waiting", StringComparison.OrdinalIgnoreCase))
                return null;
            if (string.IsNullOrWhiteSpace(dto.Text))
                return null;
            return new AssignedWork
            {
                Text = dto.Text.Trim(),
                Status = dto.Status.Trim().ToLowerInvariant(),
                Note = dto.Note,
                UpdatedAt = dto.UpdatedAt
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void Save()
    {
        if (_current is null)
        {
            SaveCleared();
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var dto = new FileDto
        {
            Text = _current.Text,
            Status = _current.Status,
            Note = _current.Note,
            UpdatedAt = _current.UpdatedAt
        };
        File.WriteAllText(_path, JsonSerializer.Serialize(dto, Json));
    }

    private void SaveCleared()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(new FileDto
            {
                Text = "",
                Status = "cleared",
                UpdatedAt = DateTimeOffset.UtcNow
            }, Json));
        }
        catch (Exception)
        {
            // A failed clear must not throw out of a chat turn.
        }
    }

    private sealed class FileDto
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
