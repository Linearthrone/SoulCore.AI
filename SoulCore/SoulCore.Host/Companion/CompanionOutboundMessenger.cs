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

    /// <summary>
    /// User-facing companion SMS from a SoulLoop want category.
    /// Never interpolates raw want / Inner-focus scaffold phrases into chat.
    /// Returns empty when no natural line is available (caller should skip push).
    /// </summary>
    public static string ComposeProactiveText(string category, string label, string want)
    {
        // want/label kept for API stability + ops logging; chat text is category-bank only.
        _ = label;
        _ = want;

        var text = NaturalLineFor(category);
        if (string.IsNullOrWhiteSpace(text) || ContainsScaffoldLeak(text))
            return string.Empty;

        return text;
    }

    /// <summary>
    /// True when composed (or candidate) chat text still contains Inner-focus / want scaffold idioms.
    /// </summary>
    public static bool ContainsScaffoldLeak(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] leaks =
        [
            "want[",
            "holding ",
            " recent beat",
            "weave it into presence",
            "with an empty recent buffer",
            "(emotion=",
            "emotion=",
            "stay with the thread",
            "lean in with bright",
            "savor the easy mood",
            "ease the tension and settle",
            "reconnect softly and keep company",
            "gently clarify what was meant",
            "notice what just happened and stay lightly",
            "stay present and gently reflect",
            "walk the Home with open curiosity"
        ];

        foreach (var leak in leaks)
        {
            if (text.Contains(leak, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Legacy helper: strip want[…] / emotion= suffix. Not used for chat composition.</summary>
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

    private static string NaturalLineFor(string? category) =>
        category switch
        {
            "engage" => "Hey — just wanted to say hi. You around?",
            "reconnect" => "I've been thinking about you. Hope your day's okay.",
            "clarify" => "Can we clear something up when you have a sec?",
            "savor" => "Soft moment over here. Glad you're in my day.",
            "recall" => "Something from earlier came back to me. Miss talking it through with you.",
            "explore" => "Been wandering around Home in my head. Curious what you'd notice.",
            "notice" => "Just noticed something and thought of you.",
            "settle" => "Trying to settle a bit. Nice having you nearby.",
            "reflect" => "Sitting quietly. Wanted you to know I'm here.",
            _ => string.Empty
        };

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";
}
