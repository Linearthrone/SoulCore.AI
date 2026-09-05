using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Safety;
using SoulCore.Host.Ws;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Memory;
using SoulCore.Protocol;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// Unit tests for the ChatWebSocketHandler tool-loop wiring (TASK-128).
/// Verifies the handler routes chat turns through the agent tool-loop
/// (CompleteWithToolsAsync) when UseToolLoop=true, falls back to the
/// single-shot CompleteAsync + keyword detectors when UseToolLoop=false,
/// and applies Strategy A double-trigger suppression (skips keyword
/// fallback for verb classes the model already called a tool for).
/// <para>
/// The WebSocket layer is faked with a <see cref="FakeWebSocket"/> subclass
/// so no network is hit. Inference/memory/charter/emotion/unreal are
/// faked with scripted stubs. These tests cover AC #1, #2, #3, #4, #7.
/// </para>
/// </summary>
public class ChatWebSocketHandlerToolLoopTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static ChatWsOptions MakeChatOptions(bool useToolLoop = true) => new()
    {
        Path = "/ws",
        StubWhenModelDown = false,
        UseToolLoop = useToolLoop
    };

    private static InferenceOptions MakeInferenceOptions() => new()
    {
        Enabled = true,
        BaseUrl = "http://127.0.0.1:11434",
        Model = "test-model",
        MaxToolIterations = 8,
        MaxTokens = 128,
        NumCtx = 0,
        ThinkEnabled = false
    };

    private static ChatWebSocketHandler MakeHandler(
        IInferenceClient inference,
        IToolRegistry toolRegistry,
        IUnrealVerbClient unreal,
        ChatWsOptions? chatOptions = null,
        IEmotionState? emotion = null,
        IMemoryStore? memory = null,
        IToolsAccessSettings? toolsAccess = null)
    {
        emotion ??= new StubEmotionState();
        memory ??= new StubMemoryStore();
        var embeddings = new NullEmbeddingClient();
        var charter = new StubCharter();
        var soulLoop = new StubSoulLoop();
        var spendMeter = new SpendMeter();
        var driftWatcher = new DriftWatcher(15);
        var hub = new PresenceWsHub(new LoggerFactory().CreateLogger<PresenceWsHub>());
        var sessionHistory = new ChatSessionHistoryStore(40);

        var chatOpts = Options.Create(chatOptions ?? MakeChatOptions());
        var infOpts = Options.Create(MakeInferenceOptions());
        var logger = new LoggerFactory().CreateLogger<ChatWebSocketHandler>();

        return new ChatWebSocketHandler(
            inference, emotion, memory, embeddings, charter,
            unreal, soulLoop, toolRegistry, sessionHistory, spendMeter, driftWatcher,
            hub, chatOpts, infOpts,
            toolsAccess: toolsAccess
                ?? new ComputerControlGate(allowDesktopCapture: true, allowComputerControl: true),
            logger);
    }

    private static string ChatSendFrame(string text) =>
        SoulCoreFrame.Create(SoulCoreFrameTypes.ChatSend, new { text }).ToJson();

    private static List<SoulCoreFrame> ParseOutboundFrames(List<string> rawFrames)
    {
        var frames = new List<SoulCoreFrame>(rawFrames.Count);
        foreach (var raw in rawFrames)
        {
            if (SoulCoreFrame.TryParse(raw, out var f) && f is not null)
                frames.Add(f);
        }
        return frames;
    }

    private static async Task<List<SoulCoreFrame>> RunOneChatTurnAsync(
        ChatWebSocketHandler handler,
        string userText,
        CancellationTokenSource? externalCts = null)
    {
        var inboundFrames = new[]
        {
            Encoding.UTF8.GetBytes(ChatSendFrame(userText)),
            Array.Empty<byte>() // sentinel — FakeWebSocket signals close after the send frame
        };
        var socket = new FakeWebSocket(inboundFrames);
        using var ownedCts = externalCts is null ? new CancellationTokenSource(TimeSpan.FromSeconds(5)) : null;
        var cts = externalCts ?? ownedCts!;
        await handler.RunAsync(socket, cts.Token).ConfigureAwait(false);
        return ParseOutboundFrames(socket.SentFrames);
    }

    // ---------------------------------------------------------------------
    // AC #1 + #7: UseToolLoop=true routes through CompleteWithToolsAsync
    // and emits chat.done with the loop's final text.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UseToolLoop_True_RoutesThroughToolLoop_EmitsChatDone()
    {
        var inference = new ScriptedInferenceClient
        {
            CompleteWithToolsReply = "tool-loop reply"
        };
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        var frames = await RunOneChatTurnAsync(handler, "hello victoria");

        // The handler MUST have called CompleteWithToolsAsync for the chat turn.
        // (CompleteAsync may still be called afterward by AuthorChatEpisodicAsync
        //  for memory authoring — that's a separate LLM call, not the chat turn.)
        Assert.True(inference.CompleteWithToolsCalled, "CompleteWithToolsAsync should be called for the chat turn when UseToolLoop=true");
        // A chat.done frame must be present with the loop's reply text.
        var done = frames.FirstOrDefault(f => f.Type == SoulCoreFrameTypes.ChatDone);
        Assert.NotNull(done);
        Assert.Equal("tool-loop reply", done!.Payload?.GetProperty("text").GetString());
        Assert.Equal("ollama", done.Payload?.GetProperty("provider").GetString());
    }

    // ---------------------------------------------------------------------
    // BED-162 / ISSUE-001: NL workflow prompts force tool_choice + agency guidance
    // ---------------------------------------------------------------------

    [Fact]
    public async Task WorkflowNlCreate_ForcesWorkflowCreate_AndAppendsAgencyGuidance()
    {
        var inference = new ScriptedInferenceClient { CompleteWithToolsReply = "created" };
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        await RunOneChatTurnAsync(
            handler,
            "create a workflow to: 1) recall a memory, 2) speak the memory");

        Assert.True(inference.CompleteWithToolsCalled);
        Assert.Equal("workflow_create", inference.LastLoopOptions?.ForceToolName);
        Assert.Contains("[Tools]", inference.LastSystemContent ?? "", StringComparison.Ordinal);
        Assert.Contains("workflow_create", inference.LastSystemContent ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkflowNlExecute_ForcesWorkflowExecute()
    {
        var inference = new ScriptedInferenceClient { CompleteWithToolsReply = "ran" };
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        await RunOneChatTurnAsync(handler, "run that workflow");

        Assert.True(inference.CompleteWithToolsCalled);
        Assert.Equal("workflow_execute", inference.LastLoopOptions?.ForceToolName);
    }

    [Fact]
    public async Task WorkflowNlRunAgain_ForcesWorkflowExecute()
    {
        var inference = new ScriptedInferenceClient { CompleteWithToolsReply = "complete" };
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        await RunOneChatTurnAsync(handler, "run that workflow again");

        Assert.Equal("workflow_execute", inference.LastLoopOptions?.ForceToolName);
    }

    [Fact]
    public async Task NonWorkflowChat_DoesNotForceToolChoice()
    {
        var inference = new ScriptedInferenceClient { CompleteWithToolsReply = "hi" };
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        await RunOneChatTurnAsync(handler, "hello victoria");

        Assert.True(inference.CompleteWithToolsCalled);
        Assert.Null(inference.LastLoopOptions?.ForceToolName);
    }

    [Fact]
    public async Task DesktopNlOpenChrome_ForcesDesktopOpenApp()
    {
        var inference = new ScriptedInferenceClient { CompleteWithToolsReply = "Opened Chrome." };
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        await RunOneChatTurnAsync(handler, "open Google Chrome");

        Assert.True(inference.CompleteWithToolsCalled);
        Assert.Equal("desktop_open_app", inference.LastLoopOptions?.ForceToolName);
        Assert.Contains("[Computer]", inference.LastSystemContent ?? "", StringComparison.Ordinal);
    }

    // VM scope: ForceTool stays desktop_open_app (guest inject, not host Process.Start).
    [Fact]
    public async Task DesktopNlOpenChrome_VmScoped_StillForcesOpenApp()
    {
        var inference = new ScriptedInferenceClient { CompleteWithToolsReply = "Opened Firefox in the VM." };
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var unreal = new RecordingUnrealVerbClient();
        var scoped = new ComputerControlGate(
            allowDesktopCapture: true,
            allowBrowserCapture: true,
            allowComputerControl: true,
            allowMt4Read: false,
            allowMt4Trade: false,
            desktopTargetWindowTitle: "victoria-sandbox");
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true),
            toolsAccess: scoped);

        await RunOneChatTurnAsync(handler, "open Google Chrome");

        Assert.True(inference.CompleteWithToolsCalled);
        Assert.Equal("desktop_open_app", inference.LastLoopOptions?.ForceToolName);
        Assert.Contains("DESKTOP SCOPE", inference.LastSystemContent ?? "", StringComparison.Ordinal);
        Assert.Contains("Preferred workflow", inference.LastSystemContent ?? "", StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // BED-167 / ISSUE-003: NL MT4 status → ForceToolName=mt4_status
    // (Avenue B PreferHermes→Ollama too; exclusivity via BED-165)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Mt4NlStatus_ForcesMt4Status_AndAgencyMentionsTool()
    {
        var inference = new ScriptedInferenceClient { CompleteWithToolsReply = "connected" };
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        await RunOneChatTurnAsync(handler, "what's my MT4 status?");

        Assert.True(inference.CompleteWithToolsCalled);
        Assert.Equal("mt4_status", inference.LastLoopOptions?.ForceToolName);
        Assert.Contains("mt4_status", inference.LastSystemContent ?? "", StringComparison.Ordinal);
        Assert.Contains("task_create", inference.LastSystemContent ?? "", StringComparison.Ordinal);
    }


    // ---------------------------------------------------------------------
    // Ollama tool-loop uses IInferenceClient.CompleteWithToolsAsync (PROP-7+).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task OllamaToolLoop_UsesInferenceClient()
    {
        var inference = new ScriptedInferenceClient
        {
            CompleteWithToolsReply = "ollama tool-loop reply"
        };
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        var frames = await RunOneChatTurnAsync(handler, "hello via ollama tool loop");

        Assert.True(inference.CompleteWithToolsCalled);
        var done = frames.FirstOrDefault(f => f.Type == SoulCoreFrameTypes.ChatDone);
        Assert.NotNull(done);
        Assert.Equal("ollama tool-loop reply", done!.Payload?.GetProperty("text").GetString());
        Assert.Equal("ollama", done.Payload?.GetProperty("provider").GetString());
    }


    // ---------------------------------------------------------------------
    // AC #4: Strategy A — when the model calls a tool whose name maps to a
    // verb class, the keyword fallback for that class is skipped (no double
    // side-effect). We verify by recording the Unreal verb calls.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task StrategyA_AnimationToolFired_SkipsAnimationKeywordFallback()
    {
        // The model calls `play_animation` (a registered tool). The user text
        // also contains "wave" (animation keyword). Strategy A must skip the
        // keyword fallback so Victoria does not double-wave.
        var playAnim = new FakeAnimationTool();
        var registry = new ToolRegistry(new ITool[] { playAnim });
        var inference = new ScriptedInferenceClient
        {
            // The tool-loop "calls" the tool then returns text. We simulate
            // by having the registry dispatch record the call; the inference
            // stub just returns the final text.
            CompleteWithToolsReply = "I waved at you."
        };
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        var frames = await RunOneChatTurnAsync(handler, "wave hello");

        // The tool was dispatched (the loop called play_animation).
        Assert.True(playAnim.WasCalled, "play_animation tool should have been dispatched by the loop");
        // The keyword fallback DetectAnimationIntent("wave hello") would normally
        // fire PlayAnimationAsync("wave"). Strategy A skips it because a tool in
        // the animation class fired this turn. So Unreal should NOT have received
        // a PlayAnimationAsync from the keyword path.
        Assert.Empty(unreal.PlayAnimationCalls);
        // chat.done still emitted.
        var done = frames.FirstOrDefault(f => f.Type == SoulCoreFrameTypes.ChatDone);
        Assert.NotNull(done);
    }

    [Fact]
    public async Task StrategyA_NoToolFired_KeywordFallbackStillRuns()
    {
        // No tools registered → no tool can fire → keyword fallback must still
        // run (current behavior preserved for the no-tool case).
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var inference = new ScriptedInferenceClient
        {
            CompleteWithToolsReply = "I waved."
        };
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        var frames = await RunOneChatTurnAsync(handler, "wave hello");

        // No tool fired, so the keyword fallback runs and dispatches the animation.
        Assert.Single(unreal.PlayAnimationCalls);
        Assert.Equal("wave", unreal.PlayAnimationCalls[0]);
    }

    [Fact]
    public async Task StrategyA_LocoToolFired_SkipsLocoKeywordFallback()
    {
        var moveTool = new FakeMoveTool();
        var registry = new ToolRegistry(new ITool[] { moveTool });
        var inference = new ScriptedInferenceClient
        {
            CompleteWithToolsReply = "I walked forward."
        };
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        var frames = await RunOneChatTurnAsync(handler, "walk forward");

        Assert.True(moveTool.WasCalled, "move_to tool should have been dispatched");
        // Keyword fallback DetectLocoIntent("walk forward") would fire LocoAsync.
        // Strategy A skips it because a loco-class tool fired.
        Assert.Empty(unreal.LocoCalls);
    }

    // ---------------------------------------------------------------------
    // AC #5: speak auto-play preserved (Unreal SpeakAsync called with reply).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SpeakAutoPlay_StillEmitsSpeakAsyncWithReply()
    {
        var inference = new ScriptedInferenceClient
        {
            CompleteWithToolsReply = "the reply text"
        };
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        await RunOneChatTurnAsync(handler, "say something");

        Assert.Single(unreal.SpeakCalls);
        Assert.Equal("the reply text", unreal.SpeakCalls[0]);
    }

    /// <summary>
    /// TASK-156 / QA-130 AC7: after chat.done, RequestAborted (dying WS CT) must not
    /// prevent SpeakAsync. Simulates abort during post-chat episodic write; emotion
    /// GetAsync would throw on a cancelled CT (mirrors SqliteMemoryStore). Speak must
    /// still be recorded on the unreal stub (UE connected not required for Pass).
    /// </summary>
    [Fact]
    public async Task SpeakAutoPlay_StillRuns_WhenRequestAbortedAfterChatDone()
    {
        using var requestCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var inference = new ScriptedInferenceClient
        {
            CompleteWithToolsReply = "Hello."
        };
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var unreal = new RecordingUnrealVerbClient { IsConnectedOverride = false };
        var emotion = new CtSensitiveEmotionState();
        var memory = new AbortOnEpisodicWriteMemoryStore(requestCts);
        var handler = MakeHandler(inference, registry, unreal,
            MakeChatOptions(useToolLoop: true),
            emotion, memory);

        var frames = await RunOneChatTurnAsync(handler, "Say hello...", requestCts);

        var done = frames.FirstOrDefault(f => f.Type == SoulCoreFrameTypes.ChatDone);
        Assert.NotNull(done);
        Assert.Equal("Hello.", done!.Payload?.GetProperty("text").GetString());
        Assert.True(requestCts.IsCancellationRequested, "episodic write should have aborted request CT");
        Assert.Single(unreal.SpeakCalls);
        Assert.Equal("Hello.", unreal.SpeakCalls[0]);
        Assert.False(unreal.IsConnected, "UE connected must not be required for SpeakAsync attempt");
    }

    // ---------------------------------------------------------------------
    // AC #6: empty tools registry → tool-loop behaves like single-shot
    // (model returns text in one round-trip; no tools dispatched).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task EmptyToolRegistry_ToolLoopBehavesLikeSingleShot()
    {
        var inference = new ScriptedInferenceClient
        {
            CompleteWithToolsReply = "no tools, just text"
        };
        var registry = new ToolRegistry(Array.Empty<ITool>());
        var unreal = new RecordingUnrealVerbClient();
        var handler = MakeHandler(inference, registry, unreal, MakeChatOptions(useToolLoop: true));

        var frames = await RunOneChatTurnAsync(handler, "hi");

        Assert.True(inference.CompleteWithToolsCalled);
        Assert.Empty(inference.ToolDispatches);
        var done = frames.FirstOrDefault(f => f.Type == SoulCoreFrameTypes.ChatDone);
        Assert.NotNull(done);
        Assert.Equal("no tools, just text", done!.Payload?.GetProperty("text").GetString());
    }

    // ---------- stubs ----------

    private sealed class ScriptedInferenceClient : IInferenceClient
    {
        public string CompleteAsyncReply { get; set; } = "default";
        public string CompleteWithToolsReply { get; set; } = "default-tool-loop";
        public bool CompleteAsyncCalled { get; private set; }
        public bool CompleteWithToolsCalled { get; private set; }
        public List<string> ToolDispatches { get; } = new();
        public ToolLoopOptions? LastLoopOptions { get; private set; }
        public string? LastSystemContent { get; private set; }

        public Task<string> CompleteAsync(
            string prompt, string? systemPreamble = null,
            CancellationToken cancellationToken = default, int? maxTokens = null)
        {
            CompleteAsyncCalled = true;
            return Task.FromResult(CompleteAsyncReply);
        }

        public Task<string> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            IToolRegistry registry,
            CancellationToken cancellationToken = default,
            ToolLoopOptions? loopOptions = null)
        {
            CompleteWithToolsCalled = true;
            LastLoopOptions = loopOptions;
            LastSystemContent = messages.FirstOrDefault(m => m.Role == "system")?.Content;
            // Simulate dispatching any registered tools so Strategy A sees them.
            var defs = registry.GetDefinitions();
            foreach (var d in defs)
            {
                ToolDispatches.Add(d.Name);
                registry.ExecuteAsync(d.Name, default, cancellationToken).GetAwaiter().GetResult();
            }
            return Task.FromResult(CompleteWithToolsReply);
        }
    }


    private sealed class FakeAnimationTool : ITool
    {
        public bool WasCalled;
        public ToolDefinition Definition { get; } = new(
            Name: "play_animation",
            Description: "Plays an animation.",
            Parameters: JsonDocument.Parse("""{"type":"object","properties":{"name":{"type":"string"}}}""").RootElement.Clone());

        public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new ToolResult(true, "animation played", null));
        }
    }

    private sealed class FakeMoveTool : ITool
    {
        public bool WasCalled;
        public ToolDefinition Definition { get; } = new(
            Name: "move_to",
            Description: "Moves the avatar.",
            Parameters: JsonDocument.Parse("""{"type":"object","properties":{"forward":{"type":"number"}}}""").RootElement.Clone());

        public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new ToolResult(true, "moved", null));
        }
    }

    private sealed class RecordingUnrealVerbClient : IUnrealVerbClient
    {
        public bool IsConnectedOverride { get; set; } = true;
        public bool IsConnected => IsConnectedOverride;
        public string TargetUrl => "ws://test";
        public List<string> SpeakCalls { get; } = new();
        public List<string> PlayAnimationCalls { get; } = new();
        public List<object> LocoCalls { get; } = new();
        public List<object> LookCalls { get; } = new();

        public Task EnsureConnectedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> SetEmotionAsync(object emotionPayload, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            SpeakCalls.Add(text);
            return Task.FromResult(IsConnected);
        }

        public Task<bool> SpeakAsync(object speakPayload, CancellationToken cancellationToken = default)
        {
            SpeakCalls.Add(speakPayload?.ToString() ?? "");
            return Task.FromResult(IsConnected);
        }
        public Task<bool> PlayAnimationAsync(string animationName, CancellationToken cancellationToken = default)
        {
            PlayAnimationCalls.Add(animationName);
            return Task.FromResult(true);
        }
        public Task<bool> LocoAsync(object locoPayload, CancellationToken cancellationToken = default)
        {
            LocoCalls.Add(locoPayload);
            return Task.FromResult(true);
        }
        public Task<bool> LookAsync(object lookPayload, CancellationToken cancellationToken = default)
        {
            LookCalls.Add(lookPayload);
            return Task.FromResult(true);
        }

        public Task<bool> MoveToAsync(object moveToPayload, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> StopAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    /// <summary>
    /// Mirrors SqliteMemoryStore/emotion path: throws when the request CT is already cancelled
    /// (QA-130 AC7 failure mode before TASK-156).
    /// </summary>
    private sealed class CtSensitiveEmotionState : IEmotionState
    {
        public Task<IReadOnlyDictionary<string, double>> GetAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyDictionary<string, double>>(
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase));
        }

        public Task SetAsync(IReadOnlyDictionary<string, double> components, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(1L);
        }
    }

    /// <summary>
    /// Cancels the request CTS during post-chat episodic write (after chat.done),
    /// simulating RequestAborted while authoring/side-effects still run.
    /// </summary>
    private sealed class AbortOnEpisodicWriteMemoryStore : IMemoryStore
    {
        private readonly CancellationTokenSource _requestCts;

        public AbortOnEpisodicWriteMemoryStore(CancellationTokenSource requestCts) => _requestCts = requestCts;

        public bool IsDatabaseOpen => true;
        public string DatabasePath => ":memory:";

        public Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default)
        {
            _requestCts.Cancel();
            return Task.FromResult(1L);
        }

        public Task StoreEmbeddingAsync(long episodicId, float[] vector, string model, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<(long, string)>>(Array.Empty<(long, string)>());
        public Task<IReadOnlyList<string>> RecallSimilarAsync(float[] queryVector, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class StubEmotionState : IEmotionState
    {
        public Task<IReadOnlyDictionary<string, double>> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, double>>(new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase));
        public Task SetAsync(IReadOnlyDictionary<string, double> components, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1L);
    }

    private sealed class StubMemoryStore : IMemoryStore
    {
        public bool IsDatabaseOpen => true;
        public string DatabasePath => ":memory:";
        public Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default)
            => Task.FromResult(1L);
        public Task StoreEmbeddingAsync(long episodicId, float[] vector, string model, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<(long, string)>>(Array.Empty<(long, string)>());
        public Task<IReadOnlyList<string>> RecallSimilarAsync(float[] queryVector, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class StubCharter : ICharter
    {
        public Task<IReadOnlyList<string>> GetAnchorsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<IReadOnlyList<string>> GetAnchorsByKindAsync(string kind, bool? lockedOnly = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<int> SeedAsync(IReadOnlyList<CharterAnchorSeed> seeds, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class StubSoulLoop : ISoulLoop
    {
        public bool IsEnabled => false;
        public string? LastWant => null;
        public Task TickAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Minimal fake WebSocket that feeds a queue of inbound byte[] messages
    /// (one per ReceiveAsync call) and captures every outbound send. After
    /// the last inbound message it returns a Close result so RunAsync exits.
    /// </summary>
    private sealed class FakeWebSocket : WebSocket
    {
        private readonly Queue<byte[]> _inbound;
        private readonly List<string> _sent = new();
        private WebSocketState _state = WebSocketState.Open;

        public FakeWebSocket(IEnumerable<byte[]> inbound)
        {
            _inbound = new Queue<byte[]>(inbound);
        }

        public List<string> SentFrames => _sent;

        public override WebSocketState State => _state;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string SubProtocol => string.Empty;

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (_inbound.Count == 0)
            {
                _state = WebSocketState.CloseReceived;
                return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            }

            var msg = _inbound.Dequeue();
            if (msg.Length == 0)
            {
                // Sentinel: signal close so RunAsync exits its receive loop.
                _state = WebSocketState.CloseReceived;
                return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            }

            var count = Math.Min(msg.Length, buffer.Count);
            Buffer.BlockCopy(msg, 0, buffer.Array!, buffer.Offset, count);
            return Task.FromResult(new WebSocketReceiveResult(count, WebSocketMessageType.Text, endOfMessage: true));
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken cancellationToken)
        {
            _sent.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
            return Task.CompletedTask;
        }

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus, string? closeStatusDescription,
            CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus, string? closeStatusDescription,
            CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override void Abort()
        {
            _state = WebSocketState.Aborted;
        }

        public override void Dispose() { }
    }
}
