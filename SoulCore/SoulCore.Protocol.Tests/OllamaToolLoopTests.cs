using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// Unit tests for the Ollama agent loop (TASK-126). The HTTP layer is faked
/// with an <see cref="HttpMessageHandler"/> subclass so no network is hit;
/// <see cref="IToolRegistry"/> is faked with a scripted stub. These tests
/// cover the loop contract: tool_call parse → registry dispatch →
/// role:"tool" feedback → re-prompt → final text; iteration cap; defensive
/// <c>arguments</c> parse (object and string forms); no-tool-call fast path.
/// </summary>
public class OllamaToolLoopTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static InferenceOptions MakeOptions(int maxToolIterations = 8) => new()
    {
        Enabled = true,
        BaseUrl = "http://127.0.0.1:11434",
        Model = "test-model",
        MaxToolIterations = maxToolIterations,
        MaxTokens = 128,
        NumCtx = 0,
        ThinkEnabled = false
    };

    private static OllamaInferenceClient MakeClient(
        HttpMessageHandler handler,
        InferenceOptions? options = null,
        IToolRegistry? registry = null)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:11434/")
        };
        var opts = Options.Create(options ?? MakeOptions());
        var logger = new LoggerFactory().CreateLogger<OllamaInferenceClient>();
        return new OllamaInferenceClient(http, opts, logger, registry);
    }

    private static ToolDefinition EchoToolDef() => new(
        Name: "echo",
        Description: "Echoes text.",
        Parameters: JsonDocument.Parse("""{"type":"object","properties":{"text":{"type":"string"}}}""").RootElement.Clone());

    [Fact]
    public async Task NoToolCall_ReturnsTextImmediately_OneRoundTrip()
    {
        // Model returns plain text on the first call → loop ends in 1 round-trip.
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(content: "Hello, world!", toolCalls: null)
 // iteration 0
            });
        var client = MakeClient(handler, options: MakeOptions(maxToolIterations: 8));
        var registry = new ScriptedRegistry();

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "hi" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("Hello, world!", result);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(registry.Calls);
    }

    [Fact]
    public async Task OneToolCall_RoundTrips_DispatchesTool_FeedsResultBack_ReturnsFinalText()
    {
        // iter 0: model emits a tool_call for "echo"
        // iter 1: model sees the tool result and returns final text
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(content: "", toolCalls: new[]
                {
                    new { function = new { name = "echo", arguments = new { text = "hello" } } }
                }),
                ChatResponseJson(content: "echoed: hello", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                return new ToolResult(true, $"echo: {text}", null);
            }));
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "say hello via echo" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("echoed: hello", result);
        Assert.Equal(2, handler.CallCount);
        Assert.Single(registry.Calls);
        Assert.Equal("echo", registry.Calls[0].Name);

        // The second request must include the role:"tool" message with the
        // tool result Content (BED-125 contract: only Content, not Data).
        var secondReq = handler.CapturedRequests[1];
        Assert.Contains(secondReq.Messages, m =>
            m.Role == "tool" && m.Name == "echo" && m.Content == "echo: hello");
    }

    [Fact]
    public async Task IterationCap_StopsAfterN_WhenModelKeepsCallingTools()
    {
        // Model always emits a tool_call → cap kicks in at N=3.
        // After N iterations the loop returns the cap marker (no final text
        // was ever produced because every turn was a tool_call with empty content).
        const int cap = 3;
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(content: "", toolCalls: new[]
                {
                    new { function = new { name = "echo", arguments = new { text = "a" } } }
                }),
                ChatResponseJson(content: "", toolCalls: new[]
                {
                    new { function = new { name = "echo", arguments = new { text = "b" } } }
                }),
                ChatResponseJson(content: "", toolCalls: new[]
                {
                    new { function = new { name = "echo", arguments = new { text = "c" } } }
                }),
                ChatResponseJson(content: "", toolCalls: new[]
                {
                    new { function = new { name = "echo", arguments = new { text = "d" } } }
                })
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                return new ToolResult(true, $"echo: {text}", null);
            }));
        var client = MakeClient(handler, options: MakeOptions(maxToolIterations: cap), registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "keep calling echo" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        // After cap iterations, no assistant text was produced → marker.
        Assert.Equal(OllamaInferenceClient.IterationCapMarker, result);
        // Exactly cap round-trips, not more.
        Assert.Equal(cap, handler.CallCount);
        // The registry was invoked once per tool_call per iteration → cap times.
        Assert.Equal(cap, registry.Calls.Count);
    }

    [Fact]
    public async Task IterationCap_ReturnsLastAssistantText_WhenModelEmittedSomeTextBeforeCap()
    {
        // Model emits text + a tool_call every turn. Cap=3. After 3 iterations
        // the loop returns the last non-empty assistant text ("partial 2").
        const int cap = 3;
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(content: "partial 1", toolCalls: new[]
                {
                    new { function = new { name = "echo", arguments = new { text = "a" } } }
                }),
                ChatResponseJson(content: "partial 2", toolCalls: new[]
                {
                    new { function = new { name = "echo", arguments = new { text = "b" } } }
                }),
                ChatResponseJson(content: "partial 3", toolCalls: new[]
                {
                    new { function = new { name = "echo", arguments = new { text = "c" } } }
                })
            });
        var registry = new ScriptedRegistry(
            ("echo", _ => new ToolResult(true, "echoed", null)));
        var client = MakeClient(handler, options: MakeOptions(maxToolIterations: cap), registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "say something then call a tool" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("partial 3", result);
        Assert.Equal(cap, handler.CallCount);
    }

    [Fact]
    public async Task DefensiveArguments_StringForm_IsParsedToObject()
    {
        // Ollama has shipped `arguments` as a JSON string in some versions.
        // The loop must parse it back to an object before dispatching.
        string? capturedArg = null;
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(content: "", toolCalls: new[]
                {
                    // arguments as a JSON *string* — note the quoted body.
                    new { function = new { name = "echo", arguments = "{\"text\":\"from-string\"}" } }
                }),
                ChatResponseJson(content: "done", toolCalls: (object[]?)null)
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                // Capture the raw arg element to inspect what the loop passed.
                capturedArg = args.GetRawText();
                var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                return new ToolResult(true, $"echo: {text}", null);
            }));
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "call echo with string args" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("done", result);
        Assert.NotNull(capturedArg);
        // The string form was parsed back to an object — the registry sees
        // the object shape, not the raw string.
        Assert.Contains("\"text\":\"from-string\"", capturedArg);
    }

    [Fact]
    public async Task DefensiveArguments_ObjectForm_PassesThrough()
    {
        // Ollama's current format: arguments as a JSON object. Pass-through.
        string? capturedArg = null;
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(content: "", toolCalls: new[]
                {
                    new { function = new { name = "echo", arguments = new { text = "from-object" } } }
                }),
                ChatResponseJson(content: "ok", toolCalls: (object[]?)null)
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                capturedArg = args.GetRawText();
                var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                return new ToolResult(true, $"echo: {text}", null);
            }));
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "call echo with object args" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("ok", result);
        Assert.NotNull(capturedArg);
        Assert.Contains("\"text\":\"from-object\"", capturedArg);
    }

    [Fact]
    public async Task EmptyToolList_LoopStillRuns_ModelReturnsTextInOneRoundTrip()
    {
        // No tools advertised → model cannot call tools → 1 round-trip, text back.
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(content: "no tools available, here's text", toolCalls: null)
            });
        var registry = new ScriptedRegistry();
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "hi" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, Array.Empty<ToolDefinition>(), registry);

        Assert.Equal("no tools available, here's text", result);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(registry.Calls);
        // Request should omit tools when the list is empty.
        Assert.Null(handler.CapturedRequests[0].Tools);
    }

    [Fact]
    public async Task FailedToolResult_ContentFedBackToModel_LoopContinues()
    {
        // Tool returns Success=false (e.g. unknown tool, bad args). The loop
        // must feed result.Content back as the role:"tool" message and let
        // the model react, not crash.
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(content: "", toolCalls: new[]
                {
                    new { function = new { name = "missing", arguments = new { } } }
                }),
                ChatResponseJson(content: "tool failed, but I'll reply", toolCalls: null)
            });
        var registry = new ScriptedRegistry(); // no tools → ExecuteAsync returns failed
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "call a missing tool" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("tool failed, but I'll reply", result);
        Assert.Equal(2, handler.CallCount);
        Assert.Single(registry.Calls);
        // The failed result Content was forwarded to the model.
        Assert.Contains(handler.CapturedRequests[1].Messages, m =>
            m.Role == "tool" && (m.Content ?? string.Empty).Contains("Unknown tool"));
    }

    [Fact]
    public async Task Ctor_NullHttp_Throws()
    {
        var opts = Options.Create(MakeOptions());
        var logger = new LoggerFactory().CreateLogger<OllamaInferenceClient>();
        Assert.Throws<ArgumentNullException>(() =>
            new OllamaInferenceClient(null!, opts, logger));
    }

    [Fact]
    public async Task Ctor_NullOptions_Throws()
    {
        var http = new HttpClient(new ScriptedHandler(Array.Empty<string>()));
        var logger = new LoggerFactory().CreateLogger<OllamaInferenceClient>();
        Assert.Throws<ArgumentNullException>(() =>
            new OllamaInferenceClient(http, null!, logger));
    }

    [Fact]
    public async Task CompleteWithTools_NullMessages_Throws()
    {
        var client = MakeClient(new ScriptedHandler(Array.Empty<string>()), registry: new ScriptedRegistry());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.CompleteWithToolsAsync(null!, Array.Empty<ToolDefinition>(), new ScriptedRegistry()));
    }

    [Fact]
    public async Task CompleteWithTools_EmptyMessages_Throws()
    {
        var client = MakeClient(new ScriptedHandler(Array.Empty<string>()), registry: new ScriptedRegistry());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.CompleteWithToolsAsync(new List<ChatMessage>(), Array.Empty<ToolDefinition>(), new ScriptedRegistry()));
    }

    [Fact]
    public async Task CompleteWithTools_NullRegistry_Throws_WhenNoCtorRegistry()
    {
        // No ctor-injected registry and no call-arg registry → throws.
        var client = MakeClient(new ScriptedHandler(Array.Empty<string>()), registry: null);
        var messages = new List<ChatMessage> { new() { Role = "user", Content = "hi" } };

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.CompleteWithToolsAsync(messages, Array.Empty<ToolDefinition>(), null!));
    }

    [Fact]
    public async Task NullInferenceClient_ToolLoopStubReply_IsDeterministic()
    {
        // The appsettings Enabled=false path returns a deterministic stub,
        // no network.
        var stub = new NullInferenceClient();
        var reply = await stub.CompleteWithToolsAsync(
            new List<ChatMessage> { new() { Role = "user", Content = "hi" } },
            Array.Empty<ToolDefinition>(),
            new ScriptedRegistry());

        Assert.Equal(NullInferenceClient.ToolLoopStubReply, reply);
    }

    // ---------------------------------------------------------------------
    // ISSUE-20260726-001: content-embedded-JSON fallback (qwen2.5 leak).
    // Mirrors the 4 Hermes fallback tests in HermesToolLoopTests.cs (BED-127).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task FallbackParser_ContentEmbeddedJson_DispatchesTool_WhenToolCallsNull()
    {
        // qwen2.5 leak: tool call leaked as bare JSON in message.content with
        // tool_calls: null. The fallback must recover it and dispatch.
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(
                    content: """{"name":"echo","arguments":{"text":"hello"}}""",
                    toolCalls: null),
                ChatResponseJson(content: "echoed: hello", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                return new ToolResult(true, $"echo: {text}", null);
            }));
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "say hello via echo" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("echoed: hello", result);
        Assert.Equal(2, handler.CallCount);
        Assert.Single(registry.Calls);
        Assert.Equal("echo", registry.Calls[0].Name);
        // The dispatch happened with the recovered arguments.
        Assert.Contains("\"text\":\"hello\"", registry.Calls[0].Args.GetRawText());
    }

    [Fact]
    public async Task FallbackParser_JsonEmbeddedInText_DispatchesTool()
    {
        // Variant: the JSON object is embedded in surrounding prose text.
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(
                    content: """I'll call the tool: {"name":"echo","arguments":{"text":"hi"}} and reply.""",
                    toolCalls: null),
                ChatResponseJson(content: "echoed: hi", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                return new ToolResult(true, $"echo: {text}", null);
            }));
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "say hi via echo" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("echoed: hi", result);
        Assert.Equal(2, handler.CallCount);
        Assert.Single(registry.Calls);
        Assert.Equal("echo", registry.Calls[0].Name);
    }

    [Fact]
    public async Task FallbackParser_NameNotRegistered_TreatedAsTextReply()
    {
        // The leaked JSON has a name that is NOT a registered tool → treat as
        // a normal text reply (no dispatch, no crash).
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(
                    content: """{"name":"not_a_tool","arguments":{}}""",
                    toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("echo", _ => new ToolResult(true, "echo", null)));
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "call a missing tool" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        // Treated as a text reply — the leaked JSON is returned verbatim, no dispatch.
        Assert.Contains("not_a_tool", result);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(registry.Calls);
    }

    [Fact]
    public async Task FallbackParser_NoToolsAdvertised_DoesNotAttemptRecovery()
    {
        // When no tools are advertised, content-embedded JSON is never a tool
        // call — the loop must return the text in 1 round-trip.
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(
                    content: """{"name":"echo","arguments":{"text":"hi"}}""",
                    toolCalls: null)
            });
        var registry = new ScriptedRegistry();
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "hi" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, Array.Empty<ToolDefinition>(), registry);

        Assert.Contains("echo", result);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(registry.Calls);
    }

    [Fact]
    public async Task FallbackParser_StringFormArguments_IsParsedToObject()
    {
        // qwen2.5 sometimes leaks arguments as a JSON *string* inside the
        // content-embedded JSON: {"name":"echo","arguments":"{\"text\":\"s\"}"}.
        // The fallback must parse it back to an object before dispatch.
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(
                    content: """{"name":"echo","arguments":"{\"text\":\"from-string\"}"}""",
                    toolCalls: null),
                ChatResponseJson(content: "done", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                return new ToolResult(true, $"echo: {text}", null);
            }));
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "call echo with string args in content" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("done", result);
        Assert.Single(registry.Calls);
        Assert.Contains("\"text\":\"from-string\"", registry.Calls[0].Args.GetRawText());
    }

    // ---------- helpers ----------

    private static string ChatResponseJson(string content, object[]? toolCalls)
    {
        var msg = new
        {
            message = new
            {
                role = "assistant",
                content,
                tool_calls = toolCalls
            }
        };
        return JsonSerializer.Serialize(msg, JsonOptions);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public int CallCount { get; private set; }
        public List<CapturedRequest> CapturedRequests { get; } = new();

        public ScriptedHandler(IEnumerable<string> responses)
        {
            _responses = new Queue<string>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

            // Capture the request body for assertions.
            var body = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            var messages = new List<CapturedMessage>();
            var tools = (List<object>?)null;
            if (body is not null)
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("messages", out var msgs))
                {
                    foreach (var m in msgs.EnumerateArray())
                    {
                        messages.Add(new CapturedMessage(
                            Role: m.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "",
                            Content: m.TryGetProperty("content", out var c) ? c.GetString() : null,
                            Name: m.TryGetProperty("name", out var n) ? n.GetString() : null));
                    }
                }
                if (doc.RootElement.TryGetProperty("tools", out var t) && t.ValueKind == JsonValueKind.Array)
                {
                    tools = new List<object>();
                    foreach (var tool in t.EnumerateArray())
                    {
                        tools.Add(tool.GetRawText());
                    }
                }
            }
            CapturedRequests.Add(new CapturedRequest(messages, tools));

            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"message":{"role":"assistant","content":"no more scripts","tool_calls":null}}""")
                });
            }
            var json = _responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }

    private sealed record CapturedRequest(IReadOnlyList<CapturedMessage> Messages, List<object>? Tools);
    private sealed record CapturedMessage(string Role, string? Content, string? Name);

    private sealed class ScriptedRegistry : IToolRegistry
    {
        private readonly Dictionary<string, Func<JsonElement, ToolResult>> _handlers;
        public List<(string Name, JsonElement Args)> Calls { get; } = new();

        public ScriptedRegistry(params (string Name, Func<JsonElement, ToolResult>)[] handlers)
        {
            _handlers = new Dictionary<string, Func<JsonElement, ToolResult>>(StringComparer.Ordinal);
            foreach (var (name, fn) in handlers)
            {
                _handlers[name] = fn;
            }
        }

        public IReadOnlyList<ToolDefinition> GetDefinitions() => Array.Empty<ToolDefinition>();

        public Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default)
        {
            Calls.Add((name, args));
            if (_handlers.TryGetValue(name, out var fn))
            {
                return Task.FromResult(fn(args));
            }
            return Task.FromResult(new ToolResult(
                Success: false,
                Content: $"Unknown tool '{name}'. Available: {string.Join(", ", _handlers.Keys)}.",
                Data: null));
        }
    }
}
