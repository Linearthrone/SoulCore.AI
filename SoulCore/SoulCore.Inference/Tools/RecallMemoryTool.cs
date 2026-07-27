using System.Text;
using System.Text.Json;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools;

/// <summary>
/// Model-callable memory recall tool (BED-131). Wraps
/// <see cref="IMemoryStore.RecallSimilarAsync"/> (semantic top-K via cosine over
/// stored embeddings) when <see cref="IEmbeddingClient.IsEnabled"/> is true, and
/// falls back to <see cref="IMemoryStore.RecallRecentAsync"/> when embeddings are
/// off or the embed call fails / returns an empty vector. The chat handler still
/// preamble-injects a baseline recall — this tool is for **additional,
/// model-initiated** recall within a turn (e.g. "what did we say about QUOKKA?").
/// </summary>
/// <remarks>
/// The tool does not throw on bad args or store failures; it returns a failed
/// <see cref="ToolResult"/> so the agent loop can feed the error back to the
/// model instead of crashing the turn. The <see cref="ToolRegistry"/> also wraps
/// unexpected exceptions, but returning a failed result is cleaner for the
/// model to understand.
/// </remarks>
public sealed class RecallMemoryTool : ITool
{
    /// <summary>Default number of memories to recall when <c>limit</c> is omitted.</summary>
    public const int DefaultLimit = 3;

    /// <summary>Hard cap on <c>limit</c> to bound work per tool call.</summary>
    public const int MaxLimit = 20;

    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IMemoryStore _memory;
    private readonly IEmbeddingClient _embeddings;

    public RecallMemoryTool(IMemoryStore memory, IEmbeddingClient embeddings)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "recall_memory",
        Description: "Recall memories similar to a query. Use when you need to remember something specific.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: recall_memory expects a JSON object with a 'query' string.",
                Data: null);
        }

        if (!args.TryGetProperty("query", out var queryProp) || queryProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(
                Success: false,
                Content: "error: recall_memory requires 'query' (string).",
                Data: null);
        }

        var query = queryProp.GetString();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ToolResult(
                Success: false,
                Content: "error: recall_memory 'query' must be non-empty.",
                Data: null);
        }

        var limit = DefaultLimit;
        if (args.TryGetProperty("limit", out var limitProp)
            && limitProp.ValueKind == JsonValueKind.Number
            && limitProp.TryGetInt32(out var requested)
            && requested > 0)
        {
            limit = Math.Min(requested, MaxLimit);
        }

        IReadOnlyList<string> rows;
        try
        {
            rows = await RecallAsync(query, limit, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: recall_memory failed: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }

        if (rows is null || rows.Count == 0)
        {
            return new ToolResult(
                Success: true,
                Content: "no memories found.",
                Data: new { count = 0, query, limit });
        }

        var content = FormatMemories(rows, query);
        return new ToolResult(
            Success: true,
            Content: content,
            Data: new { count = rows.Count, query, limit, rows });
    }

    /// <summary>
    /// Semantic top-K when embeddings are enabled (embed the query → cosine
    /// <see cref="IMemoryStore.RecallSimilarAsync"/>); recency fallback
    /// (<see cref="IMemoryStore.RecallRecentAsync"/>) when embeddings are off,
    /// the embed call fails, or the embed returns an empty vector. Mirrors the
    /// <c>ChatWebSocketHandler.RecallChatMemoriesAsync</c> fallback strategy so
    /// the tool degrades gracefully in a no-embeddings deployment.
    /// </summary>
    private async Task<IReadOnlyList<string>> RecallAsync(string query, int limit, CancellationToken ct)
    {
        if (!_embeddings.IsEnabled)
        {
            return await _memory.RecallRecentAsync(limit, ct).ConfigureAwait(false);
        }

        try
        {
            var queryVec = await _embeddings.EmbedAsync(query, ct).ConfigureAwait(false);
            if (queryVec.Length == 0)
            {
                return await _memory.RecallRecentAsync(limit, ct).ConfigureAwait(false);
            }

            var similar = await _memory.RecallSimilarAsync(queryVec, limit, ct).ConfigureAwait(false);
            if (similar.Count > 0)
                return similar;

            // Semantic recall returned no hits — fall back to recency.
            return await _memory.RecallRecentAsync(limit, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Embed/semantic failure → recency fallback (best-effort recall).
            return await _memory.RecallRecentAsync(limit, ct).ConfigureAwait(false);
        }
    }

    private static string FormatMemories(IReadOnlyList<string> rows, string query)
    {
        var sb = new StringBuilder(64 + rows.Count * 128);
        sb.Append("Recalled ").Append(rows.Count).Append(" memor")
          .Append(rows.Count == 1 ? "y" : "ies").Append(" for '")
          .Append(query).Append("':");
        for (var i = 0; i < rows.Count; i++)
        {
            var line = (rows[i] ?? string.Empty).Trim();
            if (line.Length == 0)
                continue;
            sb.Append("\n[").Append(i + 1).Append("] ").Append(line);
        }
        return sb.ToString();
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "query": {
              "type": "string",
              "description": "What to remember — a phrase or topic to recall memories about."
            },
            "limit": {
              "type": "integer",
              "description": "Maximum number of memories to return (default 3, capped at 20).",
              "default": 3,
              "minimum": 1
            }
          },
          "required": ["query"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
