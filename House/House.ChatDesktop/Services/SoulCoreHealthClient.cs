using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace House.ChatDesktop.Services;

public sealed class SoulCoreHealthSnapshot
{
    public bool Reachable { get; init; }
    public string Status { get; init; } = "unknown";
    public bool MemoryOpen { get; init; }
    public string? MemoryPath { get; init; }
    public bool InferenceEnabled { get; init; }
    public int? Phase { get; init; }
    public string? Detail { get; init; }
    public bool? UnrealEnabled { get; init; }
    public string? UnrealTarget { get; init; }
    public bool? UnrealConnected { get; init; }
    public bool? SoulLoopEnabled { get; init; }
    public string? CharterMode { get; init; }
    public int? CharterAnchors { get; init; }
    public int? CharterLocked { get; init; }
    public bool? CharterFullyLocked { get; init; }
    public int? DriftActiveCount { get; init; }
    public bool? DriftSloExceeded { get; init; }
    public int? DriftOldestMinutes { get; init; }
    public long? SpendTokensIn { get; init; }
    public long? SpendTokensOut { get; init; }
    public decimal? SpendEstimatedCost { get; init; }
    public decimal? SpendMonthlyCap { get; init; }
    public bool? SpendCapExceeded { get; init; }

    /// <summary>Session gate: Victoria may click/type/drag (computer-use write path).</summary>
    public bool? AllowComputerControl { get; init; }

    /// <summary>Session gate: screenshot / list windows.</summary>
    public bool? AllowDesktopCapture { get; init; }

    /// <summary>Desktop backend name from Host (<c>cua</c>/<c>native</c>/<c>hermes</c>).</summary>
    public string? DesktopBackend { get; init; }

    /// <summary>Whether local <c>cua-driver.exe</c> was found on the Host machine.</summary>
    public bool? CuaDriverAvailable { get; init; }

    /// <summary>Resolved cua-driver path when available.</summary>
    public string? CuaDriverPath { get; init; }

    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Alive = host HTTP health answered.</summary>
    public bool Alive => Reachable && Status is "ok" or "degraded";

    /// <summary>Warm = memory open + inference enabled (model path ready).</summary>
    public bool Warm => Alive && MemoryOpen && InferenceEnabled;
}

public sealed class SoulCoreHealthClient : IDisposable
{
    private readonly HttpClient _http;

    public SoulCoreHealthClient()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
    }

    public async Task<SoulCoreHealthSnapshot> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
        {
            return new SoulCoreHealthSnapshot
            {
                Reachable = false,
                Status = "blocked",
                Detail = $"Non-loopback host blocked: {ConnectionDefaults.Host}"
            };
        }

        try
        {
            using var response = await _http.GetAsync(ConnectionDefaults.HealthUri, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new SoulCoreHealthSnapshot
                {
                    Reachable = true,
                    Status = "http_error",
                    Detail = $"HTTP {(int)response.StatusCode}"
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<HealthDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new SoulCoreHealthSnapshot
            {
                Reachable = true,
                Status = dto?.Status ?? "ok",
                MemoryOpen = dto?.Memory?.Open ?? false,
                MemoryPath = dto?.Memory?.Path,
                InferenceEnabled = dto?.Inference?.Enabled ?? false,
                Phase = dto?.Phase,
                UnrealEnabled = dto?.Unreal?.Enabled,
                UnrealTarget = dto?.Unreal?.Target,
                UnrealConnected = dto?.Unreal?.Connected,
                SoulLoopEnabled = dto?.SoulLoop?.Enabled,
                CharterMode = dto?.Charter?.Mode,
                CharterAnchors = dto?.Charter?.Anchors,
                CharterLocked = dto?.Charter?.Locked,
                CharterFullyLocked = dto?.Charter?.FullyLocked,
                DriftActiveCount = dto?.Safety?.Drift?.ActiveDriftCount,
                DriftSloExceeded = dto?.Safety?.Drift?.SloExceeded,
                DriftOldestMinutes = dto?.Safety?.Drift?.OldestDriftMinutes,
                SpendTokensIn = dto?.Safety?.Spend?.TotalTokensIn,
                SpendTokensOut = dto?.Safety?.Spend?.TotalTokensOut,
                SpendEstimatedCost = dto?.Safety?.Spend?.EstimatedCostUsd,
                SpendMonthlyCap = dto?.Safety?.Spend?.MonthlyCapUsd,
                SpendCapExceeded = dto?.Safety?.Spend?.CapExceeded,
                AllowComputerControl = dto?.Tools?.AllowComputerControl,
                AllowDesktopCapture = dto?.Tools?.AllowDesktopCapture,
                DesktopBackend = dto?.Tools?.DesktopBackend,
                CuaDriverAvailable = dto?.Tools?.CuaDriverAvailable,
                CuaDriverPath = dto?.Tools?.CuaDriverPath,
                Detail = null
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new SoulCoreHealthSnapshot
            {
                Reachable = false,
                Status = "unreachable",
                Detail = ex.Message
            };
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed class HealthDto
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("phase")]
        public int? Phase { get; set; }

        [JsonPropertyName("memory")]
        public MemoryDto? Memory { get; set; }

        [JsonPropertyName("inference")]
        public FlagDto? Inference { get; set; }

        [JsonPropertyName("hermes")]
        public FlagDto? Hermes { get; set; }

        [JsonPropertyName("unreal")]
        public UnrealDto? Unreal { get; set; }

        [JsonPropertyName("soulLoop")]
        public SoulLoopDto? SoulLoop { get; set; }

        [JsonPropertyName("charter")]
        public CharterDto? Charter { get; set; }

        [JsonPropertyName("safety")]
        public SafetyDto? Safety { get; set; }

        [JsonPropertyName("tools")]
        public ToolsDto? Tools { get; set; }
    }

    private sealed class ToolsDto
    {
        [JsonPropertyName("allowComputerControl")]
        public bool? AllowComputerControl { get; set; }

        [JsonPropertyName("allowDesktopCapture")]
        public bool? AllowDesktopCapture { get; set; }

        [JsonPropertyName("desktopBackend")]
        public string? DesktopBackend { get; set; }

        [JsonPropertyName("cuaDriverAvailable")]
        public bool? CuaDriverAvailable { get; set; }

        [JsonPropertyName("cuaDriverPath")]
        public string? CuaDriverPath { get; set; }
    }

    private sealed class CharterDto
    {
        [JsonPropertyName("anchors")]
        public int? Anchors { get; set; }

        [JsonPropertyName("locked")]
        public int? Locked { get; set; }

        [JsonPropertyName("fullyLocked")]
        public bool? FullyLocked { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode { get; set; }
    }

    private sealed class MemoryDto
    {
        [JsonPropertyName("open")]
        public bool Open { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }
    }

    private sealed class FlagDto
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }

    private sealed class UnrealDto
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("target")]
        public string? Target { get; set; }

        [JsonPropertyName("connected")]
        public bool Connected { get; set; }
    }

    private sealed class SoulLoopDto
    {
        [JsonPropertyName("enabled")]
        public bool? Enabled { get; set; }
    }

    private sealed class SafetyDto
    {
        [JsonPropertyName("drift")]
        public DriftDto? Drift { get; set; }

        [JsonPropertyName("spend")]
        public SpendDto? Spend { get; set; }
    }

    private sealed class DriftDto
    {
        [JsonPropertyName("activeDriftCount")]
        public int? ActiveDriftCount { get; set; }

        [JsonPropertyName("sloExceeded")]
        public bool? SloExceeded { get; set; }

        [JsonPropertyName("oldestDriftMinutes")]
        public int? OldestDriftMinutes { get; set; }
    }

    private sealed class SpendDto
    {
        [JsonPropertyName("totalTokensIn")]
        public long? TotalTokensIn { get; set; }

        [JsonPropertyName("totalTokensOut")]
        public long? TotalTokensOut { get; set; }

        [JsonPropertyName("estimatedCostUsd")]
        public decimal? EstimatedCostUsd { get; set; }

        [JsonPropertyName("monthlyCapUsd")]
        public decimal? MonthlyCapUsd { get; set; }

        [JsonPropertyName("capExceeded")]
        public bool? CapExceeded { get; set; }
    }
}
