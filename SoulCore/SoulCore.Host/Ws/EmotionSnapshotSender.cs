using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Protocol;

namespace SoulCore.Host.Ws;

/// <summary>Sends <c>emotion.snapshot</c> frames to a WebSocket client.</summary>
public sealed class EmotionSnapshotSender
{
    private readonly IEmotionState _emotion;
    private readonly ILogger<EmotionSnapshotSender> _logger;

    public EmotionSnapshotSender(IEmotionState emotion, ILogger<EmotionSnapshotSender> logger)
    {
        _emotion = emotion ?? throw new ArgumentNullException(nameof(emotion));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendAsync(
        WebSocket socket,
        string? correlationId,
        CancellationToken cancellationToken,
        string? note = null)
    {
        IReadOnlyDictionary<string, double> emotion;
        long? revision = null;
        try
        {
            emotion = await _emotion.GetAsync(cancellationToken).ConfigureAwait(false);
            revision = await _emotion.GetRevisionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Emotion snapshot unavailable");
            emotion = new Dictionary<string, double>();
        }

        var fields = EmotionInfluencePrompt.ReadFields(emotion);
        var label = EmotionInfluencePrompt.DescribeLabel(fields.Valence, fields.Arousal);

        await WsFrameSender.SendFrameAsync(
            socket,
            SoulCoreFrame.Create(
                SoulCoreFrameTypes.EmotionSnapshot,
                new
                {
                    valence = fields.Valence,
                    arousal = fields.Arousal,
                    dominance = fields.Dominance,
                    focus = fields.Focus,
                    label,
                    note,
                    revision
                },
                id: correlationId),
            cancellationToken).ConfigureAwait(false);
    }
}
