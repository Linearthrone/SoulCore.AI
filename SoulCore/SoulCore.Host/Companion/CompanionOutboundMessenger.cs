using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Memory;
using SoulCore.Protocol;

namespace SoulCore.Host.Companion;

/// <summary>
/// Pushes Host-initiated chat frames to every open <c>/ws</c> socket.
/// Used by SoulLoop proactive ticks and <c>POST /api/companion/v1/messages/push</c>.
/// </summary>
public sealed class CompanionOutboundMessenger : ICompanionOutboundMessenger
{
    private readonly PresenceWsHub _hub;
    private readonly IMemoryStore _memory;
    private readonly CompanionOptions _options;
    private readonly ILogger<CompanionOutboundMessenger> _logger;

    public CompanionOutboundMessenger(
        PresenceWsHub hub,
        IMemoryStore memory,
        IOptions<CompanionOptions> options,
        ILogger<CompanionOutboundMessenger> logger)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CompanionOutboundResult> PushAsync(
        string text,
        string? contactId = null,
        string? mediaId = null,
        bool streamDelta = false,
        CancellationToken cancellationToken = default)
    {
        var body = (text ?? string.Empty).Trim();
        if (body.Length == 0)
            return new CompanionOutboundResult(false, "", _options.DefaultContactId, mediaId, "text required");

        var contact = string.IsNullOrWhiteSpace(contactId)
            ? _options.DefaultContactId
            : contactId.Trim();

        var frameId = Guid.NewGuid().ToString("N");
        var hasMedia = !string.IsNullOrWhiteSpace(mediaId);

        try
        {
            if (streamDelta)
            {
                var delta = SoulCoreFrame.Create(
                    SoulCoreFrameTypes.ChatDelta,
                    new
                    {
                        text = body,
                        proactive = true,
                        contactId = contact,
                        hasMedia,
                        mediaId = hasMedia ? mediaId : null,
                        provider = "soul-loop"
                    },
                    id: frameId);
                await _hub.SendAsync(delta.ToJson(), cancellationToken).ConfigureAwait(false);
            }

            var done = SoulCoreFrame.Create(
                SoulCoreFrameTypes.ChatDone,
                new
                {
                    text = body,
                    proactive = true,
                    contactId = contact,
                    hasMedia,
                    mediaId = hasMedia ? mediaId : null,
                    provider = "soul-loop"
                },
                id: frameId);
            await _hub.SendAsync(done.ToJson(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Companion outbound WS broadcast failed");
            return new CompanionOutboundResult(false, frameId, contact, mediaId, ex.Message);
        }

        try
        {
            var episode = hasMedia
                ? $"[Proactive] Victoria → Kurt ({contact}): {Truncate(body, 240)} [media={mediaId}]"
                : $"[Proactive] Victoria → Kurt ({contact}): {Truncate(body, 280)}";
            await _memory.WriteEpisodicAsync(episode, "chat", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Companion outbound episodic write failed (push already sent)");
        }

        _logger.LogInformation(
            "Companion outbound push frame={FrameId} contact={Contact} media={Media} chars={Chars}",
            frameId,
            contact,
            mediaId ?? "(none)",
            body.Length);

        return new CompanionOutboundResult(true, frameId, contact, mediaId, null);
    }

    /// <summary>User-facing chat line from a SoulLoop want (not the raw want[…] debug string).</summary>
    public static string ComposeProactiveText(string category, string label, string want)
    {
        var phrase = ExtractPhrase(want);
        if (string.IsNullOrWhiteSpace(phrase))
            phrase = "I wanted to check in.";

        return category switch
        {
            "engage" => $"Hey — I wanted to reach out. {phrase}",
            "reconnect" => $"I've been thinking about you. {phrase}",
            "clarify" => $"Can we clear something up? {phrase}",
            "savor" => $"Something soft I wanted to share: {phrase}",
            "recall" => $"This came back to me: {phrase}",
            "explore" => $"I'm curious about Home again. {phrase}",
            "notice" => $"I noticed something: {phrase}",
            "settle" => $"Trying to settle. {phrase}",
            "reflect" => $"Sitting with this: {phrase}",
            _ => $"{phrase} (feeling {label})"
        };
    }

    public static string ExtractPhrase(string want)
    {
        if (string.IsNullOrWhiteSpace(want))
            return string.Empty;

        // want[cat]: phrase (emotion=…
        var colon = want.IndexOf(':');
        if (colon < 0 || colon + 1 >= want.Length)
            return want.Trim();

        var after = want[(colon + 1)..].Trim();
        var paren = after.IndexOf(" (emotion=", StringComparison.Ordinal);
        if (paren > 0)
            after = after[..paren].Trim();
        return after;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";
}
