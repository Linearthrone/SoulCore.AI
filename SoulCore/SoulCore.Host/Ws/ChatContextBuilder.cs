using System.Text;
using Microsoft.Extensions.Logging;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Persona;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Inference.Tools.Body;
using SoulCore.Inference.Tools.ChiefArchitect;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Inference.Tools.Email;
using SoulCore.Inference.Tools.Trading;
using SoulCore.Inference.Tools.Workflow;
using SoulCore.Memory;

namespace SoulCore.Host.Ws;

/// <summary>
/// Single prompt composition owner: parallel independent context reads →
/// deterministic [Identity] → [Memory] → [SoulCore emotion] (+ tool guidance).
/// <see cref="BuildAsync"/> reads the active pack once at the start of that call.
/// <see cref="BuildPinnedAsync"/> uses the pack and memory the caller already copied
/// and does not call <see cref="IPersonaSession.GetActive"/>.
/// </summary>
public sealed class ChatContextBuilder : IChatContextBuilder
{
    /// <summary>Budget for [Identity]+[Memory] chars (emotion appended outside).</summary>
    public const int ContextPreambleCharLimit = 16000;

    /// <summary>How many recent non-quarantined episodic memories to fold into the preamble.</summary>
    public const int ContextMemoryRecallLimit = 5;

    private readonly IMemoryStore _memory;
    private readonly IEmbeddingClient _embeddings;
    private readonly ICharter _charter;
    private readonly IEmotionState _emotion;
    private readonly IToolsAccessSettings _toolsAccess;
    private readonly IPersonaSession _personaSession;
    private readonly ILogger<ChatContextBuilder> _logger;

    public ChatContextBuilder(
        IMemoryStore memory,
        IEmbeddingClient embeddings,
        ICharter charter,
        IEmotionState emotion,
        IToolsAccessSettings toolsAccess,
        IPersonaSession personaSession,
        ILogger<ChatContextBuilder> logger)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _charter = charter ?? throw new ArgumentNullException(nameof(charter));
        _emotion = emotion ?? throw new ArgumentNullException(nameof(emotion));
        _toolsAccess = toolsAccess ?? throw new ArgumentNullException(nameof(toolsAccess));
        _personaSession = personaSession ?? throw new ArgumentNullException(nameof(personaSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<ChatContext> BuildAsync(
        string userText,
        bool useToolLoop,
        string? desktopTargetWindowTitle,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);

        // Live adjust = next turn: read active pack at build time (not mid-inference).
        var pack = _personaSession.GetActive();
        return BuildPinnedAsync(
            userText,
            useToolLoop,
            desktopTargetWindowTitle,
            pack,
            _memory,
            foregroundTurn: true,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ChatContext> BuildPinnedAsync(
        string userText,
        bool useToolLoop,
        string? desktopTargetWindowTitle,
        PersonaPack pinnedPack,
        IMemoryStore pinnedMemory,
        bool foregroundTurn,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);
        ArgumentNullException.ThrowIfNull(pinnedPack);
        ArgumentNullException.ThrowIfNull(pinnedMemory);

        // PROP-16.6: the caller pinned this pack. Do not read GetActive() here.
        var pack = pinnedPack;

        // PROP-8.4 / PROP-5 gate: independent reads run in parallel; each piece
        // is assembled into one immutable ChatContext afterward.
        var memoriesTask = RecallChatMemoriesSafeAsync(userText, pinnedMemory, cancellationToken);
        var identityTask = foregroundTurn
            ? LoadIdentityAnchorsSafeAsync(cancellationToken)
            : Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        var emotionTask = LoadEmotionPreambleSafeAsync(pinnedMemory, foregroundTurn, cancellationToken);

        await Task.WhenAll(memoriesTask, identityTask, emotionTask).ConfigureAwait(false);

        var recentMemories = await memoriesTask.ConfigureAwait(false);
        var identityAnchors = await identityTask.ConfigureAwait(false);
        var emotionPreamble = await emotionTask.ConfigureAwait(false);

        var blurbHasLimits = PersonaPromptBlocks.BlurbHasHardLimits(pack.IdentityBlurb);
        var boundaryBand = PersonaTraitCompiler.ToBand(
            (pack.Traits ?? new PersonaTraitScales()).BoundaryStrictness);
        _logger.LogDebug(
            "Chat context loaded: persona={PersonaId} memories={MemoryCount} identity={IdentityCount} emotionChars={EmotionLen} blurbHasLimits={BlurbHasLimits} boundaryBand={BoundaryBand}",
            pack.PersonaId,
            recentMemories.Count,
            identityAnchors.Count,
            emotionPreamble.Length,
            blurbHasLimits,
            boundaryBand);

        var preamble = BuildContextPreamble(identityAnchors, recentMemories, emotionPreamble, pack);

        if (useToolLoop)
        {
            preamble = ToolAgencyGuidance.AppendToPreamble(preamble);
            preamble = ComputerUseGuidance.AppendToPreamble(
                preamble,
                desktopTargetWindowTitle,
                _toolsAccess.BrowserBackend);
            preamble = HomeBodyGuidance.AppendToPreamble(preamble);
            preamble = ChiefArchitectGuidance.AppendToPreamble(preamble);
            preamble = EmailGuidance.AppendToPreamble(preamble);
            preamble = PersonaPromptBlocks.AppendToolGuidance(preamble, pack);
        }

        // PROP-15.20: Host [Hard policy] coda after *full* preamble (incl. tool guidance)
        // so soft agency blocks cannot bury LIMITS/META under mother:latest.
        preamble = PersonaPromptBlocks.AppendHardPolicyCoda(preamble, pack);

        return new ChatContext(preamble, identityAnchors, recentMemories, emotionPreamble, pack.PersonaId);
    }

    /// <summary>
    /// Combines charter identity anchors, recent episodic memories, and the emotion
    /// preamble into a single deterministic system preamble for the active pack.
    /// </summary>
    public static string BuildContextPreamble(
        IReadOnlyList<string> identityAnchors,
        IReadOnlyList<string> recentMemories,
        string emotionPreamble,
        PersonaPack? pack = null)
    {
        ArgumentNullException.ThrowIfNull(emotionPreamble);
        pack ??= PersonaPack.CreateBlank();

        var identityBlock = BuildIdentityBlock(identityAnchors, pack);
        var (memoryBlock, droppedMemoryCount) = BuildMemoryBlock(
            recentMemories,
            ContextPreambleCharLimit - identityBlock.Length);

        if (droppedMemoryCount > 0)
            memoryBlock += $"\n({droppedMemoryCount} older memories truncated)";

        return string.Concat(identityBlock, memoryBlock, emotionPreamble);
    }

    private async Task<IReadOnlyList<string>> LoadIdentityAnchorsSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var anchors = await _charter
                .GetAnchorsByKindAsync("identity", null, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogDebug("Loaded {Count} charter identity anchors", anchors.Count);
            return anchors;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Charter identity recall failed; chat continues without identity");
            return Array.Empty<string>();
        }
    }

    private async Task<string> LoadEmotionPreambleSafeAsync(
        IMemoryStore pinnedMemory,
        bool foregroundTurn,
        CancellationToken cancellationToken)
    {
        try
        {
            // Foreground uses the live emotion facade. A background turn reads that
            // persona's own store when the memory handle is also an emotion store,
            // and otherwise stays neutral so the foreground vector is not copied in.
            IReadOnlyDictionary<string, double> emotion;
            if (foregroundTurn)
            {
                emotion = await _emotion.GetAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (pinnedMemory is IEmotionState sideEmotion)
            {
                emotion = await sideEmotion.GetAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                emotion = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            }
            var preamble = EmotionInfluencePrompt.BuildPreamble(emotion);
            _logger.LogDebug("Emotion influence preamble ready ({Length} chars)", preamble.Length);
            return preamble;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Emotion read failed; chat continues without influence preamble");
            return EmotionInfluencePrompt.BuildPreamble(
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private async Task<IReadOnlyList<string>> RecallChatMemoriesSafeAsync(
        string userText,
        IMemoryStore memory,
        CancellationToken cancellationToken)
    {
        try
        {
            var memories = await RecallChatMemoriesAsync(userText, memory, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Recalled {Count} episodic memories for chat.send", memories.Count);
            return memories;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Episodic recall failed; chat continues without memory");
            return Array.Empty<string>();
        }
    }

    private async Task<IReadOnlyList<string>> RecallChatMemoriesAsync(
        string userText,
        IMemoryStore memory,
        CancellationToken cancellationToken)
    {
        if (!_embeddings.IsEnabled)
        {
            return await memory
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
                return await memory
                    .RecallRecentAsync(ContextMemoryRecallLimit, cancellationToken)
                    .ConfigureAwait(false);
            }

            var similar = await memory
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

        return await memory
            .RecallRecentAsync(ContextMemoryRecallLimit, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the [Identity] section from pack directives + charter anchors.
    /// Order (PROP-15.17): name → human address → trait Compile (voice/recall;
    /// soft Low boundaries gated when blurb has LIMITS) → IdentityBlurb
    /// (LIMITS/META/UNCERTAINTY last so hard policy wins attention) → charter anchors.
    /// </summary>
    public static string BuildIdentityBlock(IReadOnlyList<string> identityAnchors, PersonaPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        var hardLimits = PersonaPromptBlocks.BlurbHasHardLimits(pack.IdentityBlurb);
        var sb = new StringBuilder(768);
        sb.Append("[Identity]\n");
        sb.Append(PersonaPromptBlocks.BuildIdentityNameLine(pack));
        sb.Append('\n');
        sb.Append(PersonaPromptBlocks.BuildHumanAddressRule(pack.HumanAddress));
        sb.Append('\n');
        // Trait voice/recall before blurb; soft "Flexible boundaries" omitted when LIMITS present.
        sb.Append(PersonaTraitCompiler.Compile(pack, hardLimitsFromBlurb: hardLimits));
        if (!string.IsNullOrWhiteSpace(pack.IdentityBlurb))
            sb.Append('\n').Append(pack.IdentityBlurb.Trim());
        if (identityAnchors is { Count: > 0 })
        {
            foreach (var anchor in identityAnchors)
            {
                if (string.IsNullOrWhiteSpace(anchor))
                    continue;
                sb.Append('\n').Append(anchor.Trim());
            }
        }

        sb.Append("\n\n");
        return sb.ToString();
    }

    /// <summary>
    /// Builds the [Memory] section, truncating the oldest memories first to fit
    /// <paramref name="budget"/> chars.
    /// </summary>
    public static (string Block, int DroppedCount) BuildMemoryBlock(
        IReadOnlyList<string> recentMemories,
        int budget)
    {
        if (recentMemories is null || recentMemories.Count == 0)
            return (string.Empty, 0);

        const string header = "[Memory]\n";
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
}
