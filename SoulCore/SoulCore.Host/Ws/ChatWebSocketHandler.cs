using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Protocol;
using SoulCore.Config;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Safety;
using SoulCore.Hermes;
using SoulCore.Inference;
using SoulCore.Inference.Tools.Body;
using SoulCore.Inference.Tools.ChiefArchitect;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Inference.Tools.Trading;
using SoulCore.Inference.Tools.Workflow;
using SoulCore.Memory;

namespace SoulCore.Host.Ws;

/// <summary>
/// Presence chat WebSocket session: chat.send → Ollama (tool-loop) → emotion.snapshot + chat.delta/done,
/// optional episodic write + Unreal speak/set_emotion side-effects.
/// Hermes retired (BED-185); <see cref="IHermesClient"/> is retained only for DI signature compat.
/// </summary>
public sealed class ChatWebSocketHandler
{
    private readonly IInferenceClient _inference;
    private readonly IEmotionState _emotion;
    private readonly IMemoryStore _memory;
    private readonly IEmbeddingClient _embeddings;
    private readonly ICharter _charter;
    private readonly IUnrealVerbClient _unreal;
    private readonly IVoiceSpeakService? _voiceSpeak;
    private readonly ISoulLoop _soulLoop;
    private readonly IToolRegistry _toolRegistry;
    private readonly IChatSessionHistoryStore _sessionHistory;
    private readonly SpendMeter _spendMeter;
    private readonly DriftWatcher _driftWatcher;
    private readonly PresenceWsHub _hub;
    private readonly ChatWsOptions _chatOptions;
    private readonly InferenceOptions _inferenceOptions;
    private readonly ILogger<ChatWebSocketHandler> _logger;

    /// <summary>Max chars of the combined identity+memory preamble (before emotion).</summary>
    /// <summary>
    /// Budget for [Identity]+[Memory] chars. Sized for Victoria_Soul_Evolved (~3k)
    /// plus episodic recall; emotion preamble is always appended outside this budget.
    /// </summary>
    private const int ContextPreambleCharLimit = 16000;

    /// <summary>How many recent non-quarantined episodic memories to fold into the preamble.</summary>
    private const int ContextMemoryRecallLimit = 5;

    public ChatWebSocketHandler(
        IInferenceClient inference,
        IHermesClient hermes,
        IEmotionState emotion,
        IMemoryStore memory,
        IEmbeddingClient embeddings,
        ICharter charter,
        IUnrealVerbClient unreal,
        ISoulLoop soulLoop,
        IToolRegistry toolRegistry,
        IChatSessionHistoryStore sessionHistory,
        SpendMeter spendMeter,
        DriftWatcher driftWatcher,
        PresenceWsHub hub,
        IOptions<ChatWsOptions> chatOptions,
        IOptions<InferenceOptions> inferenceOptions,
        IOptions<HermesOptions> hermesOptions,
        ILogger<ChatWebSocketHandler> logger,
        IVoiceSpeakService? voiceSpeak = null)
    {
        _inference = inference;
        _ = hermes; // BED-185: unused — NullHermesClient only
        _ = hermesOptions;
        _emotion = emotion;
        _memory = memory;
        _embeddings = embeddings;
        _charter = charter;
        _unreal = unreal;
        _voiceSpeak = voiceSpeak;
        _soulLoop = soulLoop;
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _sessionHistory = sessionHistory ?? throw new ArgumentNullException(nameof(sessionHistory));
        _spendMeter = spendMeter;
        _driftWatcher = driftWatcher;
        _hub = hub;
        _chatOptions = chatOptions.Value;
        _inferenceOptions = inferenceOptions.Value;
        _logger = logger;
    }

    public async Task RunAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var sessionId = _hub.Register(socket);
        _logger.LogInformation("WS session {SessionId} accepted", sessionId);

        try
        {
            // Handshake: presence + emotion snapshot so clients see protocol alive.
            await SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.PresenceStatus,
                    new { alive = true, warm = true, phase = 1 }),
                cancellationToken).ConfigureAwait(false);

            await SendEmotionSnapshotAsync(socket, correlationId: null, cancellationToken)
                .ConfigureAwait(false);

            var buffer = new byte[16 * 1024];
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "bye",
                            CancellationToken.None).ConfigureAwait(false);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                var json = Encoding.UTF8.GetString(ms.ToArray());
                await HandleTextAsync(socket, json, sessionId, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _hub.Unregister(sessionId);
            _logger.LogInformation("WS session {SessionId} closed", sessionId);
        }
    }

    private async Task HandleTextAsync(
        WebSocket socket,
        string json,
        Guid connectionSessionId,
        CancellationToken cancellationToken)
    {
        if (!SoulCoreFrame.TryParse(json, out var frame) || frame is null)
        {
            await SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new { code = "frame.invalid", message = "Expected SoulCore JSON envelope {v,type,id,ts,payload}" }),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        switch (frame.Type)
        {
            case SoulCoreFrameTypes.Ping:
                await SendFrameAsync(
                    socket,
                    SoulCoreFrame.Create(SoulCoreFrameTypes.Pong, new { }, id: frame.Id),
                    cancellationToken).ConfigureAwait(false);
                break;

            case SoulCoreFrameTypes.ChatSend:
                await HandleChatSendAsync(socket, frame, connectionSessionId, cancellationToken).ConfigureAwait(false);
                break;

            case SoulCoreFrameTypes.EmotionCorrect:
                await HandleEmotionCorrectAsync(socket, frame, cancellationToken).ConfigureAwait(false);
                break;

            case SoulCoreFrameTypes.LoopTick:
                await HandleLoopTickAsync(socket, frame, cancellationToken).ConfigureAwait(false);
                break;

            default:
                await SendFrameAsync(
                    socket,
                    SoulCoreFrame.Create(
                        SoulCoreFrameTypes.Error,
                        new { code = "frame.unsupported", message = $"Unsupported type '{frame.Type}'" },
                        id: frame.Id),
                    cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Explicit tick for tests. When SoulLoop:Enabled=false → error soulloop.disabled (no want work).
    /// When enabled → TickAsync (hub broadcasts authoritative full-schema loop.want) + loop.tick.ok ack
    /// on this socket only — no second skinny loop.want echo.
    /// </summary>
    private async Task HandleLoopTickAsync(WebSocket socket, SoulCoreFrame frame, CancellationToken cancellationToken)
    {
        if (!_soulLoop.IsEnabled)
        {
            await SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new
                    {
                        code = "soulloop.disabled",
                        message = "SoulLoop:Enabled=false (kill switch). No tick work."
                    },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await _soulLoop.TickAsync(cancellationToken).ConfigureAwait(false);

        // Hub already fan-outs full-schema loop.want; ack on this socket without duplicating want.
        await SendFrameAsync(
            socket,
            SoulCoreFrame.Create(
                SoulCoreFrameTypes.LoopTickOk,
                new { ok = true },
                id: frame.Id),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleEmotionCorrectAsync(WebSocket socket, SoulCoreFrame frame, CancellationToken cancellationToken)
    {
        if (!TryParseEmotionCorrect(frame.Payload, out var components, out var note, out var errorMessage))
        {
            await SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new { code = "emotion.invalid", message = errorMessage ?? "emotion.correct payload invalid" },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        long revisionBefore;
        try
        {
            revisionBefore = await _emotion.GetRevisionAsync(cancellationToken).ConfigureAwait(false);
            await _emotion.SetAsync(components, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "emotion.correct persist failed");
            await SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new { code = "emotion.persist_failed", message = "Failed to persist emotion correction." },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            try
            {
                var v = components["valence"].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var a = components["arousal"].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var d = components["dominance"].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var f = components["focus"].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var episode =
                    $"[emotion_correction] User corrected felt emotion to " +
                    $"valence={v} arousal={a} dominance={d} focus={f}. " +
                    $"Note: {note.Trim()}";
                await _memory.WriteEpisodicAsync(episode, "correction", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Episodic write after emotion.correct failed (state already persisted)");
            }
        }

        var revisionAfter = await _emotion.GetRevisionAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "emotion.correct applied revision {Before} → {After}",
            revisionBefore,
            revisionAfter);

        await SendEmotionSnapshotAsync(socket, frame.Id, cancellationToken, note: note).ConfigureAwait(false);
    }

    private async Task HandleChatSendAsync(
        WebSocket socket,
        SoulCoreFrame frame,
        Guid connectionSessionId,
        CancellationToken cancellationToken)
    {
        var text = ExtractText(frame.Payload);
        if (string.IsNullOrWhiteSpace(text))
        {
            await SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new { code = "chat.empty", message = "chat.send payload.text required" },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        // BED-158: prefer client payload.sessionId; fall back to WS connection id
        // so multi-turn on the same socket still carries history when the client
        // omits sessionId.
        var historySessionId = ResolveHistorySessionId(frame.Payload, connectionSessionId);

        await SendEmotionSnapshotAsync(socket, frame.Id, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> recentMemories = Array.Empty<string>();
        try
        {
            recentMemories = await RecallChatMemoriesAsync(text, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug(
                "Recalled {Count} episodic memories for chat.send",
                recentMemories.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Episodic recall failed; chat continues without memory");
        }

        IReadOnlyList<string> identityAnchors = Array.Empty<string>();
        try
        {
            identityAnchors = await _charter
                .GetAnchorsByKindAsync("identity", null, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogDebug(
                "Loaded {Count} charter identity anchors for chat.send",
                identityAnchors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Charter identity recall failed; chat continues without identity");
        }

        string emotionPreamble;
        try
        {
            var emotion = await _emotion.GetAsync(cancellationToken).ConfigureAwait(false);
            emotionPreamble = EmotionInfluencePrompt.BuildPreamble(emotion);
            _logger.LogDebug(
                "Emotion influence preamble ready ({Length} chars) for chat.send",
                emotionPreamble.Length);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Emotion read failed; chat continues without influence preamble");
            emotionPreamble = EmotionInfluencePrompt.BuildPreamble(
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase));
        }

        var contextPreamble = BuildContextPreamble(identityAnchors, recentMemories, emotionPreamble);
        // BED-162: agency guidance so NL workflow prompts dispatch tools (ISSUE-001).
        if (_chatOptions.UseToolLoop)
        {
            contextPreamble = ToolAgencyGuidance.AppendToPreamble(contextPreamble);
            contextPreamble = ComputerUseGuidance.AppendToPreamble(contextPreamble);
            contextPreamble = HomeBodyGuidance.AppendToPreamble(contextPreamble);
            contextPreamble = ChiefArchitectGuidance.AppendToPreamble(contextPreamble);
        }

        var spendSummary = _spendMeter.GetSummary();
        if (spendSummary.CapExceeded)
        {
            _logger.LogWarning(
                "Spend cap exceeded; refusing chat inference. cost={Cost}/{Cap} tokens={Tokens}/{TokenCap}",
                spendSummary.EstimatedCost,
                spendSummary.MonthlyCap,
                spendSummary.TotalTokens,
                spendSummary.MonthlyTokenCap);
            await SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new
                    {
                        code = "chat.spend_cap",
                        message = "Monthly spend/token cap exceeded. Inference refused."
                    },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        string reply;
        string provider;
        var usedStub = false;
        var dispatchedToolNames = new HashSet<string>(StringComparer.Ordinal);
        var toolTrace = new List<ToolTraceEntry>();

        // Tool-loop path (BED-128): when UseToolLoop=true, route the chat turn
        // through the agent loop (CompleteWithToolsAsync) with tools built from
        // IToolRegistry. When false, fall back to single-shot CompleteAsync /
        // ChatAsync + keyword detectors (pre-tool-loop behavior, no regression).
        var useToolLoop = _chatOptions.UseToolLoop;
        try
        {
            if (useToolLoop)
            {
                var loopResult = await CompleteChatWithToolsAsync(
                        text,
                        contextPreamble,
                        historySessionId,
                        dispatchedToolNames,
                        toolTrace,
                        cancellationToken)
                    .ConfigureAwait(false);
                reply = loopResult.Text;
                provider = loopResult.Provider;
            }
            else
            {
                var result = await CompleteChatAsync(text, contextPreamble, cancellationToken).ConfigureAwait(false);
                reply = result.Text;
                provider = result.Provider;
                // Still record plain user+assistant so non-tool multi-turn has context.
                AppendPlainTurn(historySessionId, text, reply);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat model path failed; stub={Stub} useToolLoop={UseToolLoop}", _chatOptions.StubWhenModelDown, useToolLoop);
            if (!_chatOptions.StubWhenModelDown)
            {
                await SendFrameAsync(
                    socket,
                    SoulCoreFrame.Create(
                        SoulCoreFrameTypes.Error,
                        new
                        {
                            code = "chat.model_down",
                            message = string.IsNullOrWhiteSpace(ex.Message)
                                ? "LLM unreachable (Hermes/Ollama)."
                                : ex.Message
                        },
                        id: frame.Id),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            usedStub = true;
            provider = "stub";
            reply = BuildStubReply(text);
            AppendPlainTurn(historySessionId, text, reply);
        }

        // Chunk into chat.delta frames (cumulative text) then finalize with chat.done.
        foreach (var partial in ChunkReply(reply))
        {
            await SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.ChatDelta,
                    new { text = partial, stub = usedStub, provider },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
        }

        await SendFrameAsync(
            socket,
            SoulCoreFrame.Create(
                SoulCoreFrameTypes.ChatDone,
                new { text = reply, stub = usedStub, provider },
                id: frame.Id),
            cancellationToken).ConfigureAwait(false);

        if (!usedStub)
        {
            try
            {
                var episode = await AuthorChatEpisodicAsync(text, reply, provider, cancellationToken)
                    .ConfigureAwait(false);
                var episodicId = await _memory
                    .WriteEpisodicAsync(episode, "chat", cancellationToken)
                    .ConfigureAwait(false);

                if (_embeddings.IsEnabled)
                {
                    try
                    {
                        var vector = await _embeddings
                            .EmbedAsync(episode, cancellationToken)
                            .ConfigureAwait(false);
                        if (vector.Length > 0)
                        {
                            await _memory
                                .StoreEmbeddingAsync(
                                    episodicId,
                                    vector,
                                    _embeddings.Model,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                    catch (Exception embedEx)
                    {
                        _logger.LogDebug(
                            embedEx,
                            "Post-chat embedding store failed (episodic row {Id} kept)",
                            episodicId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Episodic write after chat failed (reply already sent)");
            }
        }

        // Soft-block Unreal body verbs when drift SLO is exceeded; chat/inference still proceed.
        var driftStatus = _driftWatcher.GetStatus();
        if (driftStatus.SloExceeded)
        {
            var oldestAge = driftStatus.OldestDriftReport is null
                ? TimeSpan.Zero
                : DateTimeOffset.UtcNow - driftStatus.OldestDriftReport.ObservedAt;
            _logger.LogWarning(
                "Drift SLO exceeded — Unreal verbs soft-blocked (unacked={Unacked}, oldestAge={OldestAge})",
                driftStatus.UnackedReports,
                oldestAge);
        }
        else
        {
            // Best-effort CT for post-chat Unreal side effects (TASK-156 / QA-130 AC7).
            // Do not share RequestAborted / dying WS CT — client abort after chat.done must
            // not skip SpeakAsync (or emotion/loco/anim/look). UE connected is not required;
            // SpeakAsync is still invoked (Null/stub returns false / no-op).
            var sideEffectCt = CancellationToken.None;

            // Unreal side-effects — never fail the chat path if UE is down.
            try
            {
                var emotion = await _emotion.GetAsync(sideEffectCt).ConfigureAwait(false);
                var fields = EmotionInfluencePrompt.ReadFields(emotion);
                await _unreal.SetEmotionAsync(new
                {
                    valence = fields.Valence,
                    arousal = fields.Arousal,
                    dominance = fields.Dominance
                }, sideEffectCt).ConfigureAwait(false);

                var spoke = false;
                if (_voiceSpeak is not null)
                {
                    await _voiceSpeak.SpeakAloudAsync(reply, sideEffectCt).ConfigureAwait(false);
                    spoke = true;
                }
                else
                {
                    spoke = await _unreal.SpeakAsync(reply, sideEffectCt).ConfigureAwait(false);
                }
                if (spoke)
                {
                    _logger.LogInformation(
                        "Post-chat SpeakAsync succeeded (replyLen={ReplyLen})",
                        reply.Length);
                }
                else if (!_unreal.IsConnected)
                {
                    _logger.LogInformation(
                        "Post-chat SpeakAsync attempted — Unreal not connected (no-op)");
                }
                else
                {
                    _logger.LogInformation(
                        "Post-chat SpeakAsync returned false (connected but speak failed)");
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogDebug(oce, "Post-chat SpeakAsync/emotion side-effect cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unreal verb side-effect ignored");
            }

            // Locomotion intent dispatch — keyword detection on the ORIGINAL user text.
            // Strategy A (BED-128): skip the keyword fallback when the model already
            // called a tool whose name maps to the locomotion verb class this turn
            // (avoids double-trigger: model calls move_to + user text contains "walk"
            // → only the tool runs, the keyword does not re-fire the same motion).
            // Independent try/catch so loco runs even if speak failed and never breaks chat.
            try
            {
                if (!ToolClassFiredThisTurn(dispatchedToolNames, ToolVerbClass.Loco))
                {
                    var locoIntent = DetectLocoIntent(text);
                    if (locoIntent is not null)
                    {
                        await _unreal.LocoAsync(new
                        {
                            forward = locoIntent.Forward,
                            right = locoIntent.Right,
                            up = locoIntent.Up
                        }, sideEffectCt).ConfigureAwait(false);
                        _logger.LogInformation(
                            "Unreal loco intent dispatched: forward={Forward} right={Right} up={Up} (from chat text)",
                            locoIntent.Forward, locoIntent.Right, locoIntent.Up);
                    }
                }
                else
                {
                    _logger.LogDebug(
                        "Loco keyword fallback skipped — model called a loco-class tool this turn (strategy A). Tools={Tools}",
                        string.Join(",", dispatchedToolNames));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unreal loco side-effect ignored");
            }

            // Animation intent dispatch — keyword detection on the ORIGINAL user text.
            // Strategy A (BED-128): skip when the model called an animation-class tool
            // this turn (e.g. play_animation) so Victoria does not double-wave.
            // Independent try/catch so animation runs even if loco/speak failed and never breaks chat.
            try
            {
                if (!ToolClassFiredThisTurn(dispatchedToolNames, ToolVerbClass.Animation))
                {
                    var animationName = DetectAnimationIntent(text);
                    if (animationName is not null)
                    {
                        await _unreal.PlayAnimationAsync(animationName, sideEffectCt).ConfigureAwait(false);
                        _logger.LogInformation(
                            "Unreal animation intent dispatched: anim={AnimationName} (from chat text)",
                            animationName);
                    }
                }
                else
                {
                    _logger.LogDebug(
                        "Animation keyword fallback skipped — model called an animation-class tool this turn (strategy A). Tools={Tools}",
                        string.Join(",", dispatchedToolNames));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unreal animation side-effect ignored");
            }

            // Look intent dispatch — keyword detection on the ORIGINAL user text.
            // Strategy A (BED-128): skip when the model called a look-class tool
            // this turn (e.g. look_at) so Victoria does not double-look.
            // Independent try/catch so look runs even if animation/loco/speak failed and never breaks chat.
            // The UE mapper ignores the payload and always sends the fixed look_at_player command.
            try
            {
                if (!ToolClassFiredThisTurn(dispatchedToolNames, ToolVerbClass.Look))
                {
                    var lookIntent = DetectLookIntent(text);
                    if (lookIntent)
                    {
                        await _unreal.LookAsync(null!, sideEffectCt).ConfigureAwait(false);
                        _logger.LogInformation(
                            "Unreal look intent dispatched: look_at_player (from chat text)");
                    }
                }
                else
                {
                    _logger.LogDebug(
                        "Look keyword fallback skipped — model called a look-class tool this turn (strategy A). Tools={Tools}",
                        string.Join(",", dispatchedToolNames));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unreal look side-effect ignored");
            }
        }
    }

    /// <summary>
    /// Asks the chat provider (same as the reply when possible) to author a short first-person
    /// episodic memory. Falls back to the legacy briefing template on empty/failure so the
    /// chat path never fails after <c>chat.done</c>.
    /// </summary>
    private async Task<string> AuthorChatEpisodicAsync(
        string userText,
        string assistantReply,
        string chatProvider,
        CancellationToken cancellationToken)
    {
        var template = EpisodicMemoryPrompt.BuildTemplateFallback(userText, assistantReply);
        var system = EpisodicMemoryPrompt.SystemInstruction;
        var userPayload = EpisodicMemoryPrompt.BuildUserPayload(userText, assistantReply);

        try
        {
            string? authored = null;
            var authorProvider = chatProvider;

            // BED-185: memory authoring uses Ollama only (Hermes retired).
            if (_inferenceOptions.Enabled)
            {
                authored = await _inference
                    .CompleteAsync(
                        userPayload,
                        system,
                        cancellationToken,
                        EpisodicMemoryPrompt.AuthorMaxTokens)
                    .ConfigureAwait(false);
                authorProvider = "ollama";
            }

            if (string.IsNullOrWhiteSpace(authored))
            {
                _logger.LogWarning(
                    "Memory-author LLM returned empty (provider={Provider}); falling back to template",
                    authorProvider);
                return template;
            }

            var trimmed = authored.Trim();
            RecordSpend(authorProvider, userPayload, system, trimmed);
            return trimmed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Memory-author LLM call failed; falling back to template");
            return template;
        }
    }

    private async Task<ChatCompletionResult> CompleteChatAsync(
        string text,
        string contextPreamble,
        CancellationToken cancellationToken)
    {
        if (!_inferenceOptions.Enabled)
        {
            throw new InvalidOperationException(
                "No LLM client enabled (Inference:Enabled). Hermes retired (BED-185). Refusing stub-as-success.");
        }

        Exception? lastError = null;

        // BED-185: Hermes chat path retired — Ollama only.
        if (_chatOptions.PreferHermes)
        {
            _logger.LogWarning("PreferHermes ignored (BED-185 Hermes retired) — Ollama only");
        }

        try
        {
            var inferenceReply = await _inference
                .CompleteAsync(text, contextPreamble, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(inferenceReply))
            {
                RecordSpend("ollama", text, contextPreamble, inferenceReply);
                return new ChatCompletionResult(inferenceReply.Trim(), "ollama");
            }
        }
        catch (Exception ex)
        {
            lastError = ex;
            _logger.LogDebug(ex, "Ollama inference failed");
        }

        throw lastError ?? new InvalidOperationException(
            "LLM returned empty reply from Ollama (Hermes retired — BED-185).");
    }

    /// <summary>
    /// Combines charter identity anchors, recent episodic memories, and the emotion
    /// preamble into a single deterministic system preamble. Order is
    /// [Identity] → [Memory] → [SoulCore emotion] so identity seeds the persona,
    /// memory grounds continuity, and emotion sits closest to the model's attention.
    /// The combined [Identity]+[Memory] portion is truncated to
    /// <see cref="ContextPreambleCharLimit"/> chars, dropping the oldest memories
    /// first when over budget. The emotion preamble is always appended in full
    /// (it is small and fixed-shape).
    /// </summary>
    internal static string BuildContextPreamble(
        IReadOnlyList<string> identityAnchors,
        IReadOnlyList<string> recentMemories,
        string emotionPreamble)
    {
        ArgumentNullException.ThrowIfNull(emotionPreamble);

        var identityBlock = BuildIdentityBlock(identityAnchors);
        var (memoryBlock, droppedMemoryCount) = BuildMemoryBlock(recentMemories, ContextPreambleCharLimit - identityBlock.Length);

        if (droppedMemoryCount > 0)
        {
            memoryBlock = memoryBlock + $"\n({droppedMemoryCount} older memories truncated)";
        }

        return string.Concat(identityBlock, memoryBlock, emotionPreamble);
    }

    private static string BuildIdentityBlock(IReadOnlyList<string> identityAnchors)
    {
        if (identityAnchors is null || identityAnchors.Count == 0)
            return string.Empty;

        var sb = new StringBuilder(512);
        sb.Append("[Identity]\n");
        var first = true;
        foreach (var anchor in identityAnchors)
        {
            if (string.IsNullOrWhiteSpace(anchor))
                continue;
            if (!first)
                sb.Append('\n');
            sb.Append(anchor.Trim());
            first = false;
        }
        sb.Append("\n\n");
        return sb.ToString();
    }

    /// <summary>
    /// Prefer semantic top-K when embeddings are enabled; fall back to recency on any failure.
    /// </summary>
    private async Task<IReadOnlyList<string>> RecallChatMemoriesAsync(
        string userText,
        CancellationToken cancellationToken)
    {
        if (!_embeddings.IsEnabled)
        {
            return await _memory
                .RecallRecentAsync(ContextMemoryRecallLimit, cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            var queryVec = await _embeddings
                .EmbedAsync(userText, cancellationToken)
                .ConfigureAwait(false);
            if (queryVec.Length == 0)
            {
                _logger.LogDebug("Empty embedding vector; falling back to RecallRecentAsync");
                return await _memory
                    .RecallRecentAsync(ContextMemoryRecallLimit, cancellationToken)
                    .ConfigureAwait(false);
            }

            var similar = await _memory
                .RecallSimilarAsync(queryVec, ContextMemoryRecallLimit, cancellationToken)
                .ConfigureAwait(false);
            if (similar.Count > 0)
                return similar;

            _logger.LogDebug("Semantic recall returned no hits; falling back to RecallRecentAsync");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Semantic recall failed; falling back to RecallRecentAsync");
        }

        return await _memory
            .RecallRecentAsync(ContextMemoryRecallLimit, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the [Memory] section, truncating the oldest memories first to fit
    /// <paramref name="budget"/> chars (block-level, including the header). Returns
    /// the block text and the count of memories dropped due to truncation.
    /// </summary>
    private static (string Block, int DroppedCount) BuildMemoryBlock(
        IReadOnlyList<string> recentMemories,
        int budget)
    {
        if (recentMemories is null || recentMemories.Count == 0)
            return (string.Empty, 0);

        const string header = "[Memory]\n";
        // Memory rows from recall are ordered newest/most-relevant first. We keep
        // the head and drop the tail when truncating.
        var kept = new List<string>(recentMemories.Count);
        var dropped = 0;
        for (var i = 0; i < recentMemories.Count; i++)
        {
            var line = recentMemories[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                dropped++;
                continue;
            }
            kept.Add(line.Trim());
        }

        var droppedCount = dropped;

        // Shrink from the tail (oldest) until the projected block fits the budget.
        while (kept.Count > 0)
        {
            var projected = header + string.Join("\n", kept) + "\n\n";
            if (projected.Length <= budget || budget <= 0)
                break;
            kept.RemoveAt(kept.Count - 1);
            droppedCount++;
        }

        if (kept.Count == 0)
            return (string.Empty, droppedCount);

        return (header + string.Join("\n", kept) + "\n\n", droppedCount);
    }

    private async Task SendEmotionSnapshotAsync(
        WebSocket socket,
        string? correlationId,
        CancellationToken cancellationToken,
        string? note = null)
    {
        IReadOnlyDictionary<string, double> emotion;
        long? revision = null;
        try
        {
            emotion = await _emotion.GetAsync(cancellationToken).ConfigureAwait(false);
            revision = await _emotion.GetRevisionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Emotion snapshot unavailable");
            emotion = new Dictionary<string, double>();
        }

        var fields = EmotionInfluencePrompt.ReadFields(emotion);
        var label = EmotionInfluencePrompt.DescribeLabel(fields.Valence, fields.Arousal);

        await SendFrameAsync(
            socket,
            SoulCoreFrame.Create(
                SoulCoreFrameTypes.EmotionSnapshot,
                new
                {
                    valence = fields.Valence,
                    arousal = fields.Arousal,
                    dominance = fields.Dominance,
                    focus = fields.Focus,
                    label,
                    note,
                    revision
                },
                id: correlationId),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses emotion.correct payload. Ranges match emotion_state CHECK:
    /// valence [-1,1], arousal/dominance/focus [0,1]. Rejects out-of-range (no silent clamp).
    /// </summary>
    private static bool TryParseEmotionCorrect(
        JsonElement? payload,
        out Dictionary<string, double> components,
        out string? note,
        out string? errorMessage)
    {
        components = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        note = null;
        errorMessage = null;

        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
        {
            errorMessage = "emotion.correct payload object required";
            return false;
        }

        var root = payload.Value;
        if (!TryReadRequiredDouble(root, "valence", -1.0, 1.0, out var valence, out errorMessage)
            || !TryReadRequiredDouble(root, "arousal", 0.0, 1.0, out var arousal, out errorMessage)
            || !TryReadRequiredDouble(root, "dominance", 0.0, 1.0, out var dominance, out errorMessage)
            || !TryReadRequiredDouble(root, "focus", 0.0, 1.0, out var focus, out errorMessage))
        {
            return false;
        }

        if (root.TryGetProperty("note", out var noteProp))
        {
            if (noteProp.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                note = null;
            }
            else if (noteProp.ValueKind == JsonValueKind.String)
            {
                note = noteProp.GetString();
            }
            else
            {
                errorMessage = "emotion.correct note must be a string when present";
                return false;
            }
        }

        components["valence"] = valence;
        components["arousal"] = arousal;
        components["dominance"] = dominance;
        components["focus"] = focus;
        return true;
    }

    private static bool TryReadRequiredDouble(
        JsonElement root,
        string name,
        double min,
        double max,
        out double value,
        out string? errorMessage)
    {
        value = 0;
        errorMessage = null;
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Number)
        {
            errorMessage = $"emotion.correct payload.{name} required (number)";
            return false;
        }

        if (!prop.TryGetDouble(out value) || double.IsNaN(value) || double.IsInfinity(value))
        {
            errorMessage = $"emotion.correct payload.{name} must be a finite number";
            return false;
        }

        if (value < min || value > max)
        {
            errorMessage =
                $"emotion.correct payload.{name} out of range [{min.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {max.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
            return false;
        }

        return true;
    }

    private static string BuildStubReply(string text)
    {
        var preview = text.Length <= 80 ? text : text[..80] + "…";
        return $"[stub] SoulCore received: {preview}";
    }

    /// <summary>
    /// Post-hoc cumulative prefixes after full generate (not live token stream).
    /// Kept for assistant-bubble UX until true Ollama/Hermes streaming is ticketed.
    /// </summary>
    private static IEnumerable<string> ChunkReply(string reply)
    {
        if (string.IsNullOrEmpty(reply))
        {
            yield return string.Empty;
            yield break;
        }

        const int chunkSize = 48;
        if (reply.Length <= chunkSize)
        {
            yield return reply;
            yield break;
        }

        for (var i = chunkSize; i < reply.Length; i += chunkSize)
            yield return reply[..i];

        yield return reply;
    }

    private static string? ExtractText(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
            return null;
        if (payload.Value.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
            return textProp.GetString();
        return null;
    }

    /// <summary>
    /// Lightweight keyword-based locomotion intent detection over the ORIGINAL user chat text.
    /// Multi-word phrases are checked first ("turn left", "turn right") before single keywords
    /// so the more specific match wins. Returns null when no locomotion intent is present.
    /// Units are Unreal centimeters (forward=+X, right=+Y, up=+Z).
    /// Optional distance: "3 ft" / "2 feet" / "1 m" / "150 cm" — default step is 200 cm (~6.5 ft)
    /// so motion is visible in a house-scale scene (plain "walk forward" used to be only 50 cm).
    /// </summary>
    private static LocoIntent? DetectLocoIntent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Common typos before keyword match.
        var normalized = text.ToLowerInvariant()
            .Replace("foreward", "forward", StringComparison.Ordinal)
            .Replace("foward", "forward", StringComparison.Ordinal);

        var stepCm = ParseLocoDistanceCm(normalized) ?? 200.0;

        // Multi-word phrases first (more specific than bare "left"/"right").
        if (ContainsAny(normalized, "turn left"))
            return new LocoIntent(0, -stepCm, 0);
        if (ContainsAny(normalized, "turn right"))
            return new LocoIntent(0, stepCm, 0);
        if (ContainsAny(normalized, "go back", "step back", "walk back", "move back", "backward", "backwards"))
            return new LocoIntent(-stepCm, 0, 0);
        if (ContainsAny(normalized, "go forward", "step forward", "walk forward", "move forward"))
            return new LocoIntent(stepCm, 0, 0);

        // Single-word locomotion triggers.
        if (ContainsAny(normalized, "step", "walk", "move", "forward", "go"))
            return new LocoIntent(stepCm, 0, 0);
        if (ContainsAny(normalized, "back"))
            return new LocoIntent(-stepCm, 0, 0);
        if (ContainsAny(normalized, "left"))
            return new LocoIntent(0, -stepCm, 0);
        if (ContainsAny(normalized, "right"))
            return new LocoIntent(0, stepCm, 0);

        return null;
    }

    /// <summary>
    /// Parses an optional distance from chat text into centimeters.
    /// Supports ft/feet/foot, m/meter/meters, cm. Returns null when absent.
    /// </summary>
    private static double? ParseLocoDistanceCm(string normalized)
    {
        // e.g. "3 ft", "3ft", "2.5 feet", "1 m", "150 cm"
        var match = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"(\d+(?:\.\d+)?)\s*(feet|foot|ft|meters|meter|m|cm)\b",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (!match.Success)
            return null;

        if (!double.TryParse(
                match.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount) ||
            amount <= 0)
        {
            return null;
        }

        // Cap absurd chat distances (e.g. "move 99999 ft") to keep avatar in the map.
        const double maxCm = 2000.0;
        var unit = match.Groups[2].Value;
        var cm = unit switch
        {
            "cm" => amount,
            "m" or "meter" or "meters" => amount * 100.0,
            _ => amount * 30.48 // ft / feet / foot
        };
        return Math.Min(cm, maxCm);
    }

    /// <summary>
    /// Lightweight keyword-based animation intent detection over the ORIGINAL user chat text.
    /// Multi-word phrases are checked first ("wave goodbye", "shake head", "thumbs up", "sit down",
    /// "stand up", "point at") before single keywords so the more specific match wins.
    /// Returns the UE animation name string, or null when no animation keyword is present.
    /// </summary>
    private static string? DetectAnimationIntent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = text.ToLowerInvariant();

        // Multi-word phrases first (more specific than bare single words).
        if (ContainsAny(normalized, "wave hello", "wave goodbye", "wave bye"))
            return "wave";
        if (ContainsAny(normalized, "shake head", "shake your head"))
            return "shake_head";
        if (ContainsAny(normalized, "thumbs up", "thumbs-up"))
            return "thumbs_up";
        if (ContainsAny(normalized, "sit down"))
            return "sit";
        if (ContainsAny(normalized, "stand up"))
            return "stand";
        if (ContainsAny(normalized, "point at"))
            return "point";

        // Single-word animation triggers.
        if (ContainsAny(normalized, "wave"))
            return "wave";
        if (ContainsAny(normalized, "nod", "yes"))
            return "nod";
        if (ContainsAny(normalized, "no"))
            return "shake_head";
        if (ContainsAny(normalized, "bow"))
            return "bow";
        if (ContainsAny(normalized, "clap", "applaud"))
            return "clap";
        if (ContainsAny(normalized, "dance"))
            return "dance";
        if (ContainsAny(normalized, "laugh", "giggle"))
            return "laugh";
        if (ContainsAny(normalized, "point"))
            return "point";
        if (ContainsAny(normalized, "jump"))
            return "jump";
        if (ContainsAny(normalized, "sit"))
            return "sit";
        if (ContainsAny(normalized, "stand"))
            return "stand";

        return null;
    }

    /// <summary>
    /// Lightweight keyword-based look-at intent detection over the ORIGINAL user chat text.
    /// Multi-word phrases are checked first ("look at me", "look at player") before single
    /// keywords ("look", "gaze") so the more specific match wins. The UE mapper ignores the
    /// payload and always sends the fixed <c>look_at_player</c> command, so this returns only
    /// a boolean intent flag. Returns false when no look keyword is present.
    /// </summary>
    private static bool DetectLookIntent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.ToLowerInvariant();

        // Multi-word phrases first (more specific than bare "look"/"gaze").
        if (ContainsAny(normalized, "look at me", "look at player", "look at", "face me",
            "turn to me", "watch me", "see me"))
            return true;

        // Single-word look/gaze triggers.
        if (ContainsAny(normalized, "look", "gaze"))
            return true;

        return false;
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (haystack.Contains(needle, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private sealed record LocoIntent(double Forward, double Right, double Up);

    private static async Task SendFrameAsync(WebSocket socket, SoulCoreFrame frame, CancellationToken cancellationToken)
    {
        if (socket.State != WebSocketState.Open)
            return;

        var bytes = Encoding.UTF8.GetBytes(frame.ToJson());
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Records token usage into the SpendMeter after a successful inference call. Token counts
    /// are not yet exposed by the IInferenceClient/IHermesClient response shape, so this estimates
    /// counts from the input (prompt + preamble) and output (reply) text using chars/4 as a rough
    /// proxy. The wiring is in place for when real counts become available. SpendMeter failures
    /// are swallowed so they never break the chat path.
    /// </summary>
    private void RecordSpend(string provider, string prompt, string? systemPreamble, string reply)
    {
        try
        {
            var inputChars = (prompt?.Length ?? 0) + (systemPreamble?.Length ?? 0);
            var outputChars = reply?.Length ?? 0;
            var tokensIn = EstimateTokens(inputChars);
            var tokensOut = EstimateTokens(outputChars);
            _spendMeter.RecordUsage(provider, tokensIn, tokensOut);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SpendMeter.RecordUsage failed for provider {Provider}", provider);
        }
    }

    /// <summary>Rough token estimate: chars / 4 (industry rule-of-thumb).</summary>
    private static long EstimateTokens(int charCount)
    {
        if (charCount <= 0)
            return 0;
        return (long)Math.Ceiling(charCount / 4.0);
    }

    private sealed record ChatCompletionResult(string Text, string Provider);

    /// <summary>
    /// Tool-loop inference path (BED-128 + BED-158). Builds the agent-loop
    /// messages[] as system preamble + prior session history + current user
    /// text, routes through <c>CompleteWithToolsAsync</c>, then appends the
    /// completed turn (user + tool trace + assistant) to the session store so
    /// later pronouns can resolve prior task/workflow IDs.
    /// </summary>
    private async Task<ChatCompletionResult> CompleteChatWithToolsAsync(
        string text,
        string contextPreamble,
        string historySessionId,
        HashSet<string> dispatchedToolNames,
        List<ToolTraceEntry> toolTrace,
        CancellationToken cancellationToken)
    {
        if (!_inferenceOptions.Enabled)
        {
            throw new InvalidOperationException(
                "No LLM client enabled (Inference:Enabled). Hermes retired (BED-185). Refusing stub-as-success.");
        }

        // Build the agent-loop messages[]: system preamble + prior turns + user.
        // The tool-loop clients append assistant + tool turns internally for
        // THIS turn only; we persist a compact trace afterward (BED-158).
        var prior = _sessionHistory.GetMessages(historySessionId);
        var messages = new List<ChatMessage>(2 + prior.Count);
        if (!string.IsNullOrWhiteSpace(contextPreamble))
            messages.Add(new ChatMessage { Role = "system", Content = contextPreamble.Trim() });
        messages.AddRange(prior);
        messages.Add(new ChatMessage { Role = "user", Content = text });

        _logger.LogDebug(
            "Chat tool-loop history: session={SessionId} priorMessages={PriorCount}",
            historySessionId,
            prior.Count);

        // Tools from the registry. Empty registry → empty tools[] → model
        // returns text in one round-trip (loop behaves like single-shot).
        var tools = _toolRegistry.GetDefinitions();

        // Wrap the registry in a tracking decorator so we can record which
        // tool names fired during the loop (Strategy A) AND capture tool
        // result Content for session history (BED-158). Call-scoped.
        var trackingRegistry = new TrackingToolRegistry(_toolRegistry, dispatchedToolNames, toolTrace);

        Exception? lastError = null;
        string? replyText = null;
        string? provider = null;

        // BED-162 / BED-167: force tool_choice on high-confidence NL intents
        // (Ollama path — including PreferHermes Avenue B which also uses Ollama).
        // MT4 status wins over workflow when both somehow match (ISSUE-003:
        // models escape "status" phrasing to task_create/task_get).
        ToolLoopOptions? ollamaLoopOptions = null;
        if (Mt4ToolIntent.TryMatch(text, out var mt4Intent))
        {
            ollamaLoopOptions = new ToolLoopOptions { ForceToolName = mt4Intent.ToolName };
            _logger.LogInformation(
                "MT4 NL intent matched: intent={Intent} forceTool={Tool}",
                mt4Intent.Intent, mt4Intent.ToolName);
        }
        else if (ChiefArchitectToolIntent.TryMatch(text, out var caIntent))
        {
            ollamaLoopOptions = new ToolLoopOptions { ForceToolName = caIntent.ToolName };
            _logger.LogInformation(
                "ChiefArchitect NL intent matched: intent={Intent} forceTool={Tool}",
                caIntent.Intent, caIntent.ToolName);
        }
        else if (DesktopToolIntent.TryMatch(text, out var desktopIntent))
        {
            ollamaLoopOptions = new ToolLoopOptions { ForceToolName = desktopIntent.ToolName };
            _logger.LogInformation(
                "Desktop NL intent matched: intent={Intent} forceTool={Tool}",
                desktopIntent.Intent, desktopIntent.ToolName);
        }
        else if (HomeBodyToolIntent.TryMatch(text, out var homeIntent))
        {
            ollamaLoopOptions = new ToolLoopOptions { ForceToolName = homeIntent.ToolName };
            _logger.LogInformation(
                "HomeBody NL intent matched: intent={Intent} forceTool={Tool}",
                homeIntent.Intent, homeIntent.ToolName);
        }
        else if (WorkflowToolIntent.TryMatch(text, out var intent))
        {
            ollamaLoopOptions = new ToolLoopOptions { ForceToolName = intent.ToolName };
            _logger.LogInformation(
                "Workflow NL intent matched: intent={Intent} forceTool={Tool}",
                intent.Intent, intent.ToolName);
        }

        // BED-185: Hermes / PreferHermes MCP preflight retired — Ollama tool-loop only.
        if (_chatOptions.PreferHermes)
        {
            _logger.LogWarning(
                "PreferHermes ignored (BED-185 Hermes retired) — Ollama tool-loop only");
        }

        if (replyText is null && _inferenceOptions.Enabled)
        {
            try
            {
                var reply = await _inference
                    .CompleteWithToolsAsync(messages, tools, trackingRegistry, cancellationToken, ollamaLoopOptions)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(reply))
                {
                    replyText = reply.Trim();
                    provider = "ollama";
                    RecordSpend("ollama", text, contextPreamble, replyText);
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogDebug(ex, "Ollama tool-loop failed");
            }
        }

        if (replyText is null || provider is null)
        {
            throw lastError ?? new InvalidOperationException(
                "LLM tool-loop returned empty reply from Ollama (Hermes retired — BED-185).");
        }

        // Persist this turn (user + tool trace + final assistant) for the next send.
        AppendToolLoopTurn(historySessionId, text, toolTrace, replyText);

        return new ChatCompletionResult(replyText, provider);
    }

    /// <summary>
    /// Resolves the history key: payload <c>sessionId</c> when present, else
    /// <c>ws:{connectionSessionId}</c> so same-socket multi-turn still works.
    /// </summary>
    private static string ResolveHistorySessionId(JsonElement? payload, Guid connectionSessionId)
    {
        var client = ExtractSessionId(payload);
        if (!string.IsNullOrWhiteSpace(client))
            return client.Trim();
        return "ws:" + connectionSessionId.ToString("N");
    }

    private static string? ExtractSessionId(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
            return null;
        if (payload.Value.TryGetProperty("sessionId", out var sid) && sid.ValueKind == JsonValueKind.String)
            return sid.GetString();
        return null;
    }

    private void AppendPlainTurn(string historySessionId, string userText, string assistantText)
    {
        _sessionHistory.AppendTurn(
            historySessionId,
            new[]
            {
                new ChatMessage { Role = "user", Content = userText },
                new ChatMessage { Role = "assistant", Content = assistantText }
            });
    }

    /// <summary>
    /// Stores user + synthetic assistant tool_calls + role:tool results + final
    /// assistant text. Tool args/results carry task/workflow IDs so later turns
    /// can resolve pronouns without re-asking the user.
    /// </summary>
    private void AppendToolLoopTurn(
        string historySessionId,
        string userText,
        IReadOnlyList<ToolTraceEntry> toolTrace,
        string assistantText)
    {
        var turn = new List<ChatMessage>(2 + toolTrace.Count * 2);
        turn.Add(new ChatMessage { Role = "user", Content = userText });

        if (toolTrace.Count > 0)
        {
            var calls = new List<ChatToolCall>(toolTrace.Count);
            foreach (var t in toolTrace)
            {
                calls.Add(new ChatToolCall
                {
                    Function = new ChatFunctionCall
                    {
                        Name = t.Name,
                        Arguments = t.Arguments
                    }
                });
            }

            turn.Add(new ChatMessage
            {
                Role = "assistant",
                Content = string.Empty,
                ToolCalls = calls
            });

            foreach (var t in toolTrace)
            {
                turn.Add(new ChatMessage
                {
                    Role = "tool",
                    Name = t.Name,
                    Content = t.Content ?? string.Empty
                });
            }
        }

        turn.Add(new ChatMessage { Role = "assistant", Content = assistantText });
        _sessionHistory.AppendTurn(historySessionId, turn);
    }

    private sealed record ToolTraceEntry(string Name, JsonElement? Arguments, string? Content);

    /// <summary>
    /// Verb classes the keyword detectors map to. Used by Strategy A to
    /// suppress the keyword fallback when the model already called a tool
    /// in the same class this turn. Names are the canonical tool names
    /// proposed in PROP-AGENT-LOOP-01 Phase B (BED-131/132/133) — the mapping
    /// is tolerant of variants (e.g. <c>walk_forward</c> vs <c>move_to</c>).
    /// </summary>
    private enum ToolVerbClass
    {
        Loco,
        Animation,
        Look
    }

    /// <summary>
    /// Strategy A: returns true when any tool dispatched this turn maps to
    /// the given verb class. The keyword fallback for that class is then
    /// skipped so Victoria does not double-act (e.g. model calls
    /// <c>play_animation</c> + user text contains "wave" → only the tool
    /// runs). The mapping is intentionally generous (prefix/contains) so
    /// future tool variants (<c>walk_forward</c>, <c>move_to</c>,
    /// <c>look_at</c>) are covered without code changes.
    /// </summary>
    private static bool ToolClassFiredThisTurn(HashSet<string> dispatchedToolNames, ToolVerbClass verbClass)
    {
        if (dispatchedToolNames is null || dispatchedToolNames.Count == 0)
            return false;

        foreach (var name in dispatchedToolNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var n = name.ToLowerInvariant();
            switch (verbClass)
            {
                case ToolVerbClass.Loco:
                    if (n.Contains("move", StringComparison.Ordinal)
                        || n.Contains("walk", StringComparison.Ordinal)
                        || n.Contains("loco", StringComparison.Ordinal)
                        || n.Contains("go_to", StringComparison.Ordinal))
                        return true;
                    break;
                case ToolVerbClass.Animation:
                    if (n.Contains("animation", StringComparison.Ordinal)
                        || n.Contains("animate", StringComparison.Ordinal)
                        || n.Contains("wave", StringComparison.Ordinal)
                        || n.Contains("play_anim", StringComparison.Ordinal))
                        return true;
                    break;
                case ToolVerbClass.Look:
                    if (n.Contains("look", StringComparison.Ordinal)
                        || n.Contains("gaze", StringComparison.Ordinal)
                        || n.Contains("face", StringComparison.Ordinal))
                        return true;
                    break;
            }
        }
        return false;
    }

    /// <summary>
    /// Call-scoped decorator over <see cref="IToolRegistry"/> that records
    /// every dispatched tool name (Strategy A) and tool result Content
    /// (BED-158 session history) without changing the inference client
    /// interfaces (which return only the final text, not the tool-call trace).
    /// </summary>
    private sealed class TrackingToolRegistry : IToolRegistry
    {
        private readonly IToolRegistry _inner;
        private readonly HashSet<string> _dispatched;
        private readonly List<ToolTraceEntry> _trace;

        public TrackingToolRegistry(
            IToolRegistry inner,
            HashSet<string> dispatched,
            List<ToolTraceEntry> trace)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _dispatched = dispatched ?? throw new ArgumentNullException(nameof(dispatched));
            _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        public IReadOnlyList<ToolDefinition> GetDefinitions() => _inner.GetDefinitions();

        public async Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(name))
                _dispatched.Add(name);

            var result = await _inner.ExecuteAsync(name, args, ct).ConfigureAwait(false);

            JsonElement? clonedArgs = null;
            if (args.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
                clonedArgs = args.Clone();

            _trace.Add(new ToolTraceEntry(
                name ?? string.Empty,
                clonedArgs,
                result.Content));

            return result;
        }
    }
}
