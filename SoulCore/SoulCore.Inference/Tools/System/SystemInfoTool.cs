using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Abstractions;

// Namespace is Meta (not System) so sibling tools under Tools.Body etc. can still
// resolve BCL System.* without colliding with SoulCore.Inference.Tools.System.
namespace SoulCore.Inference.Tools.Meta;

/// <summary>
/// <c>system_info</c> tool — host build, model, uptime, memory count, SoulLoop
/// status. Same shape as the <c>/health</c> summary but for the model to ask.
/// <b>Never</b> emits secrets (no API keys, tokens, or connection strings).
/// </summary>
public sealed class SystemInfoTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{}}""")
        .RootElement.Clone();

    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;
    private static readonly string HostVersion = typeof(SystemInfoTool).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

    private readonly IOptions<InferenceOptions> _inference;
    private readonly IOptions<SoulLoopOptions> _soulLoop;
    private readonly IMemoryStats? _memoryStats;

    public SystemInfoTool(
        IOptions<InferenceOptions> inference,
        IOptions<SoulLoopOptions> soulLoop,
        IMemoryStats? memoryStats = null)
    {
        _inference = inference ?? throw new ArgumentNullException(nameof(inference));
        _soulLoop = soulLoop ?? throw new ArgumentNullException(nameof(soulLoop));
        _memoryStats = memoryStats;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "system_info",
        Description: "Get SoulCore system status (model, uptime, memory, safety).",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var inf = _inference.Value;
        var loop = _soulLoop.Value;

        long memoryCount = 0;
        bool memoryOpen = false;
        if (_memoryStats is not null)
        {
            memoryOpen = _memoryStats.IsOpen;
            try
            {
                memoryCount = await _memoryStats.CountEpisodicAsync(ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                memoryCount = -1;
            }
        }

        var uptime = DateTimeOffset.UtcNow - StartedAt;

        var sb = new StringBuilder();
        sb.AppendLine($"service: SoulCore.Host");
        sb.AppendLine($"version: {HostVersion}");
        sb.AppendLine($"uptime_seconds: {(long)uptime.TotalSeconds}");
        sb.AppendLine($"inference: {(inf.Enabled ? "ollama" : "null")}");
        sb.AppendLine($"model: {(inf.Enabled ? inf.Model : "(disabled)")}");
        sb.AppendLine($"embeddings_enabled: {inf.EmbeddingsEnabled}");
        sb.AppendLine($"embedding_model: {inf.EmbeddingModel}");
        sb.AppendLine($"soul_loop_enabled: {loop.Enabled}");
        sb.AppendLine($"soul_loop_tick_interval: {loop.TickIntervalSeconds}s");
        sb.AppendLine($"memory_open: {memoryOpen}");
        sb.AppendLine($"episodic_memory_count: {memoryCount}");

        var data = new Dictionary<string, object?>
        {
            ["service"] = "SoulCore.Host",
            ["version"] = HostVersion,
            ["uptimeSeconds"] = (long)uptime.TotalSeconds,
            ["inference"] = inf.Enabled ? "ollama" : "null",
            ["model"] = inf.Enabled ? inf.Model : null,
            ["embeddingsEnabled"] = inf.EmbeddingsEnabled,
            ["soulLoopEnabled"] = loop.Enabled,
            ["memoryOpen"] = memoryOpen,
            ["episodicMemoryCount"] = memoryCount
        };

        return new ToolResult(
            Success: true,
            Content: sb.ToString().TrimEnd(),
            Data: data);
    }
}
