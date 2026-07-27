using System.Text.Json;
using SoulCore.Memory;

namespace SoulCore.Inference.Tools;

/// <summary>
/// Model-callable memory store tool (BED-131). Wraps
/// <see cref="IMemoryStore.WriteEpisodicAsync"/> — the same episodic store path
/// used by <c>ChatWebSocketHandler.AuthorChatEpisodicAsync</c> (BED-108) for
/// model-authored memories. Writes the row with <c>source='chat'</c> (the
/// schema-valid label that means "model-authored episodic memory", distinct from
/// <c>source='self'</c> used by the SoulLoop for loop-authored reflections) and
/// computes an embedding via <see cref="IEmbeddingClient"/> when enabled.
/// </summary>
/// <remarks>
/// <para>
/// The ticket (TASK-131) specified <c>source='model'</c>, but that value is NOT
/// in the schema CHECK constraint (<c>'self','chat','imported','observation','correction','system'</c>)
/// nor in <c>SqliteMemoryStore.AllowedSources</c>. Writing it literally would be
/// rejected by SQLite; <c>NormalizeSource</c> would silently coerce it to
/// <c>'system'</c>. The existing convention already distinguishes model-authored
/// from loop-authored: SoulLoop writes <c>source='self'</c>; the chat path's
/// model-authored episodics write <c>source='chat'</c>. This tool reuses
/// <c>'chat'</c> so <c>store_memory</c> lands in the same provenance bucket as
/// other model-authored memories, and remains distinct from SoulLoop's
/// <c>'self'</c>. A dedicated <c>'model'</c> label would require a DBD schema
/// migration (003) + <c>AllowedSources</c> update — tracked in ISSUE-... as a
/// follow-up; this tool will switch to <c>'model'</c> when that migration lands.
/// </para>
/// <para>
/// The tool does not throw on bad args or store/embedding failures; it returns a
/// failed <see cref="ToolResult"/> so the agent loop can feed the error back to
/// the model. Embedding failure does NOT fail the write — the episodic row is
/// already persisted; only the optional vector store is skipped (mirrors the
/// chat handler's best-effort embedding path).
/// </para>
/// <para>
/// <c>tags</c> are not a column on <see cref="IMemoryStore.WriteEpisodicAsync"/>
/// (which only takes <c>text</c> + <c>sourceLabel</c>). The schema has a
/// <c>labels_json</c> column but no method exposes it; rather than change the
/// <c>IMemoryStore</c> interface (out of this ticket's lane), tags are folded
/// into the stored content as a trailing <c>[tags: ...]</c> suffix so they are
/// searchable in recall without a schema change.
/// </para>
/// </remarks>
public sealed class StoreMemoryTool : ITool
{
    /// <summary>
    /// Source label written for model-authored <c>store_memory</c> rows.
    /// Schema-valid and distinct from <c>'self'</c> (SoulLoop-authored).
    /// </summary>
    public const string SourceLabel = "chat";

    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IMemoryStore _memory;
    private readonly IEmbeddingClient _embeddings;

    public StoreMemoryTool(IMemoryStore memory, IEmbeddingClient embeddings)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "store_memory",
        Description: "Store a memory for later recall. Use for things the user says you should remember.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: store_memory expects a JSON object with a 'content' string.",
                Data: null);
        }

        if (!args.TryGetProperty("content", out var contentProp) || contentProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(
                Success: false,
                Content: "error: store_memory requires 'content' (string).",
                Data: null);
        }

        var content = contentProp.GetString();
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ToolResult(
                Success: false,
                Content: "error: store_memory 'content' must be non-empty.",
                Data: null);
        }

        var tags = ExtractTags(args);
        var textToStore = ComposeStoredText(content!, tags);

        long id;
        try
        {
            id = await _memory.WriteEpisodicAsync(textToStore, SourceLabel, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(
                Success: false,
                Content: $"error: store_memory write failed: {ex.GetType().Name}: {ex.Message}",
                Data: null);
        }

        // Best-effort embedding — never fails the store. Mirrors the chat
        // handler's post-chat embedding path (BED-108).
        var embedded = false;
        if (_embeddings.IsEnabled)
        {
            try
            {
                var vector = await _embeddings.EmbedAsync(textToStore, ct).ConfigureAwait(false);
                if (vector.Length > 0)
                {
                    await _memory
                        .StoreEmbeddingAsync(id, vector, _embeddings.Model, ct)
                        .ConfigureAwait(false);
                    embedded = true;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Embedding failure — episodic row is already persisted; keep it.
                embedded = false;
            }
        }

        return new ToolResult(
            Success: true,
            Content: $"stored: id={id}",
            Data: new { id, source = SourceLabel, embedded, tags });
    }

    private static IReadOnlyList<string> ExtractTags(JsonElement args)
    {
        if (!args.TryGetProperty("tags", out var tagsProp))
            return Array.Empty<string>();

        if (tagsProp.ValueKind == JsonValueKind.Null)
            return Array.Empty<string>();

        if (tagsProp.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>(tagsProp.GetArrayLength());
        foreach (var item in tagsProp.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s!.Trim());
            }
        }
        return list;
    }

    /// <summary>
    /// Folds optional tags into the stored content as a trailing
    /// <c>[tags: a, b]</c> suffix so they are searchable via recall without a
    /// schema/interface change. When no tags are present, the content is
    /// stored verbatim.
    /// </summary>
    private static string ComposeStoredText(string content, IReadOnlyList<string> tags)
    {
        if (tags is null || tags.Count == 0)
            return content.Trim();

        var joined = string.Join(", ", tags);
        return $"{content.Trim()} [tags: {joined}]";
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "content": {
              "type": "string",
              "description": "The memory to store, written in first person (e.g. 'I remember that the user prefers tea over coffee.')."
            },
            "tags": {
              "type": "array",
              "items": { "type": "string" },
              "description": "Optional tags to help recall this memory later."
            }
          },
          "required": ["content"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
