using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SoulCore.Core.Abstractions;
using SoulCore.Memory;
using SoulCore.Protocol;

namespace SoulCore.Host.Ws;

/// <summary>Handles <c>emotion.correct</c> frames — persist correction + episodic note.</summary>
public sealed class EmotionCorrectHandler
{
    private readonly IEmotionState _emotion;
    private readonly IMemoryStore _memory;
    private readonly EmotionSnapshotSender _emotionSnapshot;
    private readonly ILogger<EmotionCorrectHandler> _logger;

    public EmotionCorrectHandler(
        IEmotionState emotion,
        IMemoryStore memory,
        EmotionSnapshotSender emotionSnapshot,
        ILogger<EmotionCorrectHandler> logger)
    {
        _emotion = emotion ?? throw new ArgumentNullException(nameof(emotion));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _emotionSnapshot = emotionSnapshot ?? throw new ArgumentNullException(nameof(emotionSnapshot));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(WebSocket socket, SoulCoreFrame frame, CancellationToken cancellationToken)
    {
        if (!TryParseEmotionCorrect(frame.Payload, out var components, out var note, out var errorMessage))
        {
            await WsFrameSender.SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new { code = "emotion.invalid", message = errorMessage ?? "emotion.correct payload invalid" },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        long revisionBefore;
        try
        {
            revisionBefore = await _emotion.GetRevisionAsync(cancellationToken).ConfigureAwait(false);
            await _emotion.SetAsync(components, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "emotion.correct persist failed");
            await WsFrameSender.SendFrameAsync(
                socket,
                SoulCoreFrame.Create(
                    SoulCoreFrameTypes.Error,
                    new { code = "emotion.persist_failed", message = "Failed to persist emotion correction." },
                    id: frame.Id),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            try
            {
                var v = components["valence"].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var a = components["arousal"].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var d = components["dominance"].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var f = components["focus"].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var episode =
                    $"[emotion_correction] User corrected felt emotion to " +
                    $"valence={v} arousal={a} dominance={d} focus={f}. " +
                    $"Note: {note.Trim()}";
                await _memory.WriteEpisodicAsync(episode, "correction", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Episodic write after emotion.correct failed (state already persisted)");
            }
        }

        var revisionAfter = await _emotion.GetRevisionAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "emotion.correct applied revision {Before} → {After}",
            revisionBefore,
            revisionAfter);

        await _emotionSnapshot.SendAsync(socket, frame.Id, cancellationToken, note: note).ConfigureAwait(false);
    }

    internal static bool TryParseEmotionCorrect(
        JsonElement? payload,
        out Dictionary<string, double> components,
        out string? note,
        out string? errorMessage)
    {
        components = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        note = null;
        errorMessage = null;

        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
        {
            errorMessage = "emotion.correct payload object required";
            return false;
        }

        var root = payload.Value;
        if (!TryReadRequiredDouble(root, "valence", -1.0, 1.0, out var valence, out errorMessage)
            || !TryReadRequiredDouble(root, "arousal", 0.0, 1.0, out var arousal, out errorMessage)
            || !TryReadRequiredDouble(root, "dominance", 0.0, 1.0, out var dominance, out errorMessage)
            || !TryReadRequiredDouble(root, "focus", 0.0, 1.0, out var focus, out errorMessage))
        {
            return false;
        }

        if (root.TryGetProperty("note", out var noteProp))
        {
            if (noteProp.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                note = null;
            else if (noteProp.ValueKind == JsonValueKind.String)
                note = noteProp.GetString();
            else
            {
                errorMessage = "emotion.correct note must be a string when present";
                return false;
            }
        }

        components["valence"] = valence;
        components["arousal"] = arousal;
        components["dominance"] = dominance;
        components["focus"] = focus;
        return true;
    }

    private static bool TryReadRequiredDouble(
        JsonElement root,
        string name,
        double min,
        double max,
        out double value,
        out string? errorMessage)
    {
        value = 0;
        errorMessage = null;
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Number)
        {
            errorMessage = $"emotion.correct payload.{name} required (number)";
            return false;
        }

        if (!prop.TryGetDouble(out value) || double.IsNaN(value) || double.IsInfinity(value))
        {
            errorMessage = $"emotion.correct payload.{name} must be a finite number";
            return false;
        }

        if (value < min || value > max)
        {
            errorMessage =
                $"emotion.correct payload.{name} out of range [{min.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {max.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
            return false;
        }

        return true;
    }
}
