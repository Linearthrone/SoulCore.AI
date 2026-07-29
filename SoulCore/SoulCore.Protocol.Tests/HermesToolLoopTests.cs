using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Hermes;
using SoulCore.Inference;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// Unit tests for the Hermes agent loop (TASK-127). The HTTP layer is faked
/// with an <see cref="HttpMessageHandler"/> subclass so no network is hit;
/// <see cref="IToolRegistry"/> is faked with a scripted stub. These tests
/// cover the OpenAI-compatible loop contract: <c>tools[]</c> + <c>tool_choice</c>
/// sent, <c>choices[0].message.tool_calls</c> parse (with OpenAI string-form
/// <c>arguments</c>) → registry dispatch → <c>{role:"tool", tool_call_id, name,
/// content}</c> feedback → re-prompt → final text; iteration cap; defensive
/// <c>arguments</c> parse; <see cref="NullHermesClient"/> stub; and the
/// ISSUE-20260726-001 content-embedded-JSON fallback parser.
/// </summary>
public class HermesToolLoopTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static HermesOptions MakeHermesOptions(int maxTokens = 256, string? toolChoice = null) => new()
    {
        Enabled = true,
        BaseUrl = "http://127.0.0.1:8642",
        Model = "local",
        MaxTokens = maxTokens,
        ApiKey = "test-key",
        ToolChoice = toolChoice ?? "auto"
    };

    private static InferenceOptions MakeInferenceOptions(int maxToolIterations = 8) => new()
    {
        Enabled = true,
        BaseUrl = "http://127.0.0.1:11434",
        Model = "test-model",
        MaxToolIterations = maxToolIterations,
        MaxTokens = 128,
        NumCtx = 0,
        ThinkEnabled = false
    };

    private static HermesHttpClient MakeClient(
        HttpMessageHandler handler,
        HermesOptions? hermesOptions = null,
        InferenceOptions? inferenceOptions = null)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8642/")
        };
        var hOpts = Options.Create(hermesOptions ?? MakeHermesOptions());
        var iOpts = Options.Create(inferenceOptions ?? MakeInferenceOptions());
        var logger = new LoggerFactory().CreateLogger<HermesHttpClient>();
        return new HermesHttpClient(http, hOpts, iOpts, logger);
    }

    private static ToolDefinition EchoToolDef() => new(
        Name: "echo",
        Description: "Echoes text.",
        Parameters: JsonDocument.Parse("""{"type":"object","properties":{"text":{"type":"string"}}}""").RootElement.Clone());

    // ---------------------------------------------------------------------
    // Acceptance criterion 1 + 8: tool round-trip against mock HTTP.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task NoToolCall_ReturnsTextImmediately_OneRoundTrip()
    {
        // Model returns plain text on the first call → loop ends in 1 round-trip.
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson(content: "Hello, world!", toolCalls: null)
            });
        var client = MakeClient(handler);
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
        // iter 0: model emits a tool_call for "echo" (OpenAI string-form arguments)
        // iter 1: model sees the tool result and returns final text
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson(
                    content: null,
                    toolCalls: new[]
                    {
                        new
                        {
                            id = "call_abc",
                            type = "function",
                            function = new { name = "echo", arguments = "{\"text\":\"hello\"}" }
                        }
                    }),
                OpenAIResponseJson(content: "echoed: hello", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                return new ToolResult(true, $"echo: {text}", null);
            }));
        var client = MakeClient(handler);

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
        // tool result Content (BED-125 contract) AND the tool_call_id (OpenAI
        // requires the id so the model can correlate the result with the call).
        var secondReq = handler.CapturedRequests[1];
        Assert.Contains(secondReq.Messages, m =>
            m.Role == "tool" && m.Name == "echo" && m.Content == "echo: hello" && m.ToolCallId == "call_abc");
        // The first request must advertise the tools + tool_choice.
        var firstReq = handler.CapturedRequests[0];
        Assert.NotNull(firstReq.Tools);
        Assert.Equal("auto", firstReq.ToolChoice);
    }

    // ---------------------------------------------------------------------
    // Acceptance criterion 2: iteration cap shared with BED-126.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task IterationCap_StopsAfterN_WhenModelKeepsCallingTools()
    {
        // Model always emits a tool_call → cap kicks in at N=3.
        const int cap = 3;
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson(null, new[]
                {
                    new { id = "c0", type = "function", function = new { name = "echo", arguments = "{\"text\":\"a\"}" } }
                }),
                OpenAIResponseJson(null, new[]
                {
                    new { id = "c1", type = "function", function = new { name = "echo", arguments = "{\"text\":\"b\"}" } }
                }),
                OpenAIResponseJson(null, new[]
                {
                    new { id = "c2", type = "function", function = new { name = "echo", arguments = "{\"text\":\"c\"}" } }
                }),
                OpenAIResponseJson(null, new[]
                {
                    new { id = "c3", type = "function", function = new { name = "echo", arguments = "{\"text\":\"d\"}" } }
                })
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                return new ToolResult(true, $"echo: {text}", null);
            }));
        var client = MakeClient(handler, inferenceOptions: MakeInferenceOptions(maxToolIterations: cap));

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "keep calling echo" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        // After cap iterations, no assistant text was produced → marker.
        Assert.Equal(HermesHttpClient.IterationCapMarker, result);
        Assert.Equal(cap, handler.CallCount);
        Assert.Equal(cap, registry.Calls.Count);
    }

    [Fact]
    public async Task IterationCap_ReturnsLastAssistantText_WhenModelEmittedSomeTextBeforeCap()
    {
        const int cap = 3;
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson("partial 1", new[]
                {
                    new { id = "c0", type = "function", function = new { name = "echo", arguments = "{\"text\":\"a\"}" } }
                }),
                OpenAIResponseJson("partial 2", new[]
                {
                    new { id = "c1", type = "function", function = new { name = "echo", arguments = "{\"text\":\"b\"}" } }
                }),
                OpenAIResponseJson("partial 3", new[]
                {
                    new { id = "c2", type = "function", function = new { name = "echo", arguments = "{\"text\":\"c\"}" } }
                })
            });
        var registry = new ScriptedRegistry(
            ("echo", _ => new ToolResult(true, "echoed", null)));
        var client = MakeClient(handler, inferenceOptions: MakeInferenceOptions(maxToolIterations: cap));

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "say something then call a tool" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("partial 3", result);
        Assert.Equal(cap, handler.CallCount);
    }

    // ---------------------------------------------------------------------
    // Acceptance criterion 3: defensive arguments parse (OpenAI string form).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DefensiveArguments_StringForm_IsParsedToObject()
    {
        // OpenAI ships `arguments` as a JSON *string* — the loop must parse it
        // back to an object before dispatching.
        string? capturedArg = null;
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson(null, new[]
                {
                    new { id = "c0", type = "function", function = new { name = "echo", arguments = "{\"text\":\"from-string\"}" } }
                }),
                OpenAIResponseJson("done", null)
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                capturedArg = args.GetRawText();
                var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                return new ToolResult(true, $"echo: {text}", null);
            }));
        var client = MakeClient(handler);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "call echo with string args" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("done", result);
        Assert.NotNull(capturedArg);
        Assert.Contains("\"text\":\"from-string\"", capturedArg);
    }

    [Fact]
    public async Task DefensiveArguments_MalformedString_FallsBackToRawString()
    {
        // arguments is a string but not valid JSON — the loop surfaces it as a
        // string-valued element so the tool sees the raw value rather than losing it.
        string? capturedArg = null;
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson(null, new[]
                {
                    new { id = "c0", type = "function", function = new { name = "echo", arguments = "not-json" } }
                }),
                OpenAIResponseJson("done", null)
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                capturedArg = args.GetRawText();
                return new ToolResult(true, "ok", null);
            }));
        var client = MakeClient(handler);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "call echo with bad args" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("done", result);
        Assert.NotNull(capturedArg);
        // The raw string was surfaced (serialized as a JSON string element).
        Assert.Contains("not-json", capturedArg);
    }

    // ---------------------------------------------------------------------
    // Acceptance criterion 5: tool_choice configurable.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ToolChoice_None_IsSentOnTheWire()
    {
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson("no tool call", null)
            });
        var client = MakeClient(handler, hermesOptions: MakeHermesOptions(toolChoice: "none"));
        var registry = new ScriptedRegistry();

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "hi" }
        };

        await client.CompleteWithToolsAsync(messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("none", handler.CapturedRequests[0].ToolChoice);
    }

    [Fact]
    public async Task ToolChoice_SpecificToolObject_IsSentVerbatim()
    {
        // Forcing a specific tool: tool_choice is a JSON object string.
        const string forceEcho = """{"type":"function","function":{"name":"echo"}}""";
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson(null, new[]
                {
                    new { id = "c0", type = "function", function = new { name = "echo", arguments = "{\"text\":\"x\"}" } }
                }),
                OpenAIResponseJson("ok", null)
            });
        var client = MakeClient(handler, hermesOptions: MakeHermesOptions(toolChoice: forceEcho));
        var registry = new ScriptedRegistry(
            ("echo", _ => new ToolResult(true, "echo: x", null)));

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "force echo" }
        };

        await client.CompleteWithToolsAsync(messages, new[] { EchoToolDef() }, registry);

        Assert.Equal(forceEcho, handler.CapturedRequests[0].ToolChoice);
    }

    [Fact]
    public async Task EmptyToolList_OmitsToolsAndToolChoice_ModelReturnsTextInOneRoundTrip()
    {
        // No tools advertised → OpenAI rejects tool_choice without tools, so the
        // client must omit both. Model returns text in 1 round-trip.
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson("no tools available, here's text", null)
            });
        var registry = new ScriptedRegistry();
        var client = MakeClient(handler);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "hi" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, Array.Empty<ToolDefinition>(), registry);

        Assert.Equal("no tools available, here's text", result);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(registry.Calls);
        Assert.Null(handler.CapturedRequests[0].Tools);
        Assert.Null(handler.CapturedRequests[0].ToolChoice);
    }

    // ---------------------------------------------------------------------
    // Acceptance criterion 9 (ISSUE-001): content-embedded-JSON fallback.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task FallbackParser_ContentEmbeddedJson_DispatchesTool_WhenToolCallsNull()
    {
        // qwen2.5 leak: tool call leaked as bare JSON in message.content with
        // tool_calls: null. The fallback must recover it and dispatch.
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson(
                    content: """{"name":"echo","arguments":{"text":"hello"}}""",
                    toolCalls: null),
                OpenAIResponseJson("echoed: hello", null)
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                return new ToolResult(true, $"echo: {text}", null);
            }));
        var client = MakeClient(handler);

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
                OpenAIResponseJson(
                    content: """I'll call the tool: {"name":"echo","arguments":{"text":"hi"}} and reply.""",
                    toolCalls: null),
                OpenAIResponseJson("echoed: hi", null)
            });
        var registry = new ScriptedRegistry(
            ("echo", args =>
            {
                var text = args.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                return new ToolResult(true, $"echo: {text}", null);
            }));
        var client = MakeClient(handler);

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
                OpenAIResponseJson(
                    content: """{"name":"not_a_tool","arguments":{}}""",
                    toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("echo", _ => new ToolResult(true, "echo", null)));
        var client = MakeClient(handler);

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
                OpenAIResponseJson(
                    content: """{"name":"echo","arguments":{"text":"hi"}}""",
                    toolCalls: null)
            });
        var registry = new ScriptedRegistry();
        var client = MakeClient(handler);

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

    // ---------------------------------------------------------------------
    // Acceptance criterion 4: NullHermesClient stub is deterministic.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task NullHermesClient_ToolLoopStubReply_IsDeterministic()
    {
        // The appsettings Enabled=false path returns a deterministic stub,
        // no network.
        var stub = new NullHermesClient();
        var reply = await stub.CompleteWithToolsAsync(
            new List<ChatMessage> { new() { Role = "user", Content = "hi" } },
            Array.Empty<ToolDefinition>(),
            new ScriptedRegistry());

        Assert.Equal(NullHermesClient.ToolLoopStubReply, reply);
    }

    // ---------------------------------------------------------------------
    // Failed tool result feeds back to model (robustness).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task FailedToolResult_ContentFedBackToModel_LoopContinues()
    {
        // Tool returns Success=false (e.g. unknown tool, bad args). The loop
        // must feed result.Content back as the role:"tool" message and let
        // the model react, not crash.
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson(null, new[]
                {
                    new { id = "c0", type = "function", function = new { name = "missing", arguments = "{}" } }
                }),
                OpenAIResponseJson("tool failed, but I'll reply", null)
            });
        var registry = new ScriptedRegistry(); // no tools → ExecuteAsync returns failed
        var client = MakeClient(handler);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "call a missing tool" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { EchoToolDef() }, registry);

        Assert.Equal("tool failed, but I'll reply", result);
        Assert.Equal(2, handler.CallCount);
        Assert.Single(registry.Calls);
        Assert.Contains(handler.CapturedRequests[1].Messages, m =>
            m.Role == "tool" && (m.Content ?? string.Empty).Contains("Unknown tool"));
    }

    // ---------------------------------------------------------------------
    // BED-161: PreferHermes Host ITool dispatch + gateway fail-fast.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CompleteWithTools_GatewayDown_ThrowsUnavailable_FailFast()
    {
        var handler = new ScriptedHandler(
            Array.Empty<string>(),
            healthOk: false);
        var client = MakeClient(handler);
        var registry = new ScriptedRegistry();
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "hi" }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CompleteWithToolsAsync(messages, new[] { EchoToolDef() }, registry));

        Assert.Equal(IHermesMcpInvoker.UnavailableMessage, ex.Message);
        Assert.Equal(0, handler.CallCount); // health probe does not count as chat
        Assert.Empty(registry.Calls);
    }

    // BED-164 Avenue B: PreferHermes MCP preflight without tools[] chat.
    [Fact]
    public async Task EnsureMcpReady_GatewayDown_ThrowsUnavailable_FailFast()
    {
        var handler = new ScriptedHandler(
            Array.Empty<string>(),
            healthOk: false);
        var client = MakeClient(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.EnsureMcpReadyAsync());

        Assert.Equal(IHermesMcpInvoker.UnavailableMessage, ex.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task EnsureMcpReady_Healthy_Completes()
    {
        var handler = new ScriptedHandler(
            Array.Empty<string>(),
            healthOk: true);
        var client = MakeClient(handler);

        await client.EnsureMcpReadyAsync();

        Assert.Equal(0, handler.CallCount); // health probe only
    }

    [Fact]
    public async Task ComputerUse_Alias_DispatchesSoulCoreDesktopScreenshot()
    {
        // PreferHermes must map Hermes MCP computer_use → desktop_screenshot ITool
        // (which then CallMcpToolAsync), not execute Hermes server tools on Host.
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson(
                    content: null,
                    toolCalls: new[]
                    {
                        new
                        {
                            id = "call_cu",
                            type = "function",
                            function = new
                            {
                                name = "computer_use",
                                arguments = "{\"action\":\"screenshot\",\"monitor\":0}"
                            }
                        }
                    }),
                OpenAIResponseJson(content: "screenshot done via SoulCore ITool", toolCalls: null)
            });

        var desktopDef = new ToolDefinition(
            Name: "desktop_screenshot",
            Description: "Capture desktop.",
            Parameters: JsonDocument.Parse(
                """{"type":"object","properties":{"monitor":{"type":"integer"}}}""").RootElement.Clone());

        var registry = new ScriptedRegistry(
            ("desktop_screenshot", args =>
            {
                Assert.True(args.ValueKind == JsonValueKind.Object || args.ValueKind == JsonValueKind.Undefined
                    || args.TryGetProperty("action", out _) || args.TryGetProperty("monitor", out _));
                return new ToolResult(true, "shot=/tmp/x.png", null);
            }));

        var client = MakeClient(handler);
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "take a screenshot" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages, new[] { desktopDef }, registry);

        Assert.Equal("screenshot done via SoulCore ITool", result);
        Assert.Single(registry.Calls);
        Assert.Equal("desktop_screenshot", registry.Calls[0].Name);
        Assert.Equal(2, handler.CallCount);
    }

    // ---------------------------------------------------------------------
    // Argument validation / ctor guards.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Ctor_NullHttp_Throws()
    {
        var hOpts = Options.Create(MakeHermesOptions());
        var iOpts = Options.Create(MakeInferenceOptions());
        var logger = new LoggerFactory().CreateLogger<HermesHttpClient>();
        Assert.Throws<ArgumentNullException>(() =>
            new HermesHttpClient(null!, hOpts, iOpts, logger));
    }

    [Fact]
    public async Task Ctor_NullHermesOptions_Throws()
    {
        var http = new HttpClient(new ScriptedHandler(Array.Empty<string>()));
        var iOpts = Options.Create(MakeInferenceOptions());
        var logger = new LoggerFactory().CreateLogger<HermesHttpClient>();
        Assert.Throws<ArgumentNullException>(() =>
            new HermesHttpClient(http, null!, iOpts, logger));
    }

    [Fact]
    public async Task Ctor_NullInferenceOptions_Throws()
    {
        var http = new HttpClient(new ScriptedHandler(Array.Empty<string>()));
        var hOpts = Options.Create(MakeHermesOptions());
        var logger = new LoggerFactory().CreateLogger<HermesHttpClient>();
        Assert.Throws<ArgumentNullException>(() =>
            new HermesHttpClient(http, hOpts, null!, logger));
    }

    [Fact]
    public async Task CompleteWithTools_NullMessages_Throws()
    {
        var client = MakeClient(new ScriptedHandler(Array.Empty<string>()));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.CompleteWithToolsAsync(null!, Array.Empty<ToolDefinition>(), new ScriptedRegistry()));
    }

    [Fact]
    public async Task CompleteWithTools_EmptyMessages_Throws()
    {
        var client = MakeClient(new ScriptedHandler(Array.Empty<string>()));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.CompleteWithToolsAsync(new List<ChatMessage>(), Array.Empty<ToolDefinition>(), new ScriptedRegistry()));
    }

    [Fact]
    public async Task CompleteWithTools_NullRegistry_Throws()
    {
        var client = MakeClient(new ScriptedHandler(Array.Empty<string>()));
        var messages = new List<ChatMessage> { new() { Role = "user", Content = "hi" } };

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.CompleteWithToolsAsync(messages, Array.Empty<ToolDefinition>(), null!));
    }

    // ---------------------------------------------------------------------
    // Acceptance criterion 7: ChatAsync (non-tool path) unchanged.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ChatAsync_NonToolPath_StillWorks_OneRoundTrip()
    {
        // The existing single-shot ChatAsync path is preserved for fallback /
        // non-tool chat. It does not send tools/tool_choice.
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAIResponseJson(content: "hi back", toolCalls: null)
            });
        var client = MakeClient(handler);

        var reply = await client.ChatAsync("hi");

        Assert.Equal("hi back", reply);
        Assert.Equal(1, handler.CallCount);
        // Non-tool path does not send tools or tool_choice.
        Assert.Null(handler.CapturedRequests[0].Tools);
        Assert.Null(handler.CapturedRequests[0].ToolChoice);
    }

    // ---------- helpers ----------

    private static string OpenAIResponseJson(string? content, object[]? toolCalls)
    {
        // OpenAI shape: choices[0].message.{role, content, tool_calls}
        var msg = new
        {
            choices = new[]
            {
                new
                {
                    finish_reason = toolCalls is { Length: > 0 } ? "tool_calls" : "stop",
                    message = new
                    {
                        role = "assistant",
                        content,
                        tool_calls = toolCalls
                    }
                }
            }
        };
        return JsonSerializer.Serialize(msg, JsonOptions);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;
        private readonly bool _healthOk;

        public int CallCount { get; private set; }
        public List<CapturedRequest> CapturedRequests { get; } = new();

        public ScriptedHandler(IEnumerable<string> responses, bool healthOk = true)
        {
            _responses = new Queue<string>(responses);
            _healthOk = healthOk;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // BED-161: CompleteWithToolsAsync probes /health first — do not
            // consume scripted chat responses for the health gate.
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("health", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(
                    _healthOk ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent(_healthOk ? """{"status":"ok"}""" : "down")
                });
            }

            CallCount++;

            // Capture the request body for assertions.
            var body = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            var messages = new List<CapturedMessage>();
            List<object>? tools = null;
            string? toolChoice = null;
            bool stream = false;
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
                            Name: m.TryGetProperty("name", out var n) ? n.GetString() : null,
                            ToolCallId: m.TryGetProperty("tool_call_id", out var tci) ? tci.GetString() : null));
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
                if (doc.RootElement.TryGetProperty("tool_choice", out var tc))
                {
                    toolChoice = tc.ValueKind == JsonValueKind.String
                        ? tc.GetString()
                        : tc.GetRawText();
                }
                if (doc.RootElement.TryGetProperty("stream", out var s) && s.ValueKind == JsonValueKind.False)
                {
                    stream = false;
                }
            }
            CapturedRequests.Add(new CapturedRequest(messages, tools, toolChoice, stream));

            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"choices":[{"message":{"role":"assistant","content":"no more scripts","tool_calls":null}}]}""")
                });
            }
            var json = _responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }

    private sealed record CapturedRequest(
        IReadOnlyList<CapturedMessage> Messages,
        List<object>? Tools,
        string? ToolChoice,
        bool Stream);

    private sealed record CapturedMessage(string Role, string? Content, string? Name, string? ToolCallId);

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
