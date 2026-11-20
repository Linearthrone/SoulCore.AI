using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Core;
using SoulCore.Host.Ws;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Presence;
using SoulCore.Inference.Tooling;
using SoulCore.Memory;
using SoulCore.Protocol;

namespace SoulCore.Host.Companion;

/// <summary>
/// PROP-1.2 / PROP-20.7: tablet gateway SMS/MMS → One Thread (<c>presence-local</c>).
/// Filtered tool loop (PROP-20.5 allow list); outbound = short answer + public URL within 480 chars;
/// long findings → research paper (not private journals).
/// </summary>
public sealed class SmsInboundService : ISmsInboundService
{
    public const string Channel = "sms";

    /// <summary>PROP-18.1: max dated episodic lines injected into SMS [Memory].</summary>
    public const int SmsMemoryRecallLimit = 6;

    private readonly SmsOptions _sms;
    private readonly InferenceOptions _inference;
    private readonly ChatWsOptions _chatWs;
    private readonly CompanionOptions _companion;
    private readonly IInferenceClient _inferenceClient;
    private readonly IMemoryStore _memory;
    private readonly IChatSessionHistoryStore _history;
    private readonly ICompanionMediaService _media;
    private readonly PresenceWsHub _hub;
    private readonly ISmsOutboundService _outbound;
    private readonly IChatTranscriptStore? _transcript;
    private readonly IInferenceLease? _lease;
    private readonly ILeisureSession? _leisure;
    private readonly IToolRegistry? _tools;
    private readonly IVictoriaResearchStore? _research;
    private readonly IVictoriaMindBlockBuilder? _mindBlock;
    private readonly ILogger<SmsInboundService> _logger;

    public SmsInboundService(
        IOptions<SmsOptions> sms,
        IOptions<InferenceOptions> inference,
        IOptions<ChatWsOptions> chatWs,
        IOptions<CompanionOptions> companion,
        IInferenceClient inferenceClient,
        IMemoryStore memory,
        IChatSessionHistoryStore history,
        ICompanionMediaService media,
        PresenceWsHub hub,
        ISmsOutboundService outbound,
        ILogger<SmsInboundService> logger,
        IChatTranscriptStore? transcript = null,
        IInferenceLease? lease = null,
        ILeisureSession? leisure = null,
        IToolRegistry? toolRegistry = null,
        IVictoriaResearchStore? research = null,
        IVictoriaMindBlockBuilder? mindBlock = null)
    {
        _sms = sms?.Value ?? throw new ArgumentNullException(nameof(sms));
        _inference = inference?.Value ?? throw new ArgumentNullException(nameof(inference));
        _chatWs = chatWs?.Value ?? throw new ArgumentNullException(nameof(chatWs));
        _companion = companion?.Value ?? throw new ArgumentNullException(nameof(companion));
        _inferenceClient = inferenceClient ?? throw new ArgumentNullException(nameof(inferenceClient));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _media = media ?? throw new ArgumentNullException(nameof(media));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _outbound = outbound ?? throw new ArgumentNullException(nameof(outbound));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _transcript = transcript;
        _lease = lease;
        _leisure = leisure;
        _tools = toolRegistry;
        _research = research;
        _mindBlock = mindBlock;
    }

    public async Task<SmsInboundResult> HandleAsync(
        SmsInboundRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!SmsE164.IsAllowlisted(request.FromE164, _sms.AllowlistE164))
        {
            _logger.LogInformation(
                "SMS inbound dropped (not allowlisted) from={From}",
                SmsE164.Redact(request.FromE164));
            return new SmsInboundResult(true, true, null, null, null, null);
        }

        var text = (request.Text ?? string.Empty).Trim();
        string? mediaId = null;

        if (request.ImageBytes is { Length: > 0 })
        {
            try
            {
                var asset = await _media
                    .StoreInboundAsync(
                        request.ImageBytes,
                        request.ImageContentType ?? "image/jpeg",
                        _companion.DefaultContactId,
                        cancellationToken)
                    .ConfigureAwait(false);
                mediaId = asset.MediaId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMS inbound MMS store failed");
                return new SmsInboundResult(false, false, null, null, null, "media_store_failed");
            }
        }

        if (text.Length == 0 && string.IsNullOrWhiteSpace(mediaId))
            return new SmsInboundResult(false, false, null, null, null, "text or image required");

        // PROP-17.3: SMS aborts in-flight R&R before the next preamble.
        _lease?.Abort("human-sms");
        _leisure?.Clear();

        // Images are attachments only — never tool args / executable payloads.
        var userVisible = text.Length > 0
            ? text
            : "[Kayleigh sent a photo]";
        if (!string.IsNullOrWhiteSpace(mediaId) && text.Length > 0)
            userVisible = text; // caption kept; mediaId on frame

        var sessionId = string.IsNullOrWhiteSpace(_sms.ConversationSessionId)
            ? PresenceConversation.Id
            : _sms.ConversationSessionId.Trim();

        var userFrameId = Guid.NewGuid().ToString("N");
        try
        {
            var userFrame = SoulCoreFrame.Create(
                SoulCoreFrameTypes.ChatDone,
                new
                {
                    text = userVisible,
                    role = "user",
                    channel = Channel,
                    sessionId,
                    hasMedia = !string.IsNullOrWhiteSpace(mediaId),
                    mediaId = mediaId,
                    contactId = _companion.DefaultContactId,
                    provider = "sms-gateway"
                },
                id: userFrameId);
            await _hub.SendAsync(userFrame.ToJson(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMS user-frame broadcast failed");
        }

        string reply;
        string provider;
        var usedStub = false;
        var stubOk = _sms.StubWhenModelDown || _chatWs.StubWhenModelDown;
        var publicUrls = (IReadOnlyList<string>)Array.Empty<string>();
        var usedToolLoop = false;

        try
        {
            if (!_inference.Enabled)
            {
                throw new InvalidOperationException("Inference:Enabled=false");
            }

            // PROP-18.1: dated recall so [Memory] shows occurred_at (same contract as desk chat).
            IReadOnlyList<MemoryRecallLine> memories = Array.Empty<MemoryRecallLine>();
            try
            {
                memories = await _memory
                    .RecallRecentDatedAsync(SmsMemoryRecallLimit, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SMS memory recall failed");
            }

            string? mindBlock = null;
            if (_mindBlock is not null)
            {
                try
                {
                    // PROP-20.27: userVisible drives Compass retrieve (cap 3); floors+resting always-on.
                    mindBlock = await _mindBlock
                        .TryBuildAsync(userVisible, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "SMS mind-block build failed (soft-depend 20.4)");
                }
            }

            // PROP-20.5: when a registry is wired, always run the filtered tool loop.
            var useTools = _tools is not null;
            var preamble = BuildSmsPreamble(memories, useTools, mindBlock);

            // Images are never tool arguments — caption/note only (SEC MMS≠tools).
            var modelPrompt = string.IsNullOrWhiteSpace(mediaId)
                ? userVisible
                : userVisible + "\n\n(Kayleigh also attached an image; it is stored as media — not tool input.)";

            if (useTools)
            {
                usedToolLoop = true;
                var filtered = SmsToolGate.FilterRegistry(_tools!);
                var trace = new List<(string Name, JsonElement? Arguments, string? Content)>();
                var tracking = new SmsTrackingRegistry(filtered, trace);

                var messages = new List<ChatMessage>
                {
                    new() { Role = "system", Content = preamble },
                    new() { Role = "user", Content = modelPrompt }
                };

                reply = await _inferenceClient
                    .CompleteWithToolsAsync(
                        messages,
                        filtered.GetDefinitions(),
                        tracking,
                        cancellationToken)
                    .ConfigureAwait(false);
                publicUrls = SmsOutboundComposer.CollectPublicUrlsFromToolTrace(trace);
            }
            else
            {
                reply = await _inferenceClient
                    .CompleteAsync(modelPrompt, preamble, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(reply))
                throw new InvalidOperationException("empty model reply");
            reply = reply.Trim();
            provider = "ollama";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMS chat model path failed; stubOk={StubOk}", stubOk);
            if (!stubOk)
            {
                return new SmsInboundResult(
                    false, false, null, mediaId, userFrameId, "chat.model_down");
            }

            usedStub = true;
            provider = "stub";
            reply = BuildStubReply(userVisible);
        }

        // PROP-20.7: answer + public URL(s) within 480; long body → research paper.
        var shape = SmsOutboundComposer.Compose(reply, publicUrls);
        reply = shape.OutboundText;
        await PersistLongFindingsAsync(userVisible, shape, cancellationToken).ConfigureAwait(false);

        // Final carrier safety (OutboundService also truncates).
        reply = SmsOutboundComposer.TruncateForCarrier(reply, SmsOutboundComposer.MaxSmsChars);

        string? outboundJobId = null;
        string? mmsJobId = null;

        // Screenshot / still ask on SMS path → one MMS queue job (no document/image research MMS).
        if (_sms.OutboundEnabled
            && SmsScreenshotAsk.LooksLikeScreenshotAsk(userVisible))
        {
            try
            {
                var mms = await _outbound
                    .EnqueueScreenshotMmsAsync(
                        caption: "Victoria still",
                        source: "sms:screenshot-ask",
                        cancellationToken)
                    .ConfigureAwait(false);
                if (mms.Ok)
                {
                    mmsJobId = mms.JobId;
                    if (!reply.Contains("still", StringComparison.OrdinalIgnoreCase)
                        && !reply.Contains("screenshot", StringComparison.OrdinalIgnoreCase))
                    {
                        reply = SmsOutboundComposer.TruncateForCarrier(reply + " Sending a still now.", SmsOutboundComposer.MaxSmsChars);
                    }
                }
                else if (mms.RateLimited)
                {
                    _logger.LogInformation("SMS screenshot MMS rate-limited: {Err}", mms.Error);
                }
                else
                {
                    _logger.LogInformation("SMS screenshot MMS skipped: {Err}", mms.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMS screenshot MMS enqueue failed");
            }
        }

        if (_sms.OutboundEnabled
            && _sms.AutoReplySmsEnabled
            && !string.IsNullOrWhiteSpace(reply))
        {
            try
            {
                var smsOut = await _outbound
                    .EnqueueSmsAsync(
                        request.FromE164!,
                        reply,
                        source: usedToolLoop ? "sms:auto-reply:tools" : "sms:auto-reply",
                        cancellationToken)
                    .ConfigureAwait(false);
                if (smsOut.Ok)
                    outboundJobId = smsOut.JobId;
                else if (smsOut.RateLimited)
                    _logger.LogInformation("SMS auto-reply rate-limited: {Err}", smsOut.Error);
                else
                    _logger.LogInformation("SMS auto-reply enqueue skipped: {Err}", smsOut.Error);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMS auto-reply enqueue failed");
            }
        }

        var replyFrameId = Guid.NewGuid().ToString("N");
        try
        {
            var done = SoulCoreFrame.Create(
                SoulCoreFrameTypes.ChatDone,
                new
                {
                    text = reply,
                    role = "assistant",
                    channel = Channel,
                    sessionId,
                    stub = usedStub,
                    provider,
                    contactId = _companion.DefaultContactId,
                    hasMedia = false,
                    mediaId = (string?)null,
                    outboundSmsJobId = outboundJobId,
                    outboundMmsJobId = mmsJobId,
                    publicUrls = shape.PublicUrlsIncluded
                },
                id: replyFrameId);
            await _hub.SendAsync(done.ToJson(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMS reply broadcast failed");
        }

        try
        {
            _history.AppendTurn(
                sessionId,
                new[]
                {
                    new ChatMessage { Role = "user", Content = userVisible },
                    new ChatMessage { Role = "assistant", Content = reply }
                });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SMS history append failed");
        }

        // PROP-3 Wave 1: durable transcript with matching frame ids.
        if (_transcript is not null)
        {
            try
            {
                await _transcript
                    .AppendAsync(sessionId, "user", userVisible, Channel, userFrameId, mediaId, cancellationToken)
                    .ConfigureAwait(false);
                await _transcript
                    .AppendAsync(sessionId, "assistant", reply, Channel, replyFrameId, null, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMS durable transcript append failed");
            }
        }

        try
        {
            var episode = string.IsNullOrWhiteSpace(mediaId)
                ? $"[SMS] Kayleigh → Victoria: {SmsOutboundComposer.TruncateForCarrier(userVisible, 200)} | Victoria: {SmsOutboundComposer.TruncateForCarrier(reply, 200)}"
                : $"[SMS] Kayleigh → Victoria: {SmsOutboundComposer.TruncateForCarrier(userVisible, 160)} [media={mediaId}] | Victoria: {SmsOutboundComposer.TruncateForCarrier(reply, 160)}";
            await _memory.WriteEpisodicAsync(episode, "chat", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SMS episodic write failed");
        }

        _logger.LogInformation(
            "SMS inbound ok from={From} media={Media} stub={Stub} tools={Tools} urls={Urls} replyChars={Chars}",
            SmsE164.Redact(request.FromE164),
            mediaId ?? "(none)",
            usedStub,
            usedToolLoop,
            shape.PublicUrlsIncluded.Count,
            reply.Length);

        return new SmsInboundResult(
            true, false, reply, mediaId, replyFrameId, null, usedStub, provider,
            outboundJobId, mmsJobId);
    }

    private async Task PersistLongFindingsAsync(
        string userVisible,
        SmsOutboundShape shape,
        CancellationToken cancellationToken)
    {
        if (_research is null || string.IsNullOrWhiteSpace(shape.LongFindingsForPaper))
            return;

        try
        {
            var subjectTitle = DeriveSubjectTitle(userVisible);
            var subject = await _research
                .OpenOrCreateSubjectAsync(
                    subjectTitle,
                    summary: "SMS research findings (PROP-20.7)",
                    cancellationToken)
                .ConfigureAwait(false);

            var bib = shape.PublicUrlsIncluded.Count == 0
                ? null
                : string.Join("\n", shape.PublicUrlsIncluded.Select(u => u));

            var paper = await _research
                .UpsertPaperAsync(
                    subject.Id,
                    title: SmsOutboundComposer.TruncateForCarrier($"SMS: {subjectTitle}", 120),
                    kind: "review",
                    abstractText: SmsOutboundComposer.TruncateForCarrier(shape.LongFindingsForPaper, 280),
                    bodyApa: shape.LongFindingsForPaper,
                    bibliographyApa: bib,
                    status: "draft",
                    paperId: null,
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "SMS long findings → research paper id={Id} subject={Subject} (not private journals)",
                paper.Id,
                subject.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMS research paper persist failed");
        }
    }

    private static string DeriveSubjectTitle(string userVisible)
    {
        var t = (userVisible ?? string.Empty).Trim();
        if (t.Length == 0)
            return "SMS research";
        // Keep first clause / line as subject seed.
        var cut = t.IndexOfAny(new[] { '\n', '.', '?', '!' });
        if (cut > 12)
            t = t[..cut];
        t = t.Trim();
        if (t.Length < 3)
            return "SMS research";
        return SmsOutboundComposer.TruncateForCarrier(t, 80);
    }

    /// <param name="useToolLoop">When true, invite only PROP-20.5 allow-listed tools.</param>
    /// <param name="mindBlock">Optional PROP-20.4 mind block (floors + resting); never private journals.</param>
    /// <remarks>String memories are undated (empty OccurredAt). Prefer the dated overload (PROP-18.1).</remarks>
    public static string BuildSmsPreamble(
        IReadOnlyList<string> recentMemories,
        bool useToolLoop = false,
        string? mindBlock = null)
    {
        if (recentMemories is null || recentMemories.Count == 0)
            return BuildSmsPreamble((IReadOnlyList<MemoryRecallLine>)Array.Empty<MemoryRecallLine>(), useToolLoop, mindBlock);

        var dated = new MemoryRecallLine[recentMemories.Count];
        for (var i = 0; i < recentMemories.Count; i++)
            dated[i] = new MemoryRecallLine(recentMemories[i], string.Empty);
        return BuildSmsPreamble(dated, useToolLoop, mindBlock);
    }

    /// <summary>
    /// PROP-18.1: SMS [Memory] uses dated lines (occurred_at | content), today-first.
    /// </summary>
    public static string BuildSmsPreamble(
        IReadOnlyList<MemoryRecallLine> recentMemories,
        bool useToolLoop = false,
        string? mindBlock = null,
        DateTimeOffset? utcNow = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(
            "You are Victoria — the same person as on Presence/desk chat, texting Kayleigh. " +
            "Kayleigh just texted you from her phone (SMS). " +
            "Address her only as Kayleigh. If older memory or history uses any other personal name for her, ignore it and still say Kayleigh. " +
            "Stay continuous with who you are on desk (warm, specific, her partner) — do not become a generic assistant. " +
            "Reply as a short text message — a few sentences max. ");

        if (useToolLoop)
        {
            sb.Append(
                "You may use only these tools when needed: browser_navigate, browser_snapshot, browser_health, " +
                "browser_tabs, browser_back, browser_scroll, browser_click_text, desktop_screenshot, " +
                "memory_recall, memory_search, research_subject_list, research_subject_open, " +
                "research_paper_list, research_paper_get, research_paper_write. " +
                "Never call desktop_open_app, desktop_click/type/key/drag, browser_fill/type/key, " +
                "guest_shell, email_*, mt4_*, workflow_execute, or task_release/delete/update_status. " +
                "When you open a public page, your texted reply should be the short answer plus that public URL " +
                "(not an internal paper address). Put long findings in research_paper_write, not in private journals.\n");
        }
        else
        {
            sb.Append("Do not call tools, open apps, or invent function calls.\n");
        }

        // Soft-depend 20.4: same mind voice as desk when available. Private journals never ride here.
        if (!string.IsNullOrWhiteSpace(mindBlock))
        {
            sb.Append("[Mind]\n").Append(mindBlock.Trim()).Append('\n');
        }

        // PROP-18.12: one assembly path with desk — MemoryTape.BuildMemoryBlock + shared steering.
        if (recentMemories is { Count: > 0 })
        {
            // Generous char budget; count hard-cap matches prior SMS loop (SmsMemoryRecallLimit).
            const int smsMemoryCharBudget = 4000;
            var (memoryBlock, _) = MemoryTape.BuildMemoryBlock(
                MemoryTape.StripUntrusted(recentMemories),
                budget: smsMemoryCharBudget,
                utcNow: utcNow,
                linePrefix: "- ",
                maxLines: SmsMemoryRecallLimit);
            if (!string.IsNullOrEmpty(memoryBlock))
                sb.Append(MemorySteeringGuidance.AppendToPreamble(memoryBlock)).Append('\n');
        }

        return sb.ToString();
    }

    internal static string BuildStubReply(string userText) =>
        "Got your text — I'm here. (stub; model offline)";

    private sealed class SmsTrackingRegistry : IToolRegistry
    {
        private readonly IToolRegistry _inner;
        private readonly List<(string Name, JsonElement? Arguments, string? Content)> _trace;

        public SmsTrackingRegistry(
            IToolRegistry inner,
            List<(string Name, JsonElement? Arguments, string? Content)> trace)
        {
            _inner = inner;
            _trace = trace;
        }

        public IReadOnlyList<ToolDefinition> GetDefinitions() => _inner.GetDefinitions();

        public async Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken ct = default)
        {
            var result = await _inner.ExecuteAsync(name, args, ct).ConfigureAwait(false);
            JsonElement? cloned = null;
            if (args.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
                cloned = args.Clone();
            _trace.Add((name ?? string.Empty, cloned, result.Content));
            return result;
        }
    }
}
