using System.Text;
using Microsoft.Extensions.Logging;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Inference;
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
    private readonly ILogger<ChatContextBuilder> _logger;

    public ChatContextBuilder(
        IMemoryStore memory,
        IEmbeddingClient embeddings,
        ICharter charter,
        IEmotionState emotion,
        ILogger<ChatContextBuilder> logger)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _charter = charter ?? throw new ArgumentNullException(nameof(charter));
        _emotion = emotion ?? throw new ArgumentNullException(nameof(emotion));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ChatContext> BuildAsync(
        string userText,
        bool useToolLoop,
        string? desktopTargetWindowTitle,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);

        // PROP-8.4 / PROP-5 gate: independent reads run in parallel; each piece
        // is assembled into one immutable ChatContext afterward.
        var memoriesTask = RecallChatMemoriesSafeAsync(userText, cancellationToken);
        var identityTask = LoadIdentityAnchorsSafeAsync(cancellationToken);
        var emotionTask = LoadEmotionPreambleSafeAsync(cancellationToken);

        await Task.WhenAll(memoriesTask, identityTask, emotionTask).ConfigureAwait(false);

        var recentMemories = await memoriesTask.ConfigureAwait(false);
        var identityAnchors = await identityTask.ConfigureAwait(false);
        var emotionPreamble = await emotionTask.ConfigureAwait(false);

        _logger.LogDebug(
            "Chat context loaded: memories={MemoryCount} identity={IdentityCount} emotionChars={EmotionLen}",
            recentMemories.Count,
            identityAnchors.Count,
            emotionPreamble.Length);

        var preamble = BuildContextPreamble(identityAnchors, recentMemories, emotionPreamble);

        if (useToolLoop)
        {
            preamble = ToolAgencyGuidance.AppendToPreamble(preamble);
            preamble = ComputerUseGuidance.AppendToPreamble(preamble, desktopTargetWindowTitle);
            preamble = HomeBodyGuidance.AppendToPreamble(preamble);
            preamble = ChiefArchitectGuidance.AppendToPreamble(preamble);
            preamble = EmailGuidance.AppendToPreamble(preamble);
        }

        return new ChatContext(preamble, identityAnchors, recentMemories, emotionPreamble);
    }

    /// <summary>
    /// Combines charter identity anchors, recent episodic memories, and the emotion
    /// preamble into a single deterministic system preamble.
    /// </summary>
    public static string BuildContextPreamble(
        IReadOnlyList<string> identityAnchors,
        IReadOnlyList<string> recentMemories,
        string emotionPreamble)
    {
        ArgumentNullException.ThrowIfNull(emotionPreamble);

        var identityBlock = BuildIdentityBlock(identityAnchors);
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

    private async Task<string> LoadEmotionPreambleSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var emotion = await _emotion.GetAsync(cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken)
    {
        try
        {
            var memories = await RecallChatMemoriesAsync(userText, cancellationToken).ConfigureAwait(false);
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
