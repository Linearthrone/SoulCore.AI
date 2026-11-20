using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Growth;
using SoulCore.Host.Growth;
using SoulCore.Memory;

namespace SoulCore.Host.Companion;

/// <summary>
/// PROP-20.4 / 20.15 / 20.27 / 20.30 / 20.37 shared mind-block for SMS/leisure soft-deps:
/// Constitution floors + resting Mid defaults always-on; Compass + context traits
/// only when earned (each cap 3; context also needs 24h-fresh contact);
/// growth traits only when soak may change prompt.
/// </summary>
public sealed class VictoriaMindBlockBuilder : IVictoriaMindBlockBuilder
{
    private readonly ICharter _charter;
    private readonly IMemoryStore? _memory;
    private readonly IEmotionState? _emotion;
    private readonly IGrowthService? _growth;

    public VictoriaMindBlockBuilder(
        ICharter charter,
        IMemoryStore? memory = null,
        IEmotionState? emotion = null,
        IGrowthService? growth = null)
    {
        _charter = charter ?? throw new ArgumentNullException(nameof(charter));
        _memory = memory;
        _emotion = emotion;
        _growth = growth;
    }

    /// <inheritdoc />
    public async Task<string?> TryBuildAsync(
        string? matchText = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var details = await _charter
                .ListAnchorDetailsAsync(kind: null, book: null, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var tuples = details.Select(a => (a.Title, a.Body, a.Book)).ToList();
            var floors = VictoriaMindBlock.FloorsFromConstitutionAnchors(tuples);
            var compass = VictoriaMindBlock.SelectCompassLines(tuples, matchText);
            var newestContact = await ResolveNewestContactUtcAsync(cancellationToken).ConfigureAwait(false);
            var emotionLabel = await ResolveEmotionLabelAsync(cancellationToken).ConfigureAwait(false);
            var context = VictoriaMindBlock.SelectContextTraits(
                matchText,
                newestContact,
                emotionLabel);
            IReadOnlyList<GrowthTraitLine>? growth = null;
            if (_growth is not null)
            {
                try
                {
                    growth = await _growth
                        .GetPromptGrowthTraitsAsync(cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    growth = null;
                }
            }

            return VictoriaMindBlock.Build(
                floors,
                compassLines: compass,
                contextTraits: context,
                growthTraits: growth);
        }
        catch
        {
            // Still emit resting Mid defaults so callers can speak as her.
            return VictoriaMindBlock.Build(Array.Empty<VictoriaMindBlock.FloorLine>());
        }
    }

    private async Task<DateTimeOffset?> ResolveNewestContactUtcAsync(CancellationToken cancellationToken)
    {
        if (_memory is null)
            return null;

        try
        {
            var recent = await _memory
                .RecallRecentDatedAsync(limit: 1, cancellationToken)
                .ConfigureAwait(false);
            return VictoriaMindBlock.NewestContactUtc(recent.Select(r => (string?)r.OccurredAt));
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> ResolveEmotionLabelAsync(CancellationToken cancellationToken)
    {
        if (_emotion is null)
            return null;

        try
        {
            var emotion = await _emotion.GetAsync(cancellationToken).ConfigureAwait(false);
            var fields = EmotionInfluencePrompt.ReadFields(emotion);
            return EmotionInfluencePrompt.DescribeLabel(fields.Valence, fields.Arousal);
        }
        catch
        {
            return null;
        }
    }
}
