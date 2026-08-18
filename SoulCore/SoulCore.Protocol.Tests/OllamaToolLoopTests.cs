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
    public async Task ForceToolName_SendsObjectToolChoice_OnFirstIterationOnly()
    {
        // Iteration 0 uses /v1/chat/completions (OpenAI shape) because tool_choice is forced.
        // Iteration 1 uses native /api/chat (Ollama shape) with no tool_choice.
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAiChatResponseJson(content: "", toolCalls: new[]
                {
                    new { function = new { name = "echo", arguments = new { text = "forced" } } }
                }),
                ChatResponseJson(content: "done", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("echo", _ => new ToolResult(true, "ok", null)));
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "create a workflow to: 1) recall a memory, 2) speak the memory" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages,
            new[] { EchoToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "echo" });

        Assert.Equal("done", result);
        Assert.Equal(2, handler.CallCount);
        Assert.Contains("v1/chat/completions", handler.CapturedRequests[0].Path, StringComparison.Ordinal);
        Assert.NotNull(handler.CapturedRequests[0].ToolChoiceRaw);
        Assert.Contains("\"name\":\"echo\"", handler.CapturedRequests[0].ToolChoiceRaw!, StringComparison.Ordinal);
        Assert.Contains("api/chat", handler.CapturedRequests[1].Path, StringComparison.Ordinal);
        Assert.Null(handler.CapturedRequests[1].ToolChoiceRaw);
    }

    [Fact]
    public async Task ForceToolName_UnknownTool_OmitsToolChoice()
    {
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(content: "plain", toolCalls: null)
            });
        var client = MakeClient(handler, registry: new ScriptedRegistry());

        await client.CompleteWithToolsAsync(
            new List<ChatMessage> { new() { Role = "user", Content = "hi" } },
            new[] { EchoToolDef() },
            new ScriptedRegistry(),
            loopOptions: new ToolLoopOptions { ForceToolName = "workflow_create" });

        Assert.Contains("api/chat", handler.CapturedRequests[0].Path, StringComparison.Ordinal);
        Assert.Null(handler.CapturedRequests[0].ToolChoiceRaw);
    }

    // ---------------------------------------------------------------------
    // BED-165: ForceToolName exclusive tools[] + hard refuse of wrong names.
    // ---------------------------------------------------------------------

    private static ToolDefinition WorkflowExecuteToolDef() => new(
        Name: "workflow_execute",
        Description: "Execute a workflow.",
        Parameters: JsonDocument.Parse("""{"type":"object","properties":{"id":{"type":"integer"}}}""").RootElement.Clone());

    private static ToolDefinition DesktopOpenAppToolDef() => new(
        Name: "desktop_open_app",
        Description: "Open an allowlisted local app.",
        Parameters: JsonDocument.Parse(
                """{"type":"object","properties":{"app":{"type":"string"},"args":{"type":"string"}}}""")
            .RootElement.Clone());

    private static ToolDefinition TaskListToolDef() => new(
        Name: "task_list",
        Description: "List tasks.",
        Parameters: JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone());

    [Fact]
    public async Task ForceToolName_ExclusiveToolsArray_OnlyForcedToolAdvertised()
    {
        // Full registry has workflow_execute + task_list, but ForceToolName must
        // advertise ONLY workflow_execute on iteration 0 (/v1).
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAiChatResponseJson(content: "", toolCalls: new[]
                {
                    new { function = new { name = "workflow_execute", arguments = new { id = 1 } } }
                }),
                ChatResponseJson(content: "ran", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("workflow_execute", _ => new ToolResult(true, "workflow id=1 execute all: ok", null)),
            ("task_list", _ => new ToolResult(true, "tasks: none", null)));
        var client = MakeClient(handler, registry: registry);

        var result = await client.CompleteWithToolsAsync(
            new List<ChatMessage> { new() { Role = "user", Content = "run that workflow" } },
            new[] { WorkflowExecuteToolDef(), TaskListToolDef(), EchoToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "workflow_execute" });

        Assert.Equal("ran", result);
        Assert.Equal(2, handler.CallCount);
        Assert.Contains("v1/chat/completions", handler.CapturedRequests[0].Path, StringComparison.Ordinal);
        Assert.NotNull(handler.CapturedRequests[0].Tools);
        Assert.Single(handler.CapturedRequests[0].Tools!);
        Assert.Contains("workflow_execute", handler.CapturedRequests[0].Tools![0].ToString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("task_list", handler.CapturedRequests[0].Tools![0].ToString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("echo", handler.CapturedRequests[0].Tools![0].ToString()!, StringComparison.Ordinal);
        Assert.NotNull(handler.CapturedRequests[0].ToolChoiceRaw);
        Assert.Contains("\"name\":\"workflow_execute\"", handler.CapturedRequests[0].ToolChoiceRaw!, StringComparison.Ordinal);

        // Iteration 1 restores full tools (no force).
        Assert.Contains("api/chat", handler.CapturedRequests[1].Path, StringComparison.Ordinal);
        Assert.Null(handler.CapturedRequests[1].ToolChoiceRaw);
        Assert.NotNull(handler.CapturedRequests[1].Tools);
        Assert.Equal(3, handler.CapturedRequests[1].Tools!.Count);
    }

    [Fact]
    public async Task ForceToolName_WrongToolName_DoesNotExecute_ReturnsRefusal()
    {
        // Even if the model invents task_list under a force for workflow_execute,
        // Host must NOT execute it (QA-142 AC6 wrong-tool escape).
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAiChatResponseJson(content: "", toolCalls: new[]
                {
                    new { function = new { name = "task_list", arguments = new { } } }
                }),
                ChatResponseJson(content: "after refuse", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("workflow_execute", _ => new ToolResult(true, "should not matter", null)),
            ("task_list", _ => new ToolResult(true, "ESCAPE EXECUTED — BUG", null)));
        var client = MakeClient(handler, registry: registry);

        var result = await client.CompleteWithToolsAsync(
            new List<ChatMessage> { new() { Role = "user", Content = "run that workflow" } },
            new[] { WorkflowExecuteToolDef(), TaskListToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "workflow_execute" });

        Assert.Equal("after refuse", result);
        Assert.Empty(registry.Calls); // neither tool executed
        var toolMsg = handler.CapturedRequests[1].Messages
            .FirstOrDefault(m => m.Role == "tool");
        Assert.NotNull(toolMsg);
        Assert.Contains("forced tool 'workflow_execute' required", toolMsg!.Content ?? "", StringComparison.Ordinal);
        Assert.Contains("refused 'task_list'", toolMsg.Content ?? "", StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // BED-166: /v1 ForceTool path must stringify object-form arguments from
    // session history (Ollama Go unmarshal requires string, not object).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ForceToolName_HistoryObjectArguments_AreStringifiedOnV1Wire()
    {
        // Session history from a prior /api/chat turn stores tool_calls with
        // object-form arguments. ForceTool posts those messages to /v1 — they
        // MUST be JSON strings on the wire or Ollama returns 400:
        //   cannot unmarshal object into ... arguments of type string
        // BED-168: text-only under ForceTool=workflow_execute with a known
        // session id soft-dispatches — provide a follow-up final text turn.
        var priorArgs = JsonDocument.Parse("""{"id":42,"all":true}""").RootElement.Clone();
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAiChatResponseJson(content: "Which workflow?", toolCalls: null),
                ChatResponseJson(content: "ran", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("workflow_execute", _ => new ToolResult(true, "workflow id=42 execute all: ok", null)));
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "run that workflow" },
            new()
            {
                Role = "assistant",
                Content = "",
                ToolCalls = new[]
                {
                    new ChatToolCall
                    {
                        Function = new ChatFunctionCall
                        {
                            Name = "workflow_execute",
                            Arguments = priorArgs
                        }
                    }
                }
            },
            new() { Role = "tool", Name = "workflow_execute", Content = "workflow id=42 execute all: ok" },
            new() { Role = "user", Content = "run that workflow again" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages,
            new[] { WorkflowExecuteToolDef(), EchoToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "workflow_execute" });

        Assert.Equal("ran", result);
        Assert.True(handler.CallCount >= 1);
        Assert.Contains("v1/chat/completions", handler.CapturedRequests[0].Path, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(handler.CapturedRequests[0].RawBody));

        using var doc = JsonDocument.Parse(handler.CapturedRequests[0].RawBody!);
        var msgs = doc.RootElement.GetProperty("messages");
        JsonElement? assistantToolCalls = null;
        foreach (var m in msgs.EnumerateArray())
        {
            if (m.TryGetProperty("role", out var role)
                && role.GetString() == "assistant"
                && m.TryGetProperty("tool_calls", out var tcs)
                && tcs.ValueKind == JsonValueKind.Array
                && tcs.GetArrayLength() > 0)
            {
                assistantToolCalls = tcs;
                break;
            }
        }

        Assert.True(assistantToolCalls.HasValue, "expected prior assistant tool_calls in /v1 body");
        var argsEl = assistantToolCalls!.Value[0].GetProperty("function").GetProperty("arguments");
        Assert.Equal(JsonValueKind.String, argsEl.ValueKind);
        var argsText = argsEl.GetString();
        Assert.False(string.IsNullOrWhiteSpace(argsText));
        using var argsDoc = JsonDocument.Parse(argsText!);
        Assert.Equal(JsonValueKind.Object, argsDoc.RootElement.ValueKind);
        Assert.Equal(42, argsDoc.RootElement.GetProperty("id").GetInt32());
        Assert.True(argsDoc.RootElement.GetProperty("all").GetBoolean());

        // Soft-dispatch must have executed workflow_execute with the session id.
        Assert.Contains(registry.Calls, c => c.Name == "workflow_execute");
    }

    // ---------------------------------------------------------------------
    // BED-168: ForceTool text-only must not end the loop — soft-dispatch
    // workflow_execute when session id known, else forced retry nudge.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ForceToolName_TextOnly_SoftDispatchesWorkflowExecute_WhenSessionIdKnown()
    {
        // QA-142 Retest-4 AC6: exclusive ForceTool + /v1 200 but model emits
        // clarification prose → soft-dispatch workflow_execute with id from
        // prior create result.
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAiChatResponseJson(
                    content: "Sure — which workflow id should I run?",
                    toolCalls: null),
                ChatResponseJson(content: "workflow finished", toolCalls: null)
            });
        JsonElement? seenArgs = null;
        var registry = new ScriptedRegistry(
            ("workflow_execute", args =>
            {
                seenArgs = args.Clone();
                return new ToolResult(true, "workflow id=7 execute all: ok", null);
            }));
        var client = MakeClient(handler, registry: registry);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "create a workflow to: 1) recall a memory, 2) speak the memory" },
            new() { Role = "assistant", Content = "", ToolCalls = new[]
            {
                new ChatToolCall
                {
                    Function = new ChatFunctionCall
                    {
                        Name = "workflow_create",
                        Arguments = JsonDocument.Parse("""{"name":"ac5","steps":[]}""").RootElement.Clone()
                    }
                }
            }},
            new() { Role = "tool", Name = "workflow_create", Content = "created: id=7 name=ac5 steps=2" },
            new() { Role = "user", Content = "run that workflow" }
        };

        var result = await client.CompleteWithToolsAsync(
            messages,
            new[] { WorkflowExecuteToolDef(), EchoToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "workflow_execute" });

        Assert.Equal("workflow finished", result);
        Assert.Single(registry.Calls);
        Assert.Equal("workflow_execute", registry.Calls[0].Name);
        Assert.NotNull(seenArgs);
        Assert.Equal(7, seenArgs!.Value.GetProperty("id").GetInt64());
        Assert.True(seenArgs.Value.GetProperty("all").GetBoolean());
        // Soft-dispatch happens in-process — no second /v1 force round required
        // before the post-tool /api/chat final text.
        Assert.Equal(2, handler.CallCount);
        Assert.Contains("v1/chat/completions", handler.CapturedRequests[0].Path, StringComparison.Ordinal);
        Assert.Contains("api/chat", handler.CapturedRequests[1].Path, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // BED-180: ForceTool desktop_open_app pre-dispatches without LLM wait.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ForceToolName_OpenApp_PreDispatchesWithoutLlm_WhenPureOpen()
    {
        var handler = new ScriptedHandler(Array.Empty<string>());
        JsonElement? seenArgs = null;
        var registry = new ScriptedRegistry(
            ("desktop_open_app", args =>
            {
                seenArgs = args.Clone();
                return new ToolResult(true, "opened app 'chrome'", null);
            }));
        var client = MakeClient(handler, registry: registry);

        var result = await client.CompleteWithToolsAsync(
            new List<ChatMessage>
            {
                new() { Role = "user", Content = "open Google Chrome" }
            },
            new[] { DesktopOpenAppToolDef(), EchoToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "desktop_open_app" });

        Assert.Equal("Opened Chrome.", result);
        Assert.Single(registry.Calls);
        Assert.Equal("desktop_open_app", registry.Calls[0].Name);
        Assert.NotNull(seenArgs);
        Assert.Equal("chrome", seenArgs!.Value.GetProperty("app").GetString());
        // Pure open must not hit Ollama at all.
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ForceToolName_OpenApp_PreDispatchesWithUrl_WhenPresent()
    {
        var handler = new ScriptedHandler(Array.Empty<string>());
        JsonElement? seenArgs = null;
        var registry = new ScriptedRegistry(
            ("desktop_open_app", args =>
            {
                seenArgs = args.Clone();
                return new ToolResult(true, "opened", null);
            }));
        var client = MakeClient(handler, registry: registry);

        var result = await client.CompleteWithToolsAsync(
            new List<ChatMessage>
            {
                new() { Role = "user", Content = "open chrome to https://example.com" }
            },
            new[] { DesktopOpenAppToolDef(), EchoToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "desktop_open_app" });

        Assert.Equal("Opened Chrome to https://example.com.", result);
        Assert.Equal(0, handler.CallCount);
        Assert.NotNull(seenArgs);
        Assert.Equal("chrome", seenArgs!.Value.GetProperty("app").GetString());
        Assert.Equal("https://example.com", seenArgs.Value.GetProperty("args").GetString());
    }

    [Fact]
    public async Task ForceToolName_OpenApp_ContinuesLoop_WhenFollowOnActionsPresent()
    {
        // "open … and click" still needs the model after Process.Start.
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(content: "Clicked the first link.", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("desktop_open_app", _ => new ToolResult(true, "opened app 'chrome'", null)));
        var client = MakeClient(handler, registry: registry);

        var result = await client.CompleteWithToolsAsync(
            new List<ChatMessage>
            {
                new() { Role = "user", Content = "open chrome and click the first link" }
            },
            new[] { DesktopOpenAppToolDef(), EchoToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "desktop_open_app" });

        Assert.Equal("Clicked the first link.", result);
        Assert.Contains(registry.Calls, c => c.Name == "desktop_open_app");
        Assert.Equal(1, handler.CallCount);
        Assert.Contains("api/chat", handler.CapturedRequests[0].Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForceToolName_OpenApp_ContinuesLoop_WhenSearchFollowOnPresent()
    {
        // BED-181: "open … and search …" must not early-exit after launch.
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(content: "Searched for cats.", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("desktop_open_app", _ => new ToolResult(true, "opened app 'chrome' [background]", null)));
        var client = MakeClient(handler, registry: registry);

        var result = await client.CompleteWithToolsAsync(
            new List<ChatMessage>
            {
                new() { Role = "user", Content = "open chrome and search for cats" }
            },
            new[] { DesktopOpenAppToolDef(), EchoToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "desktop_open_app" });

        Assert.Equal("Searched for cats.", result);
        Assert.Contains(registry.Calls, c => c.Name == "desktop_open_app");
        Assert.Equal(1, handler.CallCount);
    }

    // ---------------------------------------------------------------------
    // BED-185: Forced browser_snapshot must allow bootstrap open before
    // snapshotting (otherwise "waiting on screen data" occurs).
    // ---------------------------------------------------------------------

    private static ToolDefinition BrowserSnapshotToolDef() => new(
        "browser_snapshot",
        "Snapshot the current VM page (optionally focus query for Login/Sign in).",
        JsonDocument.Parse(
            """{"type":"object","properties":{"query":{"type":"string"}},"additionalProperties":true}""")
        .RootElement.Clone());

    [Fact]
    public async Task ForceToolName_BrowserSnapshot_AllowsBootstrapDesktopOpenApp()
    {
        // Under ForceToolName=browser_snapshot the model may emit a bootstrap
        // call first (desktop_open_app). The loop must execute the bootstrap
        // tool but still keep ForceToolName active until browser_snapshot runs.
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAiChatResponseJson(
                    content: "",
                    toolCalls: new[]
                    {
                        new
                        {
                            function = new { name = "desktop_open_app", arguments = new { app = "chrome", args = "" } }
                        }
                    }),
                OpenAiChatResponseJson(
                    content: "",
                    toolCalls: new[]
                    {
                        new
                        {
                            function = new { name = "browser_snapshot", arguments = new { query = "Login" } }
                        }
                    }),
                ChatResponseJson(content: "done", toolCalls: null)
            });

        var registry = new ScriptedRegistry(
            ("desktop_open_app", _ => new ToolResult(true, "opened chrome", null)),
            ("browser_snapshot", _ => new ToolResult(true, "snapshot ok", null)));

        var client = MakeClient(handler, registry: registry);

        var result = await client.CompleteWithToolsAsync(
            new List<ChatMessage>
            {
                new() { Role = "user", Content = "open the VM browser and snapshot login" }
            },
            new[] { DesktopOpenAppToolDef(), BrowserSnapshotToolDef(), EchoToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "browser_snapshot" });

        Assert.Equal("done", result);
        Assert.Equal(3, handler.CallCount);
        Assert.Contains(registry.Calls, c => c.Name == "desktop_open_app");
        Assert.Contains(registry.Calls, c => c.Name == "browser_snapshot");

        // Both iteration 0 and 1 should be forced /v1 until the forced tool
        // is actually executed.
        Assert.Contains("v1/chat/completions", handler.CapturedRequests[0].Path, StringComparison.Ordinal);
        Assert.Contains("v1/chat/completions", handler.CapturedRequests[1].Path, StringComparison.Ordinal);
        Assert.Contains("api/chat", handler.CapturedRequests[2].Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForceToolName_TextOnly_RetryNudge_ThenDispatches()
    {
        // No session id → cannot soft-dispatch; inject nudge and keep force
        // on the next round so the model must emit the tool call.
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAiChatResponseJson(
                    content: "I can create that workflow for you — want me to?",
                    toolCalls: null),
                OpenAiChatResponseJson(content: "", toolCalls: new[]
                {
                    new
                    {
                        function = new
                        {
                            name = "workflow_create",
                            arguments = new
                            {
                                name = "recall-speak",
                                steps = new[]
                                {
                                    new { description = "recall a memory", tool = "recall_memory" },
                                    new { description = "speak the memory", tool = "speak" }
                                }
                            }
                        }
                    }
                }),
                ChatResponseJson(content: "created", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("workflow_create", _ => new ToolResult(true, "created: id=9 name=recall-speak steps=2", null)));
        var client = MakeClient(handler, registry: registry);

        var workflowCreateDef = new ToolDefinition(
            Name: "workflow_create",
            Description: "Create a workflow.",
            Parameters: JsonDocument.Parse(
                """{"type":"object","properties":{"name":{"type":"string"},"steps":{"type":"array"}}}""")
                .RootElement.Clone());

        var result = await client.CompleteWithToolsAsync(
            new List<ChatMessage>
            {
                new()
                {
                    Role = "user",
                    Content = "create a workflow to: 1) recall a memory, 2) speak the memory"
                }
            },
            new[] { workflowCreateDef, EchoToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "workflow_create" });

        Assert.Equal("created", result);
        Assert.Single(registry.Calls);
        Assert.Equal("workflow_create", registry.Calls[0].Name);
        Assert.Equal(3, handler.CallCount);

        // Round 0 + nudge round both use /v1 force; final text uses /api/chat.
        Assert.Contains("v1/chat/completions", handler.CapturedRequests[0].Path, StringComparison.Ordinal);
        Assert.Contains("v1/chat/completions", handler.CapturedRequests[1].Path, StringComparison.Ordinal);
        Assert.Contains("api/chat", handler.CapturedRequests[2].Path, StringComparison.Ordinal);

        var nudgeRound = handler.CapturedRequests[1];
        Assert.NotNull(nudgeRound.ToolChoiceRaw);
        Assert.Contains("\"name\":\"workflow_create\"", nudgeRound.ToolChoiceRaw!, StringComparison.Ordinal);
        var nudgeUser = nudgeRound.Messages.LastOrDefault(m => m.Role == "user");
        Assert.NotNull(nudgeUser);
        Assert.Contains("workflow_create", nudgeUser!.Content ?? "", StringComparison.Ordinal);
        Assert.Contains("tool call", nudgeUser.Content ?? "", StringComparison.OrdinalIgnoreCase);
    }

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
            new OllamaInferenceClient(null!, opts, logger, toolRegistry: null));
    }

    [Fact]
    public async Task Ctor_NullOptions_Throws()
    {
        var http = new HttpClient(new ScriptedHandler(Array.Empty<string>()));
        var logger = new LoggerFactory().CreateLogger<OllamaInferenceClient>();
        Assert.Throws<ArgumentNullException>(() =>
            new OllamaInferenceClient(http, null!, logger, toolRegistry: null));
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
    public async Task FallbackParser_ExecuteToolTag_DispatchesTool_WhenToolCallsNull()
    {
        // gemma4 leak: tool call in <execute_tool> tags with tool_calls: null.
        var handler = new ScriptedHandler(
            new[]
            {
                ChatResponseJson(
                    content: "<execute_tool> list_desktop_windows{} </execute_tool>",
                    toolCalls: null),
                ChatResponseJson(content: "windows listed", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("list_desktop_windows", _ => new ToolResult(true, "[]", null)));
        var client = MakeClient(handler, registry: registry);

        var result = await client.CompleteWithToolsAsync(
            new List<ChatMessage> { new() { Role = "user", Content = "what windows are open?" } },
            new[] { ListDesktopWindowsToolDef() },
            registry);

        Assert.Equal("windows listed", result);
        Assert.Equal(2, handler.CallCount);
        Assert.Single(registry.Calls);
        Assert.Equal("list_desktop_windows", registry.Calls[0].Name);
    }

    [Fact]
    public async Task ForceTool_ListDesktopWindows_SoftDispatches_WhenModelReturnsTagOnly()
    {
        // ForceTool uses /v1/chat/completions — responses must be OpenAI-shaped.
        var handler = new ScriptedHandler(
            new[]
            {
                OpenAiChatResponseJson(
                    "<execute_tool> list_desktop_windows{} </execute_tool>",
                    toolCalls: null),
                ChatResponseJson(content: "done", toolCalls: null)
            });
        var registry = new ScriptedRegistry(
            ("list_desktop_windows", _ => new ToolResult(true, "[{\"title\":\"Firefox\"}]", null)));
        var client = MakeClient(handler, registry: registry);

        var result = await client.CompleteWithToolsAsync(
            new List<ChatMessage> { new() { Role = "user", Content = "use the vm" } },
            new[] { ListDesktopWindowsToolDef() },
            registry,
            loopOptions: new ToolLoopOptions { ForceToolName = "list_desktop_windows" });

        Assert.Equal("done", result);
        Assert.DoesNotContain("execute_tool", result, StringComparison.OrdinalIgnoreCase);
        Assert.Single(registry.Calls);
        Assert.Equal("list_desktop_windows", registry.Calls[0].Name);
    }

    private static ToolDefinition ListDesktopWindowsToolDef() => new(
        "list_desktop_windows",
        "List visible desktop windows.",
        JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone());

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

    private static string OpenAiChatResponseJson(string content, object[]? toolCalls)
    {
        // OpenAI-compat: arguments must often be a JSON string; serialize nested
        // anonymous objects to strings when needed by re-wrapping.
        object? wireCalls = null;
        if (toolCalls is not null)
        {
            var list = new List<object>();
            foreach (var tc in toolCalls)
            {
                // Expect shape: { function = { name, arguments } }
                var raw = JsonSerializer.Serialize(tc, JsonOptions);
                using var doc = JsonDocument.Parse(raw);
                var fn = doc.RootElement.GetProperty("function");
                var name = fn.GetProperty("name").GetString();
                var argsEl = fn.GetProperty("arguments");
                var argsStr = argsEl.ValueKind == JsonValueKind.String
                    ? argsEl.GetString()
                    : argsEl.GetRawText();
                list.Add(new
                {
                    id = "call_test",
                    type = "function",
                    function = new { name, arguments = argsStr }
                });
            }
            wireCalls = list;
        }

        var msg = new
        {
            choices = new[]
            {
                new
                {
                    finish_reason = toolCalls is null ? "stop" : "tool_calls",
                    message = new
                    {
                        role = "assistant",
                        content,
                        tool_calls = wireCalls
                    }
                }
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
            var path = request.RequestUri?.AbsolutePath ?? "";
            var messages = new List<CapturedMessage>();
            var tools = (List<object>?)null;
            string? toolChoiceRaw = null;
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
                if (doc.RootElement.TryGetProperty("tool_choice", out var tc))
                    toolChoiceRaw = tc.GetRawText();
            }
            CapturedRequests.Add(new CapturedRequest(messages, tools, toolChoiceRaw, path, body));

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

    private sealed record CapturedRequest(
        IReadOnlyList<CapturedMessage> Messages,
        List<object>? Tools,
        string? ToolChoiceRaw = null,
        string Path = "",
        string? RawBody = null);
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
