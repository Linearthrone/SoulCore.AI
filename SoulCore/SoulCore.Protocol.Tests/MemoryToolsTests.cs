using System.Text.Json;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Inference.Tools;
using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

public class MemoryToolsTests
{
    // ─────────────────────────────────────────────────────────────────────
    // recall_memory
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecallMemory_SemanticWhenEmbeddingsOn_ReturnsSeededContent()
    {
        var memory = new FakeMemoryStore
        {
            SimilarResults = new List<string>
            {
                "I remember the user prefers tea over coffee.",
                "We discussed QUOKKA yesterday."
            }
        };
        var embeddings = new FakeEmbeddingClient(isEnabled: true, vector: new float[] { 0.1f, 0.2f, 0.3f });
        var tool = new RecallMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"query":"tea preferences","limit":5}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("tea over coffee", result.Content);
        Assert.Contains("QUOKKA", result.Content);
        Assert.True(memory.SimilarCalled, "RecallSimilarAsync should be called when embeddings are enabled");
        Assert.False(memory.RecentCalled, "RecallRecentAsync should not be called when semantic returns hits");
        Assert.Equal(5, memory.SimilarLimit);
        Assert.Equal(1, embeddings.EmbedCallCount);
    }

    [Fact]
    public async Task RecallMemory_FallsBackToRecentWhenEmbeddingsOff()
    {
        var memory = new FakeMemoryStore
        {
            RecentResults = new List<string> { "recent memory one", "recent memory two" }
        };
        var embeddings = new FakeEmbeddingClient(isEnabled: false);
        var tool = new RecallMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"query":"anything"}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("recent memory one", result.Content);
        Assert.Contains("recent memory two", result.Content);
        Assert.False(memory.SimilarCalled, "RecallSimilarAsync must not be called when embeddings are off");
        Assert.True(memory.RecentCalled, "RecallRecentAsync should be the fallback");
        Assert.Equal(0, embeddings.EmbedCallCount);
    }

    [Fact]
    public async Task RecallMemory_FallsBackToRecentWhenSemanticReturnsEmpty()
    {
        var memory = new FakeMemoryStore
        {
            SimilarResults = new List<string>(),
            RecentResults = new List<string> { "recent fallback hit" }
        };
        var embeddings = new FakeEmbeddingClient(isEnabled: true, vector: new float[] { 1f });
        var tool = new RecallMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"query":"obscure"}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("recent fallback hit", result.Content);
        Assert.True(memory.SimilarCalled);
        Assert.True(memory.RecentCalled, "Should fall back to recency when semantic returns no hits");
    }

    [Fact]
    public async Task RecallMemory_FallsBackToRecentWhenEmbedThrows()
    {
        var memory = new FakeMemoryStore
        {
            RecentResults = new List<string> { "recency after embed failure" }
        };
        var embeddings = new FakeEmbeddingClient(isEnabled: true, throwOnEmbed: true);
        var tool = new RecallMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"query":"x"}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("recency after embed failure", result.Content);
        Assert.False(memory.SimilarCalled, "RecallSimilarAsync should not be reached when embed throws");
        Assert.True(memory.RecentCalled, "Should fall back to recency on embed failure");
    }

    [Fact]
    public async Task RecallMemory_EmptyResults_ReturnsNoMemoriesFound()
    {
        var memory = new FakeMemoryStore
        {
            SimilarResults = new List<string>(),
            RecentResults = new List<string>()
        };
        var embeddings = new FakeEmbeddingClient(isEnabled: true, vector: new float[] { 1f });
        var tool = new RecallMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"query":"nothing here"}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Contains("no memories found", result.Content);
    }

    [Fact]
    public async Task RecallMemory_MissingQuery_ReturnsFailedResult_DoesNotThrow()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: true);
        var tool = new RecallMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"limit":3}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("error:", result.Content);
        Assert.Contains("query", result.Content);
        Assert.False(memory.SimilarCalled);
        Assert.False(memory.RecentCalled);
    }

    [Fact]
    public async Task RecallMemory_EmptyQuery_ReturnsFailedResult()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: true);
        var tool = new RecallMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"query":"   "}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("non-empty", result.Content);
    }

    [Fact]
    public async Task RecallMemory_NonObjectArgs_ReturnsFailedResult()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: true);
        var tool = new RecallMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("\"not an object\"").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("error:", result.Content);
    }

    [Fact]
    public async Task RecallMemory_DefaultsLimitTo3_WhenOmitted()
    {
        var memory = new FakeMemoryStore
        {
            SimilarResults = new List<string> { "hit" }
        };
        var embeddings = new FakeEmbeddingClient(isEnabled: true, vector: new float[] { 1f });
        var tool = new RecallMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"query":"q"}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Equal(RecallMemoryTool.DefaultLimit, memory.SimilarLimit);
    }

    [Fact]
    public async Task RecallMemory_CapsLimitAtMax()
    {
        var memory = new FakeMemoryStore
        {
            SimilarResults = new List<string> { "hit" }
        };
        var embeddings = new FakeEmbeddingClient(isEnabled: true, vector: new float[] { 1f });
        var tool = new RecallMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"query":"q","limit":500}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Equal(RecallMemoryTool.MaxLimit, memory.SimilarLimit);
    }

    [Fact]
    public void RecallMemory_Definition_HasCorrectNameDescriptionAndSchema()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: false);
        var tool = new RecallMemoryTool(memory, embeddings);

        var def = tool.Definition;
        Assert.Equal("recall_memory", def.Name);
        Assert.Contains("Recall memories", def.Description);

        // JSON Schema shape: type=object, properties.query.type=string, required=[query]
        Assert.Equal(JsonValueKind.Object, def.Parameters.ValueKind);
        Assert.Equal("object", def.Parameters.GetProperty("type").GetString());
        var props = def.Parameters.GetProperty("properties");
        Assert.Equal("string", props.GetProperty("query").GetProperty("type").GetString());
        Assert.Equal("integer", props.GetProperty("limit").GetProperty("type").GetString());
        var required = def.Parameters.GetProperty("required");
        Assert.Equal(1, required.GetArrayLength());
        Assert.Equal("query", required[0].GetString());
    }

    [Fact]
    public async Task RecallMemory_RegistersInToolRegistry_GetDefinitionsIncludesIt()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: false);
        var recall = new RecallMemoryTool(memory, embeddings);
        var registry = new ToolRegistry(new ITool[] { recall });

        var defs = registry.GetDefinitions();
        Assert.Contains(defs, d => d.Name == "recall_memory");

        var args = JsonDocument.Parse("""{"query":"x"}""").RootElement.Clone();
        var result = await registry.ExecuteAsync("recall_memory", args);
        Assert.True(result.Success);
    }

    // ─────────────────────────────────────────────────────────────────────
    // store_memory
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StoreMemory_WritesRowWithModelAuthoredSource()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: true, vector: new float[] { 0.5f, 0.5f });
        var tool = new StoreMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse(
            """{"content":"I remember the user likes honey in their tea.","tags":["tea","preferences"]}""")
            .RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.True(memory.WriteCalled, "WriteEpisodicAsync should be called");
        Assert.True(memory.LastSourceLabel == StoreMemoryTool.SourceLabel,
            "store_memory must use source='model' (dedicated label, distinct from 'self'/'chat')");
        Assert.Equal("model", StoreMemoryTool.SourceLabel);
        Assert.Contains("honey in their tea", memory.LastText);
        Assert.Contains("[tags: tea, preferences]", memory.LastText);
        Assert.True(memory.EmbeddingStored, "Embedding should be stored when embeddings enabled");
        Assert.Equal(memory.LastReturnedId, memory.LastEmbeddingEpisodicId);
        Assert.StartsWith($"stored: id={memory.LastReturnedId}", result.Content);
    }

    [Fact]
    public async Task StoreMemory_NoTags_StoresContentVerbatim()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: false);
        var tool = new StoreMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"content":"A tagless memory."}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Equal("A tagless memory.", memory.LastText);
        Assert.False(memory.EmbeddingStored, "Embedding should not be stored when embeddings disabled");
    }

    [Fact]
    public async Task StoreMemory_EmptyTagsArray_StoresContentVerbatim()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: false);
        var tool = new StoreMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"content":"No tags here.","tags":[]}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Equal("No tags here.", memory.LastText);
    }

    [Fact]
    public async Task StoreMemory_NullTags_StoresContentVerbatim()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: false);
        var tool = new StoreMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"content":"Null tags.","tags":null}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Equal("Null tags.", memory.LastText);
    }

    [Fact]
    public async Task StoreMemory_DoesNotUseSelfSource()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: false);
        var tool = new StoreMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"content":"x"}""").RootElement.Clone();
        await tool.ExecuteAsync(args);

        Assert.NotEqual("self", memory.LastSourceLabel);
    }

    [Fact]
    public async Task StoreMemory_EmbeddingFailure_DoesNotFailStore()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: true, throwOnEmbed: true);
        var tool = new StoreMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"content":"Persisted even if embed fails."}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success, "Episodic row should still be persisted when the embed step fails");
        Assert.True(memory.WriteCalled);
        Assert.False(memory.EmbeddingStored);
        Assert.StartsWith($"stored: id={memory.LastReturnedId}", result.Content);
    }

    [Fact]
    public async Task StoreMemory_StoreEmbeddingFailure_DoesNotFailStore()
    {
        var memory = new FakeMemoryStore(throwOnStoreEmbedding: true);
        var embeddings = new FakeEmbeddingClient(isEnabled: true, vector: new float[] { 0.1f });
        var tool = new StoreMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"content":"Persisted even if vector store throws."}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.True(memory.WriteCalled);
        Assert.False(memory.EmbeddingStored);
    }

    [Fact]
    public async Task StoreMemory_MissingContent_ReturnsFailedResult_DoesNotThrow()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: true);
        var tool = new StoreMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"tags":["x"]}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("error:", result.Content);
        Assert.Contains("content", result.Content);
        Assert.False(memory.WriteCalled, "Must not write when content is missing");
    }

    [Fact]
    public async Task StoreMemory_EmptyContent_ReturnsFailedResult()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: true);
        var tool = new StoreMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"content":"   "}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("non-empty", result.Content);
        Assert.False(memory.WriteCalled);
    }

    [Fact]
    public async Task StoreMemory_NonObjectArgs_ReturnsFailedResult()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: true);
        var tool = new StoreMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("42").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("error:", result.Content);
        Assert.False(memory.WriteCalled);
    }

    [Fact]
    public async Task StoreMemory_WriteThrows_ReturnsFailedResult_DoesNotThrow()
    {
        var memory = new FakeMemoryStore(throwOnWrite: true);
        var embeddings = new FakeEmbeddingClient(isEnabled: false);
        var tool = new StoreMemoryTool(memory, embeddings);

        var args = JsonDocument.Parse("""{"content":"x"}""").RootElement.Clone();
        var result = await tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("error:", result.Content);
        Assert.Contains("write failed", result.Content);
    }

    [Fact]
    public void StoreMemory_Definition_HasCorrectNameDescriptionAndSchema()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: false);
        var tool = new StoreMemoryTool(memory, embeddings);

        var def = tool.Definition;
        Assert.Equal("store_memory", def.Name);
        Assert.Contains("Store a memory", def.Description);

        Assert.Equal(JsonValueKind.Object, def.Parameters.ValueKind);
        Assert.Equal("object", def.Parameters.GetProperty("type").GetString());
        var props = def.Parameters.GetProperty("properties");
        Assert.Equal("string", props.GetProperty("content").GetProperty("type").GetString());
        Assert.Equal("array", props.GetProperty("tags").GetProperty("type").GetString());
        Assert.Equal("string", props.GetProperty("tags").GetProperty("items").GetProperty("type").GetString());
        var required = def.Parameters.GetProperty("required");
        Assert.Equal(1, required.GetArrayLength());
        Assert.Equal("content", required[0].GetString());
    }

    [Fact]
    public async Task StoreMemory_RegistersInToolRegistry_GetDefinitionsIncludesIt()
    {
        var memory = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingClient(isEnabled: false);
        var store = new StoreMemoryTool(memory, embeddings);
        var registry = new ToolRegistry(new ITool[] { store });

        var defs = registry.GetDefinitions();
        Assert.Contains(defs, d => d.Name == "store_memory");

        var args = JsonDocument.Parse("""{"content":"x"}""").RootElement.Clone();
        var result = await registry.ExecuteAsync("store_memory", args);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task BothTools_RegisterTogether_InOneRegistry()
    {
        var memory = new FakeMemoryStore
        {
            SimilarResults = new List<string> { "recalled" }
        };
        var embeddings = new FakeEmbeddingClient(isEnabled: false);
        var recall = new RecallMemoryTool(memory, embeddings);
        var store = new StoreMemoryTool(memory, embeddings);
        var registry = new ToolRegistry(new ITool[] { recall, store });

        var defs = registry.GetDefinitions();
        Assert.Equal(2, defs.Count);
        Assert.Contains(defs, d => d.Name == "recall_memory");
        Assert.Contains(defs, d => d.Name == "store_memory");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Fakes
    // ─────────────────────────────────────────────────────────────────────

    private sealed class FakeMemoryStore : IMemoryStore
    {
        private long _nextId = 1;

        public FakeMemoryStore(bool throwOnWrite = false, bool throwOnStoreEmbedding = false)
        {
            ThrowOnWrite = throwOnWrite;
            ThrowOnStoreEmbedding = throwOnStoreEmbedding;
        }

        public bool ThrowOnWrite { get; }
        public bool ThrowOnStoreEmbedding { get; }

        public bool IsDatabaseOpen => true;
        public string DatabasePath => ":memory:";

        public List<string> SimilarResults { get; set; } = new();
        public List<string> RecentResults { get; set; } = new();

        public bool SimilarCalled { get; private set; }
        public int SimilarLimit { get; private set; }
        public bool RecentCalled { get; private set; }
        public bool WriteCalled { get; private set; }
        public string? LastText { get; private set; }
        public string? LastSourceLabel { get; private set; }
        public long LastReturnedId { get; private set; }
        public bool EmbeddingStored { get; private set; }
        public long LastEmbeddingEpisodicId { get; private set; }
        public float[]? LastEmbeddingVector { get; private set; }
        public string? LastEmbeddingModel { get; private set; }

        public Task<IReadOnlyList<string>> RecallSimilarAsync(
            float[] queryVector, int limit, CancellationToken cancellationToken = default)
        {
            SimilarCalled = true;
            SimilarLimit = limit;
            return Task.FromResult<IReadOnlyList<string>>(SimilarResults);
        }

        public Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default)
        {
            RecentCalled = true;
            return Task.FromResult<IReadOnlyList<string>>(RecentResults);
        }

        public Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default)
        {
            if (ThrowOnWrite)
                throw new InvalidOperationException("WriteEpisodicAsync injected failure");
            WriteCalled = true;
            LastText = text;
            LastSourceLabel = sourceLabel;
            LastReturnedId = _nextId++;
            return Task.FromResult(LastReturnedId);
        }

        public Task StoreEmbeddingAsync(
            long episodicId, float[] vector, string model, CancellationToken cancellationToken = default)
        {
            if (ThrowOnStoreEmbedding)
                throw new InvalidOperationException("StoreEmbeddingAsync injected failure");
            EmbeddingStored = true;
            LastEmbeddingEpisodicId = episodicId;
            LastEmbeddingVector = vector;
            LastEmbeddingModel = model;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(
            int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<(long, string)>>(Array.Empty<(long, string)>());
    }

    private sealed class FakeEmbeddingClient : IEmbeddingClient
    {
        public FakeEmbeddingClient(bool isEnabled, float[]? vector = null, bool throwOnEmbed = false)
        {
            IsEnabled = isEnabled;
            _vector = vector ?? Array.Empty<float>();
            ThrowOnEmbed = throwOnEmbed;
        }

        public bool IsEnabled { get; }
        public string Model => "test-embed-model";
        public bool ThrowOnEmbed { get; }
        public int EmbedCallCount { get; private set; }

        private readonly float[] _vector;

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            EmbedCallCount++;
            if (ThrowOnEmbed)
                throw new InvalidOperationException("EmbedAsync injected failure");
            return Task.FromResult(_vector);
        }
    }
}
