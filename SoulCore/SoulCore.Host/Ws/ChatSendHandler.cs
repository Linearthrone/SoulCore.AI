using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Safety;
using SoulCore.Host.Loop;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Inference.Presence;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Inference.Tools.Email;
using SoulCore.Inference.Tools.Body;
using SoulCore.Inference.Tools.ChiefArchitect;
using SoulCore.Inference.Tools.Trading;
using SoulCore.Inference.Tools.Workflow;
using SoulCore.Memory;
using SoulCore.Protocol;

namespace SoulCore.Host.Ws;

/// <summary>
/// Orchestrates <c>chat.send</c>: context build, inference (tool-loop or single-shot),
/// delta/done frames, episodic write, and post-chat effects delegation.
/// </summary>
public sealed class ChatSendHandler
{
    private readonly IInferenceClient _inference;
    private readonly IMemoryStore _memory;
    private readonly IEmbeddingClient _embeddings;
    private readonly IChatSessionHistoryStore _sessionHistory;
    private readonly IToolRegistry _toolRegistry;
    private readonly IChatContextBuilder _contextBuilder;
    private readonly EmotionSnapshotSender _emotionSnapshot;
    private readonly ChatPostEffectsHandler _postEffects;
    private readonly SpendMeter _spendMeter;
    private readonly ChatWsOptions _chatOptions;
    private readonly InferenceOptions _inferenceOptions;
    private readonly IToolsAccessSettings _toolsAccess;
    private readonly IPresenceActivityHub? _presenceActivity;
    private readonly IChatTranscriptStore? _transcript;
    private readonly IAssignedWorkStore? _assigned;
    private readonly IVictoriaTaskStore? _tasks;
    private readonly IOperatorConsentGate? _consent;
    private readonly WorkTurnGate? _turnGate;
    private readonly IInferenceLease? _lease;
    private readonly ILeisureSession? _leisure;
    private readonly ILogger<ChatSendHandler> _logger;

    public ChatSendHandler(
        IInferenceClient inference,
        IMemoryStore memory,
        IEmbeddingClient embeddings,
        IChatSessionHistoryStore sessionHistory,
        IToolRegistry toolRegistry,
        IChatContextBuilder contextBuilder,
        EmotionSnapshotSender emotionSnapshot,
        ChatPostEffectsHandler postEffects,
        SpendMeter spendMeter,
        IOptions<ChatWsOptions> chatOptions,
        IOptions<InferenceOptions> inferenceOptions,
        IToolsAccessSettings toolsAccess,
        ILogger<ChatSendHandler> logger,
        IPresenceActivityHub? presenceActivity = null,
        IChatTranscriptStore? transcript = null,
        IAssignedWorkStore? assigned = null,
        WorkTurnGate? turnGate = null,
        IVictoriaTaskStore? tasks = null,
        IOperatorConsentGate? consent = null,
        IInferenceLease? lease = null,
        ILeisureSession? leisure = null)
    {
        _inference = inference ?? throw new ArgumentNullException(nameof(inference));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _sessionHistory = sessionHistory ?? throw new ArgumentNullException(nameof(sessionHistory));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _emotionSnapshot = emotionSnapshot ?? throw new ArgumentNullException(nameof(emotionSnapshot));
        _postEffects = postEffects ?? throw new ArgumentNullException(nameof(postEffects));
        _spendMeter = spendMeter ?? throw new ArgumentNullException(nameof(spendMeter));
        _chatOptions = chatOptions?.Value ?? throw new ArgumentNullException(nameof(chatOptions));
        _inferenceOptions = inferenceOptions?.Value ?? throw new ArgumentNullException(nameof(inferenceOptions));
        _toolsAccess = toolsAccess ?? throw new ArgumentNullException(nameof(toolsAccess));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _presenceActivity = presenceActivity;
        _transcript = transcript;
        _assigned = assigned;
        _turnGate = turnGate;
        _tasks = tasks;
        _consent = consent;
        _lease = lease;
        _leisure = leisure;
    }

    public async Task HandleAsync(
        WebSocket socket,
        SoulCoreFrame frame,
        Guid connectionSessionId,
        CancellationToken cancellationToken)
    {
        var text = ExtractText(frame.Payload);
        if (string.IsNullOrWhiteSpace(text))
        {
            await WsFrameSender.SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new { code = "chat.empty", message = "chat.send payload.text required" },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var holdGate = _turnGate is not null;
        if (holdGate)
            await _turnGate!.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        // PROP-17.3: human turn aborts in-flight R&R before the next preamble.
        _lease?.Abort("human-chat");
        _leisure?.Clear();

        _consent?.TryGrantFromUserText(text);
        await RememberAssignmentAsync(text, cancellationToken).ConfigureAwait(false);

        var quotedText = ExtractQuotedText(frame.Payload);
        var modelUserText = QuotedChatText.BuildUserText(text, quotedText);

        _presenceActivity?.NoteChat("user");

        var historySessionId = ResolveHistorySessionId(frame.Payload, connectionSessionId);

        await _emotionSnapshot.SendAsync(socket, frame.Id, cancellationToken).ConfigureAwait(false);

        var useToolLoop = _chatOptions.UseToolLoop;
        var chatContext = await _contextBuilder
            .BuildAsync(
                modelUserText,
                useToolLoop,
                _toolsAccess.DesktopTargetWindowTitle,
                cancellationToken)
            .ConfigureAwait(false);

        var spendSummary = _spendMeter.GetSummary();
        if (spendSummary.CapExceeded)
        {
            _logger.LogWarning(
                "Spend cap exceeded; refusing chat inference. cost={Cost}/{Cap} tokens={Tokens}/{TokenCap}",
                spendSummary.EstimatedCost,
                spendSummary.MonthlyCap,
                spendSummary.TotalTokens,
                spendSummary.MonthlyTokenCap);
            await WsFrameSender.SendFrameAsync(
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

        string reply = "";
        string provider = "ollama";
        var usedStub = false;
        var dispatchedToolNames = new HashSet<string>(StringComparer.Ordinal);
        var toolTrace = new List<ToolTraceEntry>();
        var pausedForBudget = false;

        try
        {
            if (useToolLoop)
            {
                var passText = modelUserText;
                var extra = 0;
                while (true)
                {
                    _presenceActivity?.NoteTool("Thinking");
                    await PushActivityAsync(socket, frame.Id, "Thinking", "thinking", cancellationToken)
                        .ConfigureAwait(false);

                    var passTools = new HashSet<string>(StringComparer.Ordinal);
                    var passTrace = new List<ToolTraceEntry>();
                    var loopResult = await CompleteChatWithToolsAsync(
                            passText,
                            chatContext.Preamble,
                            historySessionId,
                            passTools,
                            passTrace,
                            cancellationToken,
                            async (toolName, ct) =>
                            {
                                var phrase = WorkContinuation.PhraseForTool(toolName);
                                _presenceActivity?.NoteTool(phrase);
                                await PushActivityAsync(socket, frame.Id, phrase, "working", ct)
                                    .ConfigureAwait(false);
                            })
                        .ConfigureAwait(false);
                    reply = loopResult.Text;
                    provider = loopResult.Provider;
                    foreach (var name in passTools)
                        dispatchedToolNames.Add(name);
                    toolTrace.AddRange(passTrace);

                    var usedWorkTools = passTools.Any(WorkContinuation.IsWorkTool);
                    var keep = WorkContinuation.ShouldKeepGoing(text, reply, usedWorkTools);
                    if (!keep || extra >= WorkContinuation.MaxExtraPasses)
                    {
                        pausedForBudget = keep && extra >= WorkContinuation.MaxExtraPasses;
                        break;
                    }

                    await SendReplyAsync(
                            socket,
                            frame.Id,
                            WorkContinuation.VisibleText(reply, continuing: true),
                            provider,
                            usedStub: false,
                            continuing: true,
                            cancellationToken)
                        .ConfigureAwait(false);
                    extra++;
                    passText = WorkContinuation.Nudge;
                    _logger.LogInformation(
                        "Work continuation pass {Pass} after tools={Tools}",
                        extra,
                        string.Join(',', passTools));
                }
            }
            else
            {
                var result = await CompleteChatAsync(modelUserText, chatContext.Preamble, cancellationToken)
                    .ConfigureAwait(false);
                reply = result.Text;
                provider = result.Provider;
                AppendPlainTurn(historySessionId, modelUserText, reply);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat model path failed; stub={Stub} useToolLoop={UseToolLoop}", _chatOptions.StubWhenModelDown, useToolLoop);
            if (!_chatOptions.StubWhenModelDown)
            {
                await WsFrameSender.SendFrameAsync(
                    socket,
                    SoulCoreFrame.Create(
                        SoulCoreFrameTypes.Error,
                        new
                        {
                            code = "chat.model_down",
                            message = string.IsNullOrWhiteSpace(ex.Message)
                                ? "LLM unreachable (Ollama)."
                                : ex.Message
                        },
                        id: frame.Id),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            usedStub = true;
            provider = "stub";
            reply = BuildStubReply(text);
            AppendPlainTurn(historySessionId, modelUserText, reply);
        }

        await NoteAssignmentOutcomeAsync(reply, cancellationToken).ConfigureAwait(false);
        var stillOpen = _assigned?.RunningId is > 0
            && string.Equals(_assigned.Current?.Status, "in_progress", StringComparison.OrdinalIgnoreCase);
        var visible = WorkContinuation.VisibleText(reply, continuing: stillOpen);
        await SendReplyAsync(socket, frame.Id, visible, provider, usedStub, continuing: stillOpen, cancellationToken)
            .ConfigureAwait(false);

        if (stillOpen)
            _presenceActivity?.NoteTool(WorkContinuation.ActivityFor(_assigned!.Current!.Text));
        else if (pausedForBudget || WorkContinuation.HitToolCap(reply))
            _presenceActivity?.NoteTool(WorkContinuation.PausedActivity);
        else if (WorkContinuation.IsHandoff(reply) || WorkContinuation.IsUserStop(text)
                 || string.Equals(_assigned?.Current?.Status, "blocked", StringComparison.OrdinalIgnoreCase))
            _presenceActivity?.NoteTool(WorkContinuation.WaitingLine);
        else
            _presenceActivity?.NoteChat("assistant");

        var activityState = stillOpen ? "working" : pausedForBudget ? "paused" : "idle";
        await PushActivityAsync(
                socket,
                frame.Id,
                _presenceActivity?.GetSnapshot().Phrase ?? WorkContinuation.NoAssignmentPhrase,
                activityState,
                cancellationToken)
            .ConfigureAwait(false);

        await WriteTranscriptAfterChatAsync(historySessionId, text, reply, frame.Id, cancellationToken)
            .ConfigureAwait(false);

        if (!usedStub)
            await WriteEpisodicAfterChatAsync(modelUserText, reply, provider, cancellationToken).ConfigureAwait(false);

        await _postEffects.ApplyAsync(text, reply, dispatchedToolNames, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (holdGate)
                _turnGate!.Release();
        }
    }

    private async Task RememberAssignmentAsync(string userText, CancellationToken cancellationToken)
    {
        if (_assigned is null)
            return;
        if (WorkContinuation.IsUserStop(userText))
        {
            if (_assigned.RunningId is { } rid && _tasks is not null)
                await _tasks.SuspendAsync(rid, "Stopped by Kayleigh.", cancellationToken).ConfigureAwait(false);
            _assigned.Clear();
            return;
        }

        if (!WorkContinuation.LooksLikeAssignment(userText) || _tasks is null)
            return;

        var body = userText.Trim();
        var title = body.Length > 80 ? body[..77] + "..." : body;
        var surface = InferSurface(body);
        var id = await _tasks.CreateAsync(
            title, body, "high", surface, ready: true, status: "in_progress", notes: null, cancellationToken)
            .ConfigureAwait(false);
        var row = await _tasks.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (row is not null)
            _assigned.Project(row);
        else
            _assigned.SetRunning(id);
    }

    private async Task NoteAssignmentOutcomeAsync(string reply, CancellationToken cancellationToken)
    {
        if (_assigned?.RunningId is not { } id || _tasks is null)
            return;
        if (!WorkContinuation.NeedsKayleigh(reply))
            return;
        await _tasks.UpdateStatusAsync(id, "blocked", cancellationToken).ConfigureAwait(false);
        _assigned.Clear();
    }

    private static string InferSurface(string text)
    {
        var t = text.ToLowerInvariant();
        if (t.Contains("maestro", StringComparison.Ordinal))
            return "maestro";
        if (t.Contains("browser", StringComparison.Ordinal) || t.Contains("firefox", StringComparison.Ordinal)
            || t.Contains("http", StringComparison.Ordinal))
            return "browser";
        if (t.Contains("vm", StringComparison.Ordinal) || t.Contains("guest", StringComparison.Ordinal)
            || t.Contains("sandbox", StringComparison.Ordinal))
            return "vm";
        return "local";
    }

    /// <summary>
    /// PROP-3 Wave 1: append the turn to the durable transcript so other clients can backfill it.
    /// </summary>
    /// <remarks>
    /// Stub replies are recorded, unlike the episodic write above, and the difference is
    /// deliberate: the transcript reproduces the conversation the operator actually saw on
    /// screen, while episodic memory is what Victoria genuinely experienced. Dropping stubs here
    /// would hydrate a thread of user messages with no answers.
    /// <para>
    /// Best-effort — a transcript failure must never fail a chat turn the operator already has.
    /// </para>
    /// </remarks>
    private async Task WriteTranscriptAfterChatAsync(
        string conversationId,
        string userText,
        string reply,
        string? frameId,
        CancellationToken cancellationToken)
    {
        if (_transcript is null)
            return;

        try
        {
            // The assistant row carries the chat.done frame id so a client can recognise a
            // hydrated row as one it already rendered live. The user row needs a distinct id
            // because both frames share one inbound id and frame_id is unique.
            var assistantFrameId = string.IsNullOrWhiteSpace(frameId) ? null : frameId;
            var userFrameId = assistantFrameId is null ? null : assistantFrameId + ":user";

            await _transcript
                .AppendAsync(conversationId, "user", userText, "desk", userFrameId, null, cancellationToken)
                .ConfigureAwait(false);
            await _transcript
                .AppendAsync(conversationId, "assistant", reply, "desk", assistantFrameId, null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Durable transcript append failed for conversation {ConversationId}", conversationId);
        }
    }

    private async Task WriteEpisodicAfterChatAsync(
        string modelUserText,
        string reply,
        string provider,
        CancellationToken cancellationToken)
    {
        try
        {
            var episode = await AuthorChatEpisodicAsync(modelUserText, reply, provider, cancellationToken)
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
            _logger.LogWarning(ex, "Memory-author LLM call failed; falling back to template");
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
                "No LLM client enabled (Inference:Enabled). Refusing stub-as-success.");
        }

        Exception? lastError = null;

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
            "LLM returned empty reply from Ollama.");
    }

    private async Task<ChatCompletionResult> CompleteChatWithToolsAsync(
        string text,
        string contextPreamble,
        string historySessionId,
        HashSet<string> dispatchedToolNames,
        List<ToolTraceEntry> toolTrace,
        CancellationToken cancellationToken,
        Func<string, CancellationToken, Task>? beforeTool = null)
    {
        if (!_inferenceOptions.Enabled)
        {
            throw new InvalidOperationException(
                "No LLM client enabled (Inference:Enabled). Refusing stub-as-success.");
        }

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

        var tools = _toolRegistry.GetDefinitions();
        var trackingRegistry = new TrackingToolRegistry(
            _toolRegistry, dispatchedToolNames, toolTrace, beforeTool);

        Exception? lastError = null;
        string? replyText = null;
        string? provider = null;

        ToolLoopOptions? ollamaLoopOptions = ResolveForcedTool(text);

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
                "LLM tool-loop returned empty reply from Ollama.");
        }

        AppendToolLoopTurn(historySessionId, text, toolTrace, replyText);

        return new ChatCompletionResult(replyText, provider);
    }

    private ToolLoopOptions? ResolveForcedTool(string text)
    {
        if (Mt4ToolIntent.TryMatch(text, out var mt4Intent))
        {
            _logger.LogInformation(
                "MT4 NL intent matched: intent={Intent} forceTool={Tool}",
                mt4Intent.Intent, mt4Intent.ToolName);
            return new ToolLoopOptions { ForceToolName = mt4Intent.ToolName };
        }

        if (EmailToolIntent.TryMatch(text, out var emailIntent))
        {
            _logger.LogInformation(
                "Email NL intent matched: intent={Intent} forceTool={Tool} account={Account}",
                emailIntent.Intent, emailIntent.ToolName, emailIntent.AccountId ?? "-");
            return new ToolLoopOptions { ForceToolName = emailIntent.ToolName };
        }

        if (ChiefArchitectToolIntent.TryMatch(text, out var caIntent))
        {
            _logger.LogInformation(
                "ChiefArchitect NL intent matched: intent={Intent} forceTool={Tool}",
                caIntent.Intent, caIntent.ToolName);
            return new ToolLoopOptions { ForceToolName = caIntent.ToolName };
        }

        if (DesktopToolIntent.TryMatch(text, _toolsAccess.BrowserBackend, out var desktopIntent))
        {
            _logger.LogInformation(
                "Desktop NL intent matched: intent={Intent} forceTool={Tool} browserBackend={Backend}",
                desktopIntent.Intent, desktopIntent.ToolName, _toolsAccess.BrowserBackend);
            return new ToolLoopOptions { ForceToolName = desktopIntent.ToolName };
        }

        if (HomeBodyToolIntent.TryMatch(text, out var homeIntent))
        {
            _logger.LogInformation(
                "HomeBody NL intent matched: intent={Intent} forceTool={Tool}",
                homeIntent.Intent, homeIntent.ToolName);
            return new ToolLoopOptions { ForceToolName = homeIntent.ToolName };
        }

        if (WorkflowToolIntent.TryMatch(text, out var intent))
        {
            _logger.LogInformation(
                "Workflow NL intent matched: intent={Intent} forceTool={Tool}",
                intent.Intent, intent.ToolName);
            return new ToolLoopOptions { ForceToolName = intent.ToolName };
        }

        return null;
    }

    private void RecordSpend(string provider, string prompt, string? systemPreamble, string reply)
    {
        try
        {
            var inputChars = (prompt?.Length ?? 0) + (systemPreamble?.Length ?? 0);
            var outputChars = reply?.Length ?? 0;
            _spendMeter.RecordUsage(provider, EstimateTokens(inputChars), EstimateTokens(outputChars));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SpendMeter.RecordUsage failed for provider {Provider}", provider);
        }
    }

    private static long EstimateTokens(int charCount) =>
        charCount <= 0 ? 0 : (long)Math.Ceiling(charCount / 4.0);

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

    private static string? ExtractText(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
            return null;
        if (payload.Value.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
            return textProp.GetString();
        return null;
    }

    private static string? ExtractQuotedText(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
            return null;
        if (!payload.Value.TryGetProperty("quotedText", out var q) || q.ValueKind != JsonValueKind.String)
            return null;
        return QuotedChatText.NormalizeQuoted(q.GetString());
    }

    private static string BuildStubReply(string text)
    {
        var preview = text.Length <= 80 ? text : text[..80] + "…";
        return $"[stub] SoulCore received: {preview}";
    }

    private async Task PushActivityAsync(
        WebSocket socket,
        string? correlationId,
        string phrase,
        string state,
        CancellationToken cancellationToken)
    {
        try
        {
            await WsFrameSender.SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.PresenceStatus,
                    new { alive = true, warm = true, phase = 1, activity = phrase, state },
                    id: correlationId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "presence activity push failed");
        }
    }

    private static async Task SendReplyAsync(
        WebSocket socket,
        string? correlationId,
        string reply,
        string provider,
        bool usedStub,
        bool continuing,
        CancellationToken cancellationToken)
    {
        foreach (var partial in ChunkReply(reply))
        {
            await WsFrameSender.SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.ChatDelta,
                    new { text = partial, stub = usedStub, provider, continuing },
                    id: correlationId),
                cancellationToken).ConfigureAwait(false);
        }

        await WsFrameSender.SendFrameAsync(
            socket,
            SoulCoreFrame.Create(
                SoulCoreFrameTypes.ChatDone,
                new { text = reply, stub = usedStub, provider, continuing },
                id: correlationId),
            cancellationToken).ConfigureAwait(false);
    }

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

    private sealed record ChatCompletionResult(string Text, string Provider);

    internal sealed record ToolTraceEntry(string Name, JsonElement? Arguments, string? Content);

    private sealed class TrackingToolRegistry : IToolRegistry
    {
        private readonly IToolRegistry _inner;
        private readonly HashSet<string> _dispatched;
        private readonly List<ToolTraceEntry> _trace;

        private readonly Func<string, CancellationToken, Task>? _beforeTool;

        public TrackingToolRegistry(
            IToolRegistry inner,
            HashSet<string> dispatched,
            List<ToolTraceEntry> trace,
            Func<string, CancellationToken, Task>? beforeTool = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _dispatched = dispatched ?? throw new ArgumentNullException(nameof(dispatched));
            _trace = trace ?? throw new ArgumentNullException(nameof(trace));
            _beforeTool = beforeTool;
        }

        public IReadOnlyList<ToolDefinition> GetDefinitions() => _inner.GetDefinitions();

        public async Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(name))
                _dispatched.Add(name);

            if (_beforeTool is not null && !string.IsNullOrWhiteSpace(name))
                await _beforeTool(name, ct).ConfigureAwait(false);

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
