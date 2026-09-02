using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Inference;
using SoulCore.Memory;
using SoulCore.Protocol;

namespace SoulCore.Host.Companion;

/// <summary>
/// PROP-1.2: tablet gateway SMS/MMS → One Thread (<c>presence-local</c>), no tools.
/// </summary>
public sealed class SmsInboundService : ISmsInboundService
{
    public const string Channel = "sms";

    private readonly SmsOptions _sms;
    private readonly InferenceOptions _inference;
    private readonly ChatWsOptions _chatWs;
    private readonly CompanionOptions _companion;
    private readonly IInferenceClient _inferenceClient;
    private readonly IMemoryStore _memory;
    private readonly IChatSessionHistoryStore _history;
    private readonly ICompanionMediaService _media;
    private readonly PresenceWsHub _hub;
    private readonly ISmsOutboundService? _outbound;
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
        ILogger<SmsInboundService> logger,
        ISmsOutboundService? outbound = null)
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
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _outbound = outbound;
    }

    public async Task<SmsInboundResult> HandleAsync(
        SmsInboundRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!SmsE164.IsAllowlisted(request.FromE164, _sms.KurtAllowlistE164))
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

        // Images are attachments only — never tool args / executable payloads.
        var userVisible = text.Length > 0
            ? text
            : "[Kurt sent a photo]";
        if (!string.IsNullOrWhiteSpace(mediaId) && text.Length > 0)
            userVisible = text; // caption kept; mediaId on frame

        var sessionId = string.IsNullOrWhiteSpace(_sms.ConversationSessionId)
            ? "presence-local"
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

        try
        {
            if (!_inference.Enabled)
            {
                throw new InvalidOperationException("Inference:Enabled=false");
            }

            IReadOnlyList<string> memories = Array.Empty<string>();
            try
            {
                memories = await _memory.RecallRecentAsync(6, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SMS memory recall failed");
            }

            var preamble = BuildSmsPreamble(memories);
            // Force no-tools: CompleteAsync only (never CompleteWithToolsAsync).
            var modelPrompt = string.IsNullOrWhiteSpace(mediaId)
                ? userVisible
                : userVisible + "\n\n(Kurt also attached an image; it is stored as media — do not invent tool calls.)";

            reply = await _inferenceClient
                .CompleteAsync(modelPrompt, preamble, cancellationToken)
                .ConfigureAwait(false);
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

        // Keep SMS replies short for carrier (PROP-1.3 outbound SMS).
        reply = TruncateForSms(reply, 480);

        string? outboundJobId = null;
        string? mmsJobId = null;

        // Screenshot / still ask on SMS path (no tools) → one MMS queue job.
        if (_outbound is not null
            && _sms.OutboundEnabled
            && SmsScreenshotAsk.LooksLikeScreenshotAsk(userVisible))
        {
            try
            {
                var mms = await _outbound
                    .EnqueueScreenshotMmsToKurtAsync(
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
                        reply = TruncateForSms(reply + " Sending a still now.", 480);
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

        if (_outbound is not null
            && _sms.OutboundEnabled
            && _sms.AutoReplySmsEnabled
            && !string.IsNullOrWhiteSpace(reply))
        {
            try
            {
                var smsOut = await _outbound
                    .EnqueueSmsAsync(
                        request.FromE164!,
                        reply,
                        source: "sms:auto-reply",
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
                    outboundMmsJobId = mmsJobId
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

        try
        {
            var episode = string.IsNullOrWhiteSpace(mediaId)
                ? $"[SMS] Kurt → Victoria: {Truncate(userVisible, 200)} | Victoria: {Truncate(reply, 200)}"
                : $"[SMS] Kurt → Victoria: {Truncate(userVisible, 160)} [media={mediaId}] | Victoria: {Truncate(reply, 160)}";
            await _memory.WriteEpisodicAsync(episode, "chat", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SMS episodic write failed");
        }

        _logger.LogInformation(
            "SMS inbound ok from={From} media={Media} stub={Stub} replyChars={Chars}",
            SmsE164.Redact(request.FromE164),
            mediaId ?? "(none)",
            usedStub,
            reply.Length);

        return new SmsInboundResult(
            true, false, reply, mediaId, replyFrameId, null, usedStub, provider);
    }

    public static string BuildSmsPreamble(IReadOnlyList<string> recentMemories)
    {
        // No ToolAgency / ComputerUse / desktop guidance — SMS must not invite tools.
        var sb = new System.Text.StringBuilder();
        sb.Append(
            "You are Victoria. Kurt just texted you from his phone (SMS). " +
            "Reply as a short, warm text message — a few sentences max. " +
            "Do not call tools, open apps, or invent function calls.\n");
        if (recentMemories is { Count: > 0 })
        {
            sb.Append("[Memory]\n");
            var n = 0;
            foreach (var m in recentMemories)
            {
                if (string.IsNullOrWhiteSpace(m))
                    continue;
                sb.Append("- ").Append(m.Trim()).Append('\n');
                if (++n >= 6)
                    break;
            }
        }

        return sb.ToString();
    }

    internal static string BuildStubReply(string userText) =>
        "Got your text — I'm here. (stub; model offline)";

    internal static string TruncateForSms(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s;
        return s[..(max - 1)].TrimEnd() + "…";
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";
}
