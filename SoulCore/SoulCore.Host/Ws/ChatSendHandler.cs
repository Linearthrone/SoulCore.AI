using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Safety;
using SoulCore.Inference;
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
        IPresenceActivityHub? presenceActivity = null)
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

        string reply;
        string provider;
        var usedStub = false;
        var dispatchedToolNames = new HashSet<string>(StringComparer.Ordinal);
        var toolTrace = new List<ToolTraceEntry>();

        try
        {
            if (useToolLoop)
            {
                var loopResult = await CompleteChatWithToolsAsync(
                        modelUserText,
                        chatContext.Preamble,
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

        foreach (var partial in ChunkReply(reply))
        {
            await WsFrameSender.SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.ChatDelta,
                    new { text = partial, stub = usedStub, provider },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
        }

        await WsFrameSender.SendFrameAsync(
            socket,
            SoulCoreFrame.Create(
                SoulCoreFrameTypes.ChatDone,
                new { text = reply, stub = usedStub, provider },
                id: frame.Id),
            cancellationToken).ConfigureAwait(false);

        _presenceActivity?.NoteChat("assistant");

        if (!usedStub)
            await WriteEpisodicAfterChatAsync(modelUserText, reply, provider, cancellationToken).ConfigureAwait(false);

        await _postEffects.ApplyAsync(text, reply, dispatchedToolNames, cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken)
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
        var trackingRegistry = new TrackingToolRegistry(_toolRegistry, dispatchedToolNames, toolTrace);

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

        if (DesktopToolIntent.TryMatch(text, out var desktopIntent))
        {
            _logger.LogInformation(
                "Desktop NL intent matched: intent={Intent} forceTool={Tool}",
                desktopIntent.Intent, desktopIntent.ToolName);
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
