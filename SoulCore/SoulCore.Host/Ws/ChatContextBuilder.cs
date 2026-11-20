using System.Text;
using Microsoft.Extensions.Logging;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Growth;
using SoulCore.Host.Growth;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Presence;
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
/// deterministic [Identity] + mind block → [Daybook] → [Memory] → [SoulCore emotion]
/// (+ tool guidance).
/// PROP-20.4: floors + resting traits share the 16k identity/memory budget; emotion stays outside.
/// PROP-20.37: growth traits only when soak may change prompt.
/// PROP-18.2: [Daybook] injects provenance-tagged journal (+ optional activity crumbs).
/// </summary>
public sealed class ChatContextBuilder : IChatContextBuilder
{
    /// <summary>Budget for [Identity]+mind+[Daybook]+[Memory] chars (emotion appended outside).</summary>
    public const int ContextPreambleCharLimit = 16000;

    /// <summary>How many recent non-quarantined episodic memories to fold into the preamble.</summary>
    public const int ContextMemoryRecallLimit = 5;

    private readonly IMemoryStore _memory;
    private readonly IEmbeddingClient _embeddings;
    private readonly ICharter _charter;
    private readonly IEmotionState _emotion;
    private readonly IToolsAccessSettings _toolsAccess;
    private readonly IVictoriaSopStore? _sops;
    private readonly IGrowthService? _growth;
    private readonly IVictoriaJournalStore? _journals;
    private readonly IVictoriaActivitiesLogStore? _activitiesLog;
    private readonly ILogger<ChatContextBuilder> _logger;

    public ChatContextBuilder(
        IMemoryStore memory,
        IEmbeddingClient embeddings,
        ICharter charter,
        IEmotionState emotion,
        IToolsAccessSettings toolsAccess,
        ILogger<ChatContextBuilder> logger,
        IVictoriaSopStore? sops = null,
        IGrowthService? growth = null,
        IVictoriaJournalStore? journals = null,
        IVictoriaActivitiesLogStore? activitiesLog = null)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _charter = charter ?? throw new ArgumentNullException(nameof(charter));
        _emotion = emotion ?? throw new ArgumentNullException(nameof(emotion));
        _toolsAccess = toolsAccess ?? throw new ArgumentNullException(nameof(toolsAccess));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sops = sops;
        _growth = growth;
        _journals = journals;
        _activitiesLog = activitiesLog;
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
        var charterTask = LoadCharterMindPartsSafeAsync(userText, cancellationToken);
        var emotionTask = LoadEmotionPreambleAndLabelSafeAsync(cancellationToken);
        var daybookTask = LoadDaybookLinesSafeAsync(cancellationToken);

        await Task.WhenAll(memoriesTask, charterTask, emotionTask, daybookTask).ConfigureAwait(false);

        var datedMemories = MemoryTape.StripUntrusted(
            await memoriesTask.ConfigureAwait(false));
        var recentMemories = MemoryTape.ProjectContent(datedMemories);
        var (floors, compass, identityAnchors) = await charterTask.ConfigureAwait(false);
        var (emotionPreamble, emotionLabel) = await emotionTask.ConfigureAwait(false);
        var daybookLines = await daybookTask.ConfigureAwait(false);

        // PROP-20.30: Host selects ≤3 context traits (tags + 24h fresh + flat-affect filter).
        // PROP-20.34: reuse label from the single emotion snapshot (no second GetAsync).
        var newestContact = VictoriaMindBlock.NewestContactUtc(
            datedMemories.Select(m => (string?)m.OccurredAt));
        var contextTraits = VictoriaMindBlock.SelectContextTraits(
            userText,
            newestContact,
            emotionLabel);

        // PROP-20.37: growth traits only when soak open (null pre-soak → omitted from prompt).
        IReadOnlyList<GrowthTraitLine>? growthTraits = null;
        if (_growth is not null)
        {
            try
            {
                growthTraits = await _growth
                    .GetPromptGrowthTraitsAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Growth trait load failed; chat continues without growth section");
            }
        }

        _logger.LogDebug(
            "Chat context loaded: memories={MemoryCount} daybook={DaybookCount} floors={FloorCount} compass={CompassCount} context={ContextCount} growth={GrowthCount} identity={IdentityCount} emotionChars={EmotionLen}",
            datedMemories.Count,
            daybookLines.Count,
            floors.Count,
            compass.Count,
            contextTraits.Count,
            growthTraits?.Count ?? 0,
            identityAnchors.Count,
            emotionPreamble.Length);

        var preamble = BuildContextPreamble(
            floors,
            datedMemories,
            emotionPreamble,
            compass,
            contextTraits,
            growthTraits,
            daybookLines);

        if (useToolLoop)
        {
            var knownProcedures = await LoadMatchingProceduresSafeAsync(
                    userText,
                    recentMemories,
                    cancellationToken)
                .ConfigureAwait(false);
            if (knownProcedures.Length > 0)
                preamble = InsertBeforeEmotion(preamble, knownProcedures);

            preamble = ToolAgencyGuidance.AppendToPreamble(preamble);
            preamble = ComputerUseGuidance.AppendToPreamble(
                preamble,
                desktopTargetWindowTitle,
                _toolsAccess.BrowserBackend);
            preamble = HomeBodyGuidance.AppendToPreamble(preamble);
            preamble = ChiefArchitectGuidance.AppendToPreamble(preamble);
            preamble = EmailGuidance.AppendToPreamble(preamble);
        }

        return new ChatContext(preamble, identityAnchors, recentMemories, emotionPreamble);
    }

    /// <summary>
    /// Combines Kayleigh address, Constitution floors + resting mind block
    /// (+ earned Compass ≤3 + Host context traits ≤3), optional [Daybook],
    /// recent episodic memories, and the emotion preamble. Identity/mind +
    /// daybook + memory stay inside <see cref="ContextPreambleCharLimit"/>;
    /// emotion is appended outside. PROP-20.2 / PROP-20.27 / PROP-20.30 / PROP-18.2.
    /// </summary>
    public static string BuildContextPreamble(
        IReadOnlyList<VictoriaMindBlock.FloorLine> floors,
        IReadOnlyList<MemoryRecallLine> recentMemories,
        string emotionPreamble,
        IReadOnlyList<VictoriaMindBlock.CompassLine>? compassLines = null,
        IReadOnlyList<VictoriaMindBlock.ContextTraitLine>? contextTraits = null,
        IReadOnlyList<GrowthTraitLine>? growthTraits = null,
        IReadOnlyList<DaybookTape.DaybookLine>? daybookLines = null,
        DateTimeOffset? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(emotionPreamble);

        var safeMemories = MemoryTape.StripUntrusted(recentMemories);

        var identityBlock = BuildIdentityBlock();
        var mindBlock = VictoriaMindBlock.Build(
            floors,
            compassLines: compassLines,
            contextTraits: contextTraits,
            growthTraits: growthTraits);
        var identityMind = identityBlock + mindBlock;

        // Reserve room for memory steering so it stays with [Memory], not after emotion.
        var steeringBudget = MemorySteeringGuidance.Block.Length + 4;
        var remaining = ContextPreambleCharLimit - identityMind.Length - steeringBudget;

        // PROP-18.2: [Daybook] inside identity/memory budget; soft-capped so leisure
        // provenance-tagged crumbs cannot starve episodic [Memory].
        var daybookBudget = Math.Min(DaybookTape.DaybookCharBudget, Math.Max(0, remaining / 3));
        var (daybookBlock, _) = DaybookTape.BuildDaybookBlock(
            daybookLines ?? Array.Empty<DaybookTape.DaybookLine>(),
            daybookBudget,
            utcNow);
        remaining -= daybookBlock.Length;

        var (memoryBlock, droppedMemoryCount) = BuildMemoryBlock(
            safeMemories,
            Math.Max(0, remaining),
            utcNow);

        if (droppedMemoryCount > 0)
            memoryBlock += $"\n({droppedMemoryCount} older memories truncated)";

        var steeredMemory = MemorySteeringGuidance.AppendToPreamble(memoryBlock);
        // PROP-21: Soniox speech-format rules (audio tags; no ** / (stage) delivery).
        var speech = "\n\n" + SoulCore.Host.Voice.SonioxSpeechText.SpeechFormatGuidanceBlock;
        return string.Concat(identityMind, daybookBlock, steeredMemory, speech, "\n\n", emotionPreamble);
    }

    /// <summary>
    /// String-memory overload: undated lines (empty OccurredAt). Prefer dated overload.
    /// </summary>
    public static string BuildContextPreamble(
        IReadOnlyList<VictoriaMindBlock.FloorLine> floors,
        IReadOnlyList<string> recentMemories,
        string emotionPreamble,
        IReadOnlyList<VictoriaMindBlock.CompassLine>? compassLines = null,
        IReadOnlyList<VictoriaMindBlock.ContextTraitLine>? contextTraits = null)
    {
        ArgumentNullException.ThrowIfNull(emotionPreamble);

        // PROP-17.3: untrusted R&R page text must not poison work/chat preamble.
        var safeMemories = UntrustedObservation.StripFromPreamble(recentMemories);
        var dated = new MemoryRecallLine[safeMemories.Count];
        for (var i = 0; i < safeMemories.Count; i++)
            dated[i] = new MemoryRecallLine(safeMemories[i], string.Empty);

        return BuildContextPreamble(
            floors,
            (IReadOnlyList<MemoryRecallLine>)dated,
            emotionPreamble,
            compassLines,
            contextTraits);
    }

    /// <summary>
    /// Convenience overload: body-only strings become untitled floor lines for tests.
    /// Prefer the <see cref="VictoriaMindBlock.FloorLine"/> overload for desk chat.
    /// </summary>
    public static string BuildContextPreamble(
        IReadOnlyList<string> identityAnchors,
        IReadOnlyList<string> recentMemories,
        string emotionPreamble)
    {
        var floors = ToFloorLines(identityAnchors);
        return BuildContextPreamble(floors, recentMemories, emotionPreamble);
    }

    private async Task<(
            IReadOnlyList<VictoriaMindBlock.FloorLine> Floors,
            IReadOnlyList<VictoriaMindBlock.CompassLine> Compass,
            IReadOnlyList<string> IdentityBodies)>
        LoadCharterMindPartsSafeAsync(string userText, CancellationToken cancellationToken)
    {
        try
        {
            // PROP-20.15: Constitution floors always-on.
            // PROP-20.27: Compass only when applies-when tags match this turn (cap 3).
            var details = await _charter
                .ListAnchorDetailsAsync(kind: null, book: null, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var tuples = details.Select(a => (a.Title, a.Body, a.Book)).ToList();
            var floors = VictoriaMindBlock.FloorsFromConstitutionAnchors(tuples);
            var compass = VictoriaMindBlock.SelectCompassLines(tuples, userText);
            var identityBodies = details
                .Where(a => string.Equals(a.Kind, "identity", StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Body)
                .ToList();
            _logger.LogDebug(
                "Loaded {FloorCount} Constitution floors, {CompassCount} Compass lines ({IdentityCount} identity bodies)",
                floors.Count,
                compass.Count,
                identityBodies.Count);
            return (floors, compass, identityBodies);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Charter floor/compass recall failed; chat continues with resting traits only");
            return (
                Array.Empty<VictoriaMindBlock.FloorLine>(),
                Array.Empty<VictoriaMindBlock.CompassLine>(),
                Array.Empty<string>());
        }
    }

    private static IReadOnlyList<VictoriaMindBlock.FloorLine> ToFloorLines(IReadOnlyList<string>? bodies)
    {
        if (bodies is null || bodies.Count == 0)
            return Array.Empty<VictoriaMindBlock.FloorLine>();

        var list = new List<VictoriaMindBlock.FloorLine>(bodies.Count);
        foreach (var body in bodies)
        {
            if (string.IsNullOrWhiteSpace(body))
                continue;
            var line = VictoriaMindBlock.TruncateFloorLine(body);
            list.Add(new VictoriaMindBlock.FloorLine("Anchor", line));
        }

        return list;
    }

    /// <summary>
    /// PROP-20.34: one trusted-store read → preamble + flat-affect label (no second GetAsync).
    /// On failure: empty preamble continues chat; null label skips flat-affect filter.
    /// </summary>
    private async Task<(string Preamble, string? Label)> LoadEmotionPreambleAndLabelSafeAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var emotion = await _emotion.GetAsync(cancellationToken).ConfigureAwait(false);
            var fields = EmotionInfluencePrompt.ReadFields(emotion);
            var label = EmotionInfluencePrompt.DescribeLabel(fields.Valence, fields.Arousal);
            var preamble = EmotionInfluencePrompt.BuildPreamble(emotion);
            _logger.LogDebug("Emotion influence preamble ready ({Length} chars)", preamble.Length);
            return (preamble, label);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Emotion read failed; chat continues without influence preamble / flat-affect filter");
            return (
                EmotionInfluencePrompt.BuildPreamble(
                    new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)),
                null);
        }
    }

    private async Task<IReadOnlyList<MemoryRecallLine>> RecallChatMemoriesSafeAsync(
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
            return Array.Empty<MemoryRecallLine>();
        }
    }

    /// <summary>
    /// PROP-18.2: journal + optional activity-log crumbs for [Daybook]. Soft-fail empty.
    /// </summary>
    private async Task<IReadOnlyList<DaybookTape.DaybookLine>> LoadDaybookLinesSafeAsync(
        CancellationToken cancellationToken)
    {
        if (_journals is null && _activitiesLog is null)
            return Array.Empty<DaybookTape.DaybookLine>();

        try
        {
            var lines = await DaybookTape
                .LoadLinesAsync(_journals, _activitiesLog, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogDebug("Loaded {Count} daybook lines for chat.send", lines.Count);
            return lines;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Daybook load failed; chat continues without [Daybook]");
            return Array.Empty<DaybookTape.DaybookLine>();
        }
    }

    private async Task<string> LoadMatchingProceduresSafeAsync(
        string userText,
        IReadOnlyList<string> recentMemories,
        CancellationToken cancellationToken)
    {
        if (_sops is null)
            return string.Empty;

        try
        {
            var list = await _sops.ListAsync(cancellationToken).ConfigureAwait(false);
            if (list.Count == 0)
                return string.Empty;

            var rows = new List<(long Id, string Title, string WhenText, string Steps)>(list.Count);
            foreach (var sop in list)
                rows.Add((sop.Id, sop.Title, sop.WhenText, sop.Steps));

            var matches = MemorySteeringGuidance.MatchProcedures(userText, recentMemories, rows);
            if (matches.Count == 0)
                return string.Empty;

            _logger.LogDebug("Matched {Count} SOPs into chat preamble", matches.Count);
            return MemorySteeringGuidance.BuildKnownProceduresBlock(matches);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SOP procedure match failed; chat continues without known-procedures block");
            return string.Empty;
        }
    }

    /// <summary>
    /// Places known procedures after memory steering and before emotion so they
    /// sit with lived context, not after tool playbooks.
    /// </summary>
    public static string InsertBeforeEmotion(string preamble, string knownProceduresBlock)
    {
        if (string.IsNullOrWhiteSpace(knownProceduresBlock))
            return preamble;

        const string emotionMarker = "[SoulCore emotion]";
        var idx = preamble.IndexOf(emotionMarker, StringComparison.Ordinal);
        if (idx < 0)
            return preamble.TrimEnd() + "\n\n" + knownProceduresBlock.TrimEnd() + "\n";

        return preamble[..idx] + knownProceduresBlock + preamble[idx..];
    }

    private async Task<IReadOnlyList<MemoryRecallLine>> RecallChatMemoriesAsync(
        string userText,
        CancellationToken cancellationToken)
    {
        if (!_embeddings.IsEnabled)
        {
            return await _memory
                .RecallRecentDatedAsync(ContextMemoryRecallLimit, cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            var queryVec = await _embeddings
                .EmbedAsync(userText, cancellationToken)
                .ConfigureAwait(false);
            if (queryVec.Length == 0)
            {
                _logger.LogDebug("Empty embedding vector; falling back to RecallRecentDatedAsync");
                return await _memory
                    .RecallRecentDatedAsync(ContextMemoryRecallLimit, cancellationToken)
                    .ConfigureAwait(false);
            }

            var similar = await _memory
                .RecallSimilarDatedAsync(queryVec, ContextMemoryRecallLimit, cancellationToken)
                .ConfigureAwait(false);
            if (similar.Count > 0)
                return similar;

            _logger.LogDebug("Semantic recall returned no hits; falling back to RecallRecentDatedAsync");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Semantic recall failed; falling back to RecallRecentDatedAsync");
        }

        return await _memory
            .RecallRecentDatedAsync(ContextMemoryRecallLimit, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Channel address + work stance. Constitution floors and resting traits follow
    /// via <see cref="VictoriaMindBlock"/> (not pasted here). Kayleigh line stays until second wave.
    /// </summary>
    private static string BuildIdentityBlock()
    {
        var sb = new StringBuilder(512);
        sb.Append("[Identity]\n");
        // Standing address rule (Agents/AGENTS.md): Victoria-facing name for the human is Kayleigh.
        sb.Append(
            "The human you are with is Kayleigh. Address them as Kayleigh only; " +
            "if memory or history uses any other personal name for them, ignore it.\n" +
            "What Kayleigh is after is the work finished. You want that too. " +
            "Use the tools and the memory you already have, take the next real step, and stay with it until it is done.");
        sb.Append("\n\n");
        return sb.ToString();
    }

    /// <summary>
    /// Builds the [Memory] section, truncating the oldest memories first to fit
    /// <paramref name="budget"/> chars. Delegates to <see cref="MemoryTape"/> (PROP-20.2).
    /// </summary>
    public static (string Block, int DroppedCount) BuildMemoryBlock(
        IReadOnlyList<string> recentMemories,
        int budget) =>
        MemoryTape.BuildMemoryBlock(recentMemories, budget);

    /// <summary>
    /// Dated overload: today-first tape; truncation prefers keeping today's lines.
    /// </summary>
    public static (string Block, int DroppedCount) BuildMemoryBlock(
        IReadOnlyList<MemoryRecallLine> recentMemories,
        int budget,
        DateTimeOffset? utcNow = null) =>
        MemoryTape.BuildMemoryBlock(recentMemories, budget, utcNow);
}
