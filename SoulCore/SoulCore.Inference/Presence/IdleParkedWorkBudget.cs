using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-18.8 / Q3 B3 soft budget for unsupervised parked-task advances.
/// Per local calendar day: at most <see cref="MaxAdvancesPerDay"/> advances or
/// <see cref="MaxToolTime"/> cumulative tool wall-clock (whichever first).
/// Serial: one active parked advance at a time. Leisure / journal / activity-log
/// never call this API and do not consume budget.
/// </summary>
public interface IIdleParkedWorkBudget
{
    /// <summary>True when another parked advance may start (not exhausted, not already serial-busy).</summary>
    bool CanStartAdvance(DateTimeOffset? now = null);

    /// <summary>
    /// Consume one advance for <paramref name="taskId"/>. Returns false when exhausted
    /// or a different parked advance is already active.
    /// </summary>
    bool TryBeginAdvance(long taskId, DateTimeOffset? now = null);

    /// <summary>Add tool wall-clock for the active parked advance (leisure must not call).</summary>
    void RecordToolTime(TimeSpan elapsed, DateTimeOffset? now = null);

    /// <summary>True when cumulative tool time for the day has hit the 45-minute cap.</summary>
    bool IsToolTimeExhausted(DateTimeOffset? now = null);

    /// <summary>Clear the active serial slot when the parked session ends (done/park/block/clear).</summary>
    void EndAdvance(long? taskId = null, DateTimeOffset? now = null);

    IdleParkedWorkBudgetSnapshot GetSnapshot(DateTimeOffset? now = null);
}

/// <summary>Immutable view for /health, Identity, and FED Presence.</summary>
public sealed record IdleParkedWorkBudgetSnapshot(
    string LocalDay,
    int AdvancesUsed,
    int AdvancesCap,
    double ToolMinutesUsed,
    double ToolMinutesCap,
    bool Exhausted,
    long? ActiveTaskId,
    string? ExhaustReason);

/// <summary>
/// File-backed B3 budget. Day key is the host's local calendar date.
/// </summary>
public sealed class FileIdleParkedWorkBudget : IIdleParkedWorkBudget
{
    public const int MaxAdvancesPerDay = 3;
    public static readonly TimeSpan MaxToolTime = TimeSpan.FromMinutes(45);

    /// <summary>
    /// Activity kinds that never consume the parked-work budget (Q3).
    /// </summary>
    public static bool IsExemptKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            return false;
        return kind.Equals("leisure", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("journal", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("activity-log", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("activity_log", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("soulloop", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("soul-loop", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly TimeZoneInfo _localTz;
    private readonly object _gate = new();
    private string _localDay = "";
    private int _advances;
    private double _toolSeconds;
    private long? _activeTaskId;

    public FileIdleParkedWorkBudget(string? path = null, TimeZoneInfo? localTimeZone = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SoulCore",
                "idle-parked-work-budget.json")
            : path;
        _localTz = localTimeZone ?? TimeZoneInfo.Local;
        Load();
        EnsureDay(DateTimeOffset.UtcNow);
    }

    public bool CanStartAdvance(DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            EnsureDay(now ?? DateTimeOffset.UtcNow);
            // New advances only: already at/over either cap, or serial slot busy.
            if (CannotStartNewUnlocked())
                return false;
            if (_activeTaskId is > 0)
                return false;
            return true;
        }
    }

    public bool TryBeginAdvance(long taskId, DateTimeOffset? now = null)
    {
        if (taskId <= 0)
            return false;

        lock (_gate)
        {
            EnsureDay(now ?? DateTimeOffset.UtcNow);

            if (_activeTaskId is > 0)
            {
                // Serial: same task may re-enter (probe pass); a different id is refused.
                if (_activeTaskId.Value == taskId)
                    return true;
                return false;
            }

            if (CannotStartNewUnlocked())
                return false;

            _activeTaskId = taskId;
            _advances++;
            PersistUnlocked();
            return true;
        }
    }

    /// <summary>
    /// True when the open advance should stop because tool wall-clock hit the day cap.
    /// Advance-count exhaustion only blocks <em>new</em> starts, not the current session.
    /// </summary>
    public bool IsToolTimeExhausted(DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            EnsureDay(now ?? DateTimeOffset.UtcNow);
            return TimeSpan.FromSeconds(_toolSeconds) >= MaxToolTime;
        }
    }

    public void RecordToolTime(TimeSpan elapsed, DateTimeOffset? now = null)
    {
        if (elapsed <= TimeSpan.Zero)
            return;

        lock (_gate)
        {
            EnsureDay(now ?? DateTimeOffset.UtcNow);
            // Only parked-budget sessions accumulate tool time.
            if (_activeTaskId is null or <= 0)
                return;
            _toolSeconds += elapsed.TotalSeconds;
            PersistUnlocked();
        }
    }

    public void EndAdvance(long? taskId = null, DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            EnsureDay(now ?? DateTimeOffset.UtcNow);
            if (_activeTaskId is null)
                return;
            if (taskId is > 0 && _activeTaskId.Value != taskId.Value)
                return;
            _activeTaskId = null;
            PersistUnlocked();
        }
    }

    public IdleParkedWorkBudgetSnapshot GetSnapshot(DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            EnsureDay(now ?? DateTimeOffset.UtcNow);
            var toolExhausted = TimeSpan.FromSeconds(_toolSeconds) >= MaxToolTime;
            var advancesExhausted = _advances >= MaxAdvancesPerDay;
            var exhausted = advancesExhausted || toolExhausted;
            string? reason = null;
            if (toolExhausted)
                reason = "tool_time_cap";
            else if (advancesExhausted)
                reason = "advances_cap";

            return new IdleParkedWorkBudgetSnapshot(
                LocalDay: _localDay,
                AdvancesUsed: _advances,
                AdvancesCap: MaxAdvancesPerDay,
                ToolMinutesUsed: Math.Round(_toolSeconds / 60.0, 3),
                ToolMinutesCap: MaxToolTime.TotalMinutes,
                Exhausted: exhausted,
                ActiveTaskId: _activeTaskId,
                ExhaustReason: reason);
        }
    }

    /// <summary>Either cap blocks starting a new parked advance.</summary>
    private bool CannotStartNewUnlocked() =>
        _advances >= MaxAdvancesPerDay
        || TimeSpan.FromSeconds(_toolSeconds) >= MaxToolTime;

    private void EnsureDay(DateTimeOffset utcNow)
    {
        var day = LocalDayKey(utcNow);
        if (string.Equals(_localDay, day, StringComparison.Ordinal))
            return;
        _localDay = day;
        _advances = 0;
        _toolSeconds = 0;
        _activeTaskId = null;
        PersistUnlocked();
    }

    private string LocalDayKey(DateTimeOffset utcNow)
    {
        var local = TimeZoneInfo.ConvertTime(utcNow, _localTz);
        return local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;
            var raw = File.ReadAllText(_path);
            var dto = JsonSerializer.Deserialize<BudgetDto>(raw, Json);
            if (dto is null || string.IsNullOrWhiteSpace(dto.LocalDay))
                return;
            _localDay = dto.LocalDay.Trim();
            _advances = Math.Max(0, dto.Advances);
            _toolSeconds = Math.Max(0, dto.ToolSeconds);
            _activeTaskId = dto.ActiveTaskId is > 0 ? dto.ActiveTaskId : null;
        }
        catch (Exception)
        {
            // Corrupt budget must not block Host start — start fresh for today.
            _localDay = "";
            _advances = 0;
            _toolSeconds = 0;
            _activeTaskId = null;
        }
    }

    private void PersistUnlocked()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var dto = new BudgetDto
            {
                LocalDay = _localDay,
                Advances = _advances,
                ToolSeconds = _toolSeconds,
                ActiveTaskId = _activeTaskId
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(dto, Json));
        }
        catch (Exception)
        {
            // Persistence failure must not throw out of the work loop.
        }
    }

    private sealed class BudgetDto
    {
        [JsonPropertyName("localDay")]
        public string LocalDay { get; set; } = "";

        [JsonPropertyName("advances")]
        public int Advances { get; set; }

        [JsonPropertyName("toolSeconds")]
        public double ToolSeconds { get; set; }

        [JsonPropertyName("activeTaskId")]
        public long? ActiveTaskId { get; set; }
    }
}
