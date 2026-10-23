using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Persona;
using SoulCore.Core.Safety;
using SoulCore.Host.Loop;
using SoulCore.Host.Persona;
using SoulCore.Host.Ws;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Memory;
using SoulCore.Protocol;

namespace SoulCore.Protocol.Tests;

/// <summary>
/// PROP-16.6: host mode, pinned chat turns, foreground-only tools, SoulLoop foreground writes.
/// </summary>
public class ChatMultiTests
{
    [Fact]
    public async Task MissingModeStorage_ReadsAsOne_AndOtherValuesAre400()
    {
        var root = CreateTempRoot();
        var mode = new HostChatMode(root);
        var modePath = Path.Combine(root, "chat-mode.txt");
        try
        {
            Assert.Equal(IHostChatMode.One, mode.Current);
            Assert.False(File.Exists(modePath));

            var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
            var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
            await using var api = await ModeApi.StartAsync(mode, packs, hub);

            var getMissing = await api.Client.GetAsync("/api/personas/mode");
            Assert.Equal(HttpStatusCode.OK, getMissing.StatusCode);
            Assert.Equal(IHostChatMode.One, await ReadModeAsync(getMissing));
            Assert.False(File.Exists(modePath));

            var rejected = await api.Client.PostAsJsonAsync("/api/personas/mode", new { mode = "both" });
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            var rejectedCase = await api.Client.PostAsJsonAsync("/api/personas/mode", new { mode = "One" });
            Assert.Equal(HttpStatusCode.BadRequest, rejectedCase.StatusCode);
            var rejectedEmpty = await api.Client.PostAsJsonAsync("/api/personas/mode", new { mode = "" });
            Assert.Equal(HttpStatusCode.BadRequest, rejectedEmpty.StatusCode);
            Assert.False(File.Exists(modePath));
            Assert.Equal(IHostChatMode.One, mode.Current);

            var setMany = await api.Client.PostAsJsonAsync("/api/personas/mode", new { mode = "many" });
            Assert.Equal(HttpStatusCode.OK, setMany.StatusCode);
            Assert.Equal(IHostChatMode.Many, await ReadModeAsync(setMany));
            Assert.Equal(IHostChatMode.Many, (await File.ReadAllTextAsync(modePath)).Trim());

            var getMany = await api.Client.GetAsync("/api/personas/mode");
            Assert.Equal(HttpStatusCode.OK, getMany.StatusCode);
            Assert.Equal(IHostChatMode.Many, await ReadModeAsync(getMany));

            var reread = new HostChatMode(root);
            Assert.Equal(IHostChatMode.Many, reread.Current);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ModeOne_OmittedPersona_WritesForeground_AndRejectsOtherWithoutWrite()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        const string personaA = "persona-a";
        const string personaB = "persona-b";
        try
        {
            await packs.UpsertAsync(Pack(personaA, allowDesktop: true));
            await packs.UpsertAsync(Pack(personaB, allowDesktop: true));
            var gate = new ForegroundToolLoopGate();
            var session = new ActivePersonaSession(
                packs,
                hub,
                NullLogger<ActivePersonaSession>.Instance,
                gate);
            await session.InitializeAsync();
            await session.SetActiveAsync(personaA);
            Assert.Equal(personaA, session.ActivePersonaId);
            Assert.False(File.Exists(Path.Combine(root, "chat-mode.txt")));
            await hub.GetMemoryStore(personaA).WriteEpisodicAsync("only-foreground-memory", "system");

            var countingSession = new CountingSession(session);
            var countingMode = new CountingMode(new HostChatMode(root));
            var inference = new ScriptedChat();
            var handler = BuildHandler(inference, countingSession, countingMode, packs, hub, gate, new ToolRegistry(Array.Empty<ITool>()));
            var socket = new OutboundWebSocket();

            var readsBefore = countingSession.GetActiveCalls;
            var idReadsBefore = countingSession.ActiveIdCalls;
            var modeReadsBefore = countingMode.Reads;

            await handler.HandleAsync(
                socket,
                SoulCoreFrame.Create(SoulCoreFrameTypes.ChatSend, new { text = "hello-a", sessionId = "thread-a" }),
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.Equal(1, countingSession.GetActiveCalls - readsBefore);
            Assert.Equal(1, countingSession.ActiveIdCalls - idReadsBefore);
            Assert.Equal(1, countingMode.Reads - modeReadsBefore);
            Assert.Contains(Frames(socket), f => f.Type == SoulCoreFrameTypes.ChatDone);
            Assert.Contains("only-foreground-memory", inference.LastSystem ?? "", StringComparison.Ordinal);
            var aRecent = await hub.GetMemoryStore(personaA).RecallRecentAsync(20);
            Assert.Contains(aRecent, t => t == "episodic-pinned");
            Assert.False(File.Exists(PersonaMemoryPaths.ResolveMemoryDbPath(root, personaB)));
            Assert.Equal(personaA, session.ActivePersonaId);
            Assert.Equal(personaA, hub.ActivePersonaId);

            var aTranscript = await hub.GetTranscriptStore(personaA).GetRecentAsync("thread-a", 10);
            Assert.Contains(aTranscript, m => m.Role == "user" && m.Content == "hello-a");

            var toolsBeforeReject = inference.CompleteWithToolsCalls;
            var asyncBeforeReject = inference.CompleteAsyncCalls;
            var rejectSocket = new OutboundWebSocket();
            readsBefore = countingSession.GetActiveCalls;
            modeReadsBefore = countingMode.Reads;

            await handler.HandleAsync(
                rejectSocket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.ChatSend,
                    new { text = "hello-b", sessionId = "thread-b", personaId = personaB }),
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.Equal(1, countingSession.GetActiveCalls - readsBefore);
            Assert.Equal(1, countingMode.Reads - modeReadsBefore);
            Assert.Equal(toolsBeforeReject, inference.CompleteWithToolsCalls);
            Assert.Equal(asyncBeforeReject, inference.CompleteAsyncCalls);
            var rejected = Frames(rejectSocket).Single(f => f.Type == SoulCoreFrameTypes.Error);
            Assert.Equal("chat.persona_rejected", rejected.Payload!.Value.GetProperty("code").GetString());
            Assert.False(File.Exists(PersonaMemoryPaths.ResolveMemoryDbPath(root, personaB)));
            var aAfterReject = await hub.GetMemoryStore(personaA).RecallRecentAsync(20);
            Assert.Equal(aRecent, aAfterReject);
            Assert.Equal(personaA, session.ActivePersonaId);
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ModeMany_WritesPersonaB_WhileAStaysForeground_AndDoesNotDispatchATool()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        const string personaA = "persona-a";
        const string personaB = "persona-b";
        try
        {
            await packs.UpsertAsync(Pack(personaA, allowDesktop: true));
            await packs.UpsertAsync(Pack(personaB, allowDesktop: true));
            var gate = new ForegroundToolLoopGate();
            var session = new ActivePersonaSession(
                packs,
                hub,
                NullLogger<ActivePersonaSession>.Instance,
                gate);
            await session.InitializeAsync();
            await session.SetActiveAsync(personaA);
            var mode = new HostChatMode(root);
            await mode.SetAsync(IHostChatMode.Many);

            var desktop = new RecordingTool("desktop_screenshot");
            var inference = new ScriptedChat();
            var handler = BuildHandler(
                inference,
                session,
                mode,
                packs,
                hub,
                gate,
                new ToolRegistry(new ITool[] { desktop }));

            var missing = new OutboundWebSocket();
            await handler.HandleAsync(
                missing,
                SoulCoreFrame.Create(SoulCoreFrameTypes.ChatSend, new { text = "need-a-persona" }),
                Guid.NewGuid(),
                CancellationToken.None);
            var required = Frames(missing).Single(f => f.Type == SoulCoreFrameTypes.Error);
            Assert.Equal("chat.persona_required", required.Payload!.Value.GetProperty("code").GetString());
            Assert.False(File.Exists(PersonaMemoryPaths.ResolveMemoryDbPath(root, personaB)));
            Assert.Equal(0, inference.CompleteAsyncCalls);
            Assert.Equal(0, inference.CompleteWithToolsCalls);

            await hub.GetMemoryStore(personaB).WriteEpisodicAsync("only-b-memory", "system");
            var socket = new OutboundWebSocket();
            await handler.HandleAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.ChatSend,
                    new { text = "hello-b", sessionId = "thread-b", personaId = personaB }),
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.Contains(Frames(socket), f => f.Type == SoulCoreFrameTypes.ChatDone);
            Assert.Contains("only-b-memory", inference.LastSystem ?? "", StringComparison.Ordinal);
            Assert.DoesNotContain("only-foreground-memory", inference.LastSystem ?? "", StringComparison.Ordinal);
            Assert.Equal(0, inference.CompleteWithToolsCalls);
            Assert.True(inference.CompleteAsyncCalls > 0);
            Assert.Equal(0, desktop.Calls);
            Assert.Equal(personaA, session.ActivePersonaId);
            Assert.Equal(personaA, hub.ActivePersonaId);

            var bRecent = await hub.GetMemoryStore(personaB).RecallRecentAsync(20);
            Assert.Contains(bRecent, t => t == "episodic-pinned");
            var aRecent = await hub.GetMemoryStore(personaA).RecallRecentAsync(20);
            Assert.DoesNotContain(aRecent, t => t.Contains("episodic-pinned") || t.Contains("hello-b"));

            var bTranscript = await hub.GetTranscriptStore(personaB).GetRecentAsync("thread-b", 10);
            Assert.Contains(bTranscript, m => m.Role == "user" && m.Content == "hello-b");
            var aTranscript = await hub.GetTranscriptStore(personaA).GetRecentAsync("thread-b", 10);
            Assert.DoesNotContain(aTranscript, m => m.Content == "hello-b");
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ForegroundTurn_AllowDesktopFalse_DoesNotDispatchDesktopTool()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        const string personaA = "persona-a";
        try
        {
            await packs.UpsertAsync(Pack(personaA, allowDesktop: false, allowBrowser: true, allowEmail: true));
            var gate = new ForegroundToolLoopGate();
            var session = new ActivePersonaSession(
                packs,
                hub,
                NullLogger<ActivePersonaSession>.Instance,
                gate);
            await session.InitializeAsync();
            await session.SetActiveAsync(personaA);

            var desktop = new RecordingTool("desktop_screenshot");
            var browser = new RecordingTool("browser_navigate");
            var email = new RecordingTool("email_send");
            var speak = new RecordingTool("speak");
            var inference = new ScriptedChat { DispatchListedTools = true };
            var handler = BuildHandler(
                inference,
                session,
                new HostChatMode(root),
                packs,
                hub,
                gate,
                new ToolRegistry(new ITool[] { desktop, browser, email, speak }));

            var socket = new OutboundWebSocket();
            await handler.HandleAsync(
                socket,
                SoulCoreFrame.Create(SoulCoreFrameTypes.ChatSend, new { text = "ping", sessionId = "thread-a" }),
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.Contains(Frames(socket), f => f.Type == SoulCoreFrameTypes.ChatDone);
            Assert.True(inference.CompleteWithToolsCalls > 0);
            Assert.NotNull(inference.SeenTools);
            Assert.DoesNotContain(inference.SeenTools!, d => d.Name == "desktop_screenshot");
            Assert.Contains(inference.SeenTools!, d => d.Name == "speak");
            Assert.Equal(0, desktop.Calls);
            Assert.True(speak.Calls > 0);
            Assert.True(browser.Calls > 0);
            Assert.True(email.Calls > 0);
            Assert.Equal(personaA, session.ActivePersonaId);
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ForegroundChange_CancelsPreviousToolLoop_AndDoesNotCancelBackgroundChat()
    {
        var root = CreateTempRoot();
        var packs = new PersonaPackStore(root, NullLogger<PersonaPackStore>.Instance);
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        const string personaA = "persona-a";
        const string personaB = "persona-b";
        const string personaC = "persona-c";
        try
        {
            await packs.UpsertAsync(Pack(personaA, allowDesktop: true));
            await packs.UpsertAsync(Pack(personaB, allowDesktop: true));
            await packs.UpsertAsync(Pack(personaC, allowDesktop: true));
            var gate = new ForegroundToolLoopGate();
            var session = new ActivePersonaSession(
                packs,
                hub,
                NullLogger<ActivePersonaSession>.Instance,
                gate);
            await session.InitializeAsync();
            await session.SetActiveAsync(personaA);
            var mode = new HostChatMode(root);
            await mode.SetAsync(IHostChatMode.Many);

            var desktop = new RecordingTool("desktop_open_app");
            var inference = new ScriptedChat { HoldToolLoop = true, DelayBackground = true };
            var handler = BuildHandler(
                inference,
                session,
                mode,
                packs,
                hub,
                gate,
                new ToolRegistry(new ITool[] { desktop }));

            var foregroundSocket = new OutboundWebSocket();
            var foregroundTask = handler.HandleAsync(
                foregroundSocket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.ChatSend,
                    new { text = "ping-foreground", sessionId = "thread-a", personaId = personaA }),
                Guid.NewGuid(),
                CancellationToken.None);

            var enteredTool = await Task.WhenAny(inference.ToolLoopEntered.Task, Task.Delay(10000));
            Assert.Same(inference.ToolLoopEntered.Task, enteredTool);

            var backgroundSocket = new OutboundWebSocket();
            var backgroundTask = handler.HandleAsync(
                backgroundSocket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.ChatSend,
                    new { text = "hello-b", sessionId = "thread-b", personaId = personaB }),
                Guid.NewGuid(),
                CancellationToken.None);

            var enteredBackground = await Task.WhenAny(inference.BackgroundEntered.Task, Task.Delay(10000));
            Assert.Same(inference.BackgroundEntered.Task, enteredBackground);

            await session.SetActiveAsync(personaC);
            Assert.Equal(personaC, session.ActivePersonaId);

            var foregroundDone = await Task.WhenAny(foregroundTask, Task.Delay(10000));
            Assert.Same(foregroundTask, foregroundDone);
            await foregroundTask;

            var backgroundDone = await Task.WhenAny(backgroundTask, Task.Delay(10000));
            Assert.Same(backgroundTask, backgroundDone);
            await backgroundTask;

            var foregroundError = Frames(foregroundSocket).Single(f => f.Type == SoulCoreFrameTypes.Error);
            Assert.Equal("chat.foreground_changed", foregroundError.Payload!.Value.GetProperty("code").GetString());
            Assert.DoesNotContain(Frames(foregroundSocket), f => f.Type == SoulCoreFrameTypes.ChatDone);
            Assert.Contains(Frames(backgroundSocket), f => f.Type == SoulCoreFrameTypes.ChatDone);
            Assert.False(inference.BackgroundSawCancel);
            Assert.Equal(0, desktop.Calls);

            var bRecent = await hub.GetMemoryStore(personaB).RecallRecentAsync(20);
            Assert.Contains(bRecent, t => t == "episodic-pinned");
            var aRecent = await hub.GetMemoryStore(personaA).RecallRecentAsync(20);
            Assert.DoesNotContain(aRecent, t => t.Contains("episodic-pinned") || t.Contains("ping-foreground"));
            var cRecent = await hub.GetMemoryStore(personaC).RecallRecentAsync(20);
            Assert.DoesNotContain(cRecent, t => t.Contains("episodic-pinned") || t.Contains("hello-b"));
            Assert.Equal(personaC, hub.ActivePersonaId);
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SoulLoop_DoesNotWriteBackgroundPersonaStore()
    {
        var root = CreateTempRoot();
        var hub = new PersonaStoreHub(root, NullLogger<PersonaStoreHub>.Instance);
        const string personaA = "loop-a";
        const string personaB = "loop-b";
        try
        {
            await hub.SwitchActiveAsync(personaA);
            await hub.GetMemoryStore(personaB).WriteEpisodicAsync("b-sentinel", "system");

            var poison = new PoisonMemory();
            var session = new FixedIdSession(personaA);
            var presence = new PresenceWsHub(NullLogger<PresenceWsHub>.Instance);
            var loop = new SoulLoopScaffold(
                new SteadyEmotion(),
                poison,
                presence,
                Options.Create(new SoulLoopOptions
                {
                    Enabled = true,
                    EpisodicRecallLimit = 1,
                    ReflectionIntervalTicks = 1,
                    ProactiveChatEnabled = false
                }),
                new DriftWatcher(sloMinutes: 15),
                NullLogger<SoulLoopScaffold>.Instance,
                journals: null,
                outbound: null,
                personaSession: session,
                personaStores: hub);

            await loop.TickAsync();

            Assert.Equal(0, poison.Writes);
            var aRecent = await hub.GetMemoryStore(personaA).RecallRecentAsync(20);
            Assert.Contains(aRecent, t => t.Contains("[Reflection]", StringComparison.Ordinal));
            var bRecent = await hub.GetMemoryStore(personaB).RecallRecentAsync(20);
            Assert.Contains(bRecent, t => t == "b-sentinel");
            Assert.DoesNotContain(bRecent, t => t.Contains("[Reflection]", StringComparison.Ordinal));
            Assert.Equal(personaA, hub.ActivePersonaId);
        }
        finally
        {
            await hub.DisposeAsync();
            TryDelete(root);
        }
    }

    private static ChatSendHandler BuildHandler(
        IInferenceClient inference,
        IPersonaSession session,
        IHostChatMode mode,
        IPersonaPackStore packs,
        IPersonaStoreHub hub,
        IForegroundToolLoopGate gate,
        IToolRegistry tools)
    {
        var loggerFactory = NullLoggerFactory.Instance;
        var emotion = new SteadyEmotion();
        var charter = new EmptyCharter();
        var access = new ComputerControlGate(
            allowDesktopCapture: true,
            allowBrowserCapture: true,
            allowComputerControl: true,
            allowMt4Read: false,
            allowMt4Trade: false,
            browserBackend: ToolsOptions.BackendNative);
        var context = new ChatContextBuilder(
            new PoisonMemory(),
            new NullEmbeddingClient(),
            charter,
            emotion,
            access,
            session,
            loggerFactory.CreateLogger<ChatContextBuilder>());
        var snapshot = new EmotionSnapshotSender(emotion, loggerFactory.CreateLogger<EmotionSnapshotSender>());
        var post = new ChatPostEffectsHandler(
            emotion,
            new NullUnrealVerbClient(),
            new DriftWatcher(15),
            loggerFactory.CreateLogger<ChatPostEffectsHandler>());

        return new ChatSendHandler(
            inference,
            new PoisonMemory(),
            new NullEmbeddingClient(),
            new ChatSessionHistoryStore(40),
            tools,
            context,
            snapshot,
            post,
            new SpendMeter(),
            Options.Create(new ChatWsOptions { UseToolLoop = true, StubWhenModelDown = false }),
            Options.Create(new InferenceOptions { Enabled = true }),
            access,
            loggerFactory.CreateLogger<ChatSendHandler>(),
            personaSession: session,
            hostChatMode: mode,
            packStore: packs,
            storeHub: hub,
            toolLoops: gate);
    }

    private static PersonaPack Pack(string id, bool allowDesktop, bool allowBrowser = true, bool allowEmail = true) =>
        new()
        {
            PersonaId = id,
            DisplayName = id,
            HumanAddress = "Friend",
            ContactId = id,
            IdentityBlurb = id + " blurb",
            ToolPolicy = new PersonaToolPolicy
            {
                AllowDesktop = allowDesktop,
                AllowBrowser = allowBrowser,
                AllowEmail = allowEmail
            }
        };

    private static List<SoulCoreFrame> Frames(OutboundWebSocket socket)
    {
        var frames = new List<SoulCoreFrame>();
        foreach (var raw in socket.Sent)
        {
            if (SoulCoreFrame.TryParse(raw, out var frame) && frame is not null)
                frames.Add(frame);
        }

        return frames;
    }

    private static async Task<string?> ReadModeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("mode").GetString();
    }

    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), "soulcore-chat-multi-" + Guid.NewGuid().ToString("N"));

    private static void TryDelete(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // SQLite may still be releasing the file on Windows.
        }
    }

    private sealed class ModeApi : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public HttpClient Client { get; }

        private ModeApi(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public static async Task<ModeApi> StartAsync(IHostChatMode mode, IPersonaPackStore packs, IPersonaStoreHub hub)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = "ChatMultiTests",
                EnvironmentName = "Development"
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton(mode);
            builder.Services.AddSingleton(packs);
            builder.Services.AddSingleton(hub);
            builder.Services.AddSingleton<IPersonaSession>(new FixedBlankPersonaSession());
            builder.Services.AddSingleton<IPersonaToolPathsResolver>(new IdleToolPaths());
            builder.Services.AddSingleton<IOptions<InferenceOptions>>(Options.Create(new InferenceOptions()));
            var app = builder.Build();
            app.MapPersonaApi();
            await app.StartAsync();
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
            var client = new HttpClient { BaseAddress = new Uri(addresses!.Addresses.First()) };
            return new ModeApi(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class IdleToolPaths : IPersonaToolPathsResolver
    {
        public string ResolveDesktopTargetWindowTitle() => "";
        public string ResolvePlaywrightUserDataDir() => "";
    }

    private sealed class CountingMode : IHostChatMode
    {
        private readonly IHostChatMode _inner;
        public int Reads;

        public CountingMode(IHostChatMode inner) => _inner = inner;

        public string Current
        {
            get
            {
                Reads++;
                return _inner.Current;
            }
        }

        public Task SetAsync(string mode, CancellationToken cancellationToken = default) =>
            _inner.SetAsync(mode, cancellationToken);
    }

    private sealed class CountingSession : IPersonaSession
    {
        private readonly IPersonaSession _inner;
        public int GetActiveCalls;
        public int ActiveIdCalls;

        public CountingSession(IPersonaSession inner) => _inner = inner;

        public string ActivePersonaId
        {
            get
            {
                ActiveIdCalls++;
                return _inner.ActivePersonaId;
            }
        }

        public PersonaPack GetActive()
        {
            GetActiveCalls++;
            return _inner.GetActive();
        }

        public bool TryGet(string personaId, out PersonaPack pack) => _inner.TryGet(personaId, out pack);

        public IReadOnlyDictionary<string, PersonaPack> GetLoadedSnapshot() => _inner.GetLoadedSnapshot();

        public Task SetActiveAsync(string personaId, CancellationToken cancellationToken = default) =>
            _inner.SetActiveAsync(personaId, cancellationToken);

        public Task ReloadActiveAsync(CancellationToken cancellationToken = default) =>
            _inner.ReloadActiveAsync(cancellationToken);
    }

    private sealed class FixedIdSession : IPersonaSession
    {
        public FixedIdSession(string id) => ActivePersonaId = id;

        public string ActivePersonaId { get; }

        public PersonaPack GetActive() => new()
        {
            PersonaId = ActivePersonaId,
            DisplayName = ActivePersonaId
        };

        public bool TryGet(string personaId, out PersonaPack pack)
        {
            if (string.Equals(personaId, ActivePersonaId, StringComparison.OrdinalIgnoreCase))
            {
                pack = GetActive();
                return true;
            }

            pack = null!;
            return false;
        }

        public IReadOnlyDictionary<string, PersonaPack> GetLoadedSnapshot() =>
            new Dictionary<string, PersonaPack>(StringComparer.OrdinalIgnoreCase)
            {
                [ActivePersonaId] = GetActive()
            };

        public Task SetActiveAsync(string personaId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReloadActiveAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ScriptedChat : IInferenceClient
    {
        public int CompleteAsyncCalls;
        public int CompleteWithToolsCalls;
        public string? LastSystem;
        public IReadOnlyList<ToolDefinition>? SeenTools;
        public bool DispatchListedTools;
        public bool HoldToolLoop;
        public bool DelayBackground;
        public bool BackgroundSawCancel;
        public TaskCompletionSource ToolLoopEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource BackgroundEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<string> CompleteAsync(
            string prompt,
            string? systemPreamble = null,
            CancellationToken cancellationToken = default,
            int? maxTokens = null)
        {
            Interlocked.Increment(ref CompleteAsyncCalls);
            if (maxTokens != EpisodicMemoryPrompt.AuthorMaxTokens)
                LastSystem = systemPreamble;
            if (DelayBackground)
            {
                BackgroundEntered.TrySetResult();
                try
                {
                    await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    BackgroundSawCancel = true;
                    throw;
                }
            }

            if (maxTokens == EpisodicMemoryPrompt.AuthorMaxTokens)
                return "episodic-pinned";
            return "chat-visible";
        }

        public async Task<string> CompleteWithToolsAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            IToolRegistry registry,
            CancellationToken cancellationToken = default,
            ToolLoopOptions? loopOptions = null)
        {
            Interlocked.Increment(ref CompleteWithToolsCalls);
            SeenTools = tools;
            LastSystem = messages.FirstOrDefault(m => m.Role == "system")?.Content;
            if (HoldToolLoop)
            {
                ToolLoopEntered.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }

            if (DispatchListedTools)
            {
                await registry.ExecuteAsync("desktop_screenshot", default, cancellationToken).ConfigureAwait(false);
                foreach (var definition in tools)
                    await registry.ExecuteAsync(definition.Name, default, cancellationToken).ConfigureAwait(false);
            }

            return "chat-visible";
        }
    }

    private sealed class RecordingTool : ITool
    {
        private static readonly JsonElement Schema = JsonDocument.Parse(
            """{"type":"object","properties":{}}""").RootElement.Clone();

        public RecordingTool(string name) =>
            Definition = new ToolDefinition(name, name, Schema);

        public int Calls;
        public ToolDefinition Definition { get; }

        public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new ToolResult(true, "ok"));
        }
    }

    private sealed class PoisonMemory : IMemoryStore
    {
        public int Writes;
        public bool IsDatabaseOpen => true;
        public string DatabasePath => ":memory:";

        public Task<long> WriteEpisodicAsync(string text, string sourceLabel, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Writes);
            throw new InvalidOperationException("chat turn wrote the unbound memory store");
        }

        public Task StoreEmbeddingAsync(long episodicId, float[] vector, string model, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<(long Id, string Content)>> ListEpisodicsMissingEmbeddingsAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(long, string)>>(Array.Empty<(long, string)>());

        public Task<IReadOnlyList<string>> RecallSimilarAsync(float[] queryVector, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> RecallRecentAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class SteadyEmotion : IEmotionState
    {
        public Task<IReadOnlyDictionary<string, double>> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, double>>(new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["valence"] = 0.2,
                ["arousal"] = 0.3,
                ["dominance"] = 0.4,
                ["focus"] = 0.5
            });

        public Task SetAsync(IReadOnlyDictionary<string, double> components, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);
    }

    private sealed class EmptyCharter : ICharter
    {
        public Task<IReadOnlyList<string>> GetAnchorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> GetAnchorsByKindAsync(string kind, bool? lockedOnly = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<int> SeedAsync(IReadOnlyList<CharterAnchorSeed> seeds, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class OutboundWebSocket : WebSocket
    {
        private readonly List<string> _sent = new();
        public IReadOnlyList<string> Sent => _sent;
        public override WebSocketState State => WebSocketState.Open;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string SubProtocol => string.Empty;

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            _sent.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
            return Task.CompletedTask;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? closeStatusDescription, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? closeStatusDescription, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override void Abort()
        {
        }

        public override void Dispose()
        {
        }
    }
}
