using System.Text.Json;
using SoulCore.Inference;

namespace SoulCore.Protocol.Tests;

public class ToolRegistryTests
{
    [Fact]
    public void EmptyToolList_YieldsEmptyDefinitionsAndBootsClean()
    {
        var registry = new ToolRegistry(Array.Empty<ITool>());

        Assert.Empty(registry.GetDefinitions());
    }

    [Fact]
    public void NullToolList_YieldsEmptyDefinitions()
    {
        var registry = new ToolRegistry(null!);

        Assert.Empty(registry.GetDefinitions());
    }

    [Fact]
    public async Task DispatchByName_RoutesToRegisteredTool()
    {
        var echo = new FakeEchoTool();
        var registry = new ToolRegistry(new ITool[] { echo });

        var defs = registry.GetDefinitions();
        Assert.Single(defs);
        Assert.Equal("echo", defs[0].Name);

        var args = JsonDocument.Parse("""{"text":"hello"}""").RootElement.Clone();
        var result = await registry.ExecuteAsync("echo", args);

        Assert.True(result.Success);
        Assert.Equal("echo: hello", result.Content);
        Assert.True(echo.WasCalled);
    }

    [Fact]
    public async Task UnknownTool_ReturnsFailedResult_DoesNotThrow()
    {
        var registry = new ToolRegistry(new ITool[] { new FakeEchoTool() });

        var result = await registry.ExecuteAsync("does_not_exist", default);

        Assert.False(result.Success);
        Assert.Contains("Unknown tool", result.Content);
        Assert.Contains("echo", result.Content);
    }

    [Fact]
    public async Task ToolThrows_IsWrappedIntoFailedResult_DoesNotThrow()
    {
        var boom = new FakeThrowingTool();
        var registry = new ToolRegistry(new ITool[] { boom });

        var result = await registry.ExecuteAsync("boom", default);

        Assert.False(result.Success);
        Assert.Contains("'boom' threw", result.Content);
        Assert.Contains(nameof(InvalidOperationException), result.Content);
    }

    [Fact]
    public async Task Dispatch_PassesThroughCancellationToken()
    {
        var slow = new FakeCancellableTool();
        var registry = new ToolRegistry(new ITool[] { slow });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => registry.ExecuteAsync("slow", default, cts.Token));
    }

    [Fact]
    public void DuplicateToolNames_ThrowsAtConstruction()
    {
        var first = new FakeEchoTool();
        var second = new FakeEchoTool();

        var ex = Assert.Throws<InvalidOperationException>(() => new ToolRegistry(new ITool[] { first, second }));
        Assert.Contains("Duplicate tool name 'echo'", ex.Message);
    }

    [Fact]
    public void EmptyToolName_ThrowsAtConstruction()
    {
        var bad = new FakeNoNameTool();
        var ex = Assert.Throws<InvalidOperationException>(() => new ToolRegistry(new ITool[] { bad }));
        Assert.Contains("empty Definition.Name", ex.Message);
    }

    private sealed class FakeEchoTool : ITool
    {
        public bool WasCalled;
        public ToolDefinition Definition { get; } = new(
            Name: "echo",
            Description: "Echoes back text.",
            Parameters: JsonDocument.Parse("""{"type":"object","properties":{"text":{"type":"string"}}}""").RootElement.Clone());

        public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
        {
            WasCalled = true;
            var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            return Task.FromResult(new ToolResult(true, $"echo: {text}", null));
        }
    }

    private sealed class FakeThrowingTool : ITool
    {
        public ToolDefinition Definition { get; } = new(
            Name: "boom",
            Description: "Always throws.",
            Parameters: JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone());

        public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
            => throw new InvalidOperationException("intentional");
    }

    private sealed class FakeNoNameTool : ITool
    {
        public ToolDefinition Definition { get; } = new(
            Name: "  ",
            Description: "Bad name.",
            Parameters: JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone());

        public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
            => Task.FromResult(new ToolResult(true, "never", null));
    }

    private sealed class FakeCancellableTool : ITool
    {
        public ToolDefinition Definition { get; } = new(
            Name: "slow",
            Description: "Honors cancellation.",
            Parameters: JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone());

        public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new ToolResult(true, "done", null));
        }
    }
}
