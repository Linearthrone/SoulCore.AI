using Microsoft.Extensions.Logging;
using SoulCore.Adapters.Ws;
using SoulCore.Core;
using SoulCore.Core.Abstractions;
using SoulCore.Core.Safety;
using SoulCore.Inference.Tools.Body;

namespace SoulCore.Host.Ws;

/// <summary>
/// Post-chat Unreal side effects: speak, emotion, loco/animation/look keyword fallbacks.
/// Strategy A suppresses keyword fallback when the model already called a tool in the same verb class.
/// </summary>
public sealed class ChatPostEffectsHandler
{
    private readonly IEmotionState _emotion;
    private readonly IUnrealVerbClient _unreal;
    private readonly IVoiceSpeakService? _voiceSpeak;
    private readonly DriftWatcher _driftWatcher;
    private readonly ILogger<ChatPostEffectsHandler> _logger;

    public ChatPostEffectsHandler(
        IEmotionState emotion,
        IUnrealVerbClient unreal,
        DriftWatcher driftWatcher,
        ILogger<ChatPostEffectsHandler> logger,
        IVoiceSpeakService? voiceSpeak = null)
    {
        _emotion = emotion ?? throw new ArgumentNullException(nameof(emotion));
        _unreal = unreal ?? throw new ArgumentNullException(nameof(unreal));
        _driftWatcher = driftWatcher ?? throw new ArgumentNullException(nameof(driftWatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _voiceSpeak = voiceSpeak;
    }

    public async Task ApplyAsync(
        string originalUserText,
        string reply,
        IReadOnlySet<string> dispatchedToolNames,
        CancellationToken cancellationToken = default)
    {
        var driftStatus = _driftWatcher.GetStatus();
        if (driftStatus.SloExceeded)
        {
            var oldestAge = driftStatus.OldestDriftReport is null
                ? TimeSpan.Zero
                : DateTimeOffset.UtcNow - driftStatus.OldestDriftReport.ObservedAt;
            _logger.LogWarning(
                "Drift SLO exceeded — Unreal verbs soft-blocked (unacked={Unacked}, oldestAge={OldestAge})",
                driftStatus.UnackedReports,
                oldestAge);
            return;
        }

        // Best-effort CT — client abort after chat.done must not skip speak/emotion.
        var sideEffectCt = CancellationToken.None;

        await ApplySpeakAndEmotionAsync(reply, sideEffectCt).ConfigureAwait(false);
        await ApplyLocoIntentAsync(originalUserText, dispatchedToolNames, sideEffectCt).ConfigureAwait(false);
        await ApplyAnimationIntentAsync(originalUserText, dispatchedToolNames, sideEffectCt).ConfigureAwait(false);
        await ApplyLookIntentAsync(originalUserText, dispatchedToolNames, sideEffectCt).ConfigureAwait(false);
    }

    private async Task ApplySpeakAndEmotionAsync(string reply, CancellationToken sideEffectCt)
    {
        try
        {
            var emotion = await _emotion.GetAsync(sideEffectCt).ConfigureAwait(false);
            var fields = EmotionInfluencePrompt.ReadFields(emotion);
            await _unreal.SetEmotionAsync(new
            {
                valence = fields.Valence,
                arousal = fields.Arousal,
                dominance = fields.Dominance
            }, sideEffectCt).ConfigureAwait(false);

            var spoke = false;
            if (_voiceSpeak is not null)
            {
                await _voiceSpeak.SpeakAloudAsync(reply, sideEffectCt).ConfigureAwait(false);
                spoke = true;
            }
            else
            {
                spoke = await _unreal.SpeakAsync(reply, sideEffectCt).ConfigureAwait(false);
            }

            if (spoke)
            {
                _logger.LogInformation("Post-chat SpeakAsync succeeded (replyLen={ReplyLen})", reply.Length);
            }
            else if (!_unreal.IsConnected)
            {
                _logger.LogInformation("Post-chat SpeakAsync attempted — Unreal not connected (no-op)");
            }
            else
            {
                _logger.LogInformation("Post-chat SpeakAsync returned false (connected but speak failed)");
            }
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogDebug(oce, "Post-chat SpeakAsync/emotion side-effect cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unreal verb side-effect ignored");
        }
    }

    private async Task ApplyLocoIntentAsync(
        string originalUserText,
        IReadOnlySet<string> dispatchedToolNames,
        CancellationToken sideEffectCt)
    {
        try
        {
            if (ToolClassFiredThisTurn(dispatchedToolNames, ToolVerbClass.Loco))
            {
                _logger.LogDebug(
                    "Loco keyword fallback skipped — model called a loco-class tool this turn (strategy A). Tools={Tools}",
                    string.Join(",", dispatchedToolNames));
                return;
            }

            var locoIntent = DetectLocoIntent(originalUserText);
            if (locoIntent is null)
                return;

            await _unreal.LocoAsync(new
            {
                forward = locoIntent.Forward,
                right = locoIntent.Right,
                up = locoIntent.Up
            }, sideEffectCt).ConfigureAwait(false);
            _logger.LogInformation(
                "Unreal loco intent dispatched: forward={Forward} right={Right} up={Up} (from chat text)",
                locoIntent.Forward, locoIntent.Right, locoIntent.Up);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unreal loco side-effect ignored");
        }
    }

    private async Task ApplyAnimationIntentAsync(
        string originalUserText,
        IReadOnlySet<string> dispatchedToolNames,
        CancellationToken sideEffectCt)
    {
        try
        {
            if (ToolClassFiredThisTurn(dispatchedToolNames, ToolVerbClass.Animation))
            {
                _logger.LogDebug(
                    "Animation keyword fallback skipped — model called an animation-class tool this turn (strategy A). Tools={Tools}",
                    string.Join(",", dispatchedToolNames));
                return;
            }

            var animationName = DetectAnimationIntent(originalUserText);
            if (animationName is null)
                return;

            await _unreal.PlayAnimationAsync(animationName, sideEffectCt).ConfigureAwait(false);
            _logger.LogInformation(
                "Unreal animation intent dispatched: anim={AnimationName} (from chat text)",
                animationName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unreal animation side-effect ignored");
        }
    }

    private async Task ApplyLookIntentAsync(
        string originalUserText,
        IReadOnlySet<string> dispatchedToolNames,
        CancellationToken sideEffectCt)
    {
        try
        {
            if (ToolClassFiredThisTurn(dispatchedToolNames, ToolVerbClass.Look))
            {
                _logger.LogDebug(
                    "Look keyword fallback skipped — model called a look-class tool this turn (strategy A). Tools={Tools}",
                    string.Join(",", dispatchedToolNames));
                return;
            }

            if (!DetectLookIntent(originalUserText))
                return;

            await _unreal.LookAsync(null!, sideEffectCt).ConfigureAwait(false);
            _logger.LogInformation("Unreal look intent dispatched: look_at_player (from chat text)");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unreal look side-effect ignored");
        }
    }

    internal enum ToolVerbClass
    {
        Loco,
        Animation,
        Look
    }

    internal static bool ToolClassFiredThisTurn(IReadOnlySet<string>? dispatchedToolNames, ToolVerbClass verbClass)
    {
        if (dispatchedToolNames is null || dispatchedToolNames.Count == 0)
            return false;

        foreach (var name in dispatchedToolNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var n = name.ToLowerInvariant();
            switch (verbClass)
            {
                case ToolVerbClass.Loco:
                    if (n.Contains("move", StringComparison.Ordinal)
                        || n.Contains("walk", StringComparison.Ordinal)
                        || n.Contains("loco", StringComparison.Ordinal)
                        || n.Contains("go_to", StringComparison.Ordinal))
                        return true;
                    break;
                case ToolVerbClass.Animation:
                    if (n.Contains("animation", StringComparison.Ordinal)
                        || n.Contains("animate", StringComparison.Ordinal)
                        || n.Contains("wave", StringComparison.Ordinal)
                        || n.Contains("play_anim", StringComparison.Ordinal))
                        return true;
                    break;
                case ToolVerbClass.Look:
                    if (n.Contains("look", StringComparison.Ordinal)
                        || n.Contains("gaze", StringComparison.Ordinal)
                        || n.Contains("face", StringComparison.Ordinal))
                        return true;
                    break;
            }
        }
        return false;
    }

    internal static LocoIntent? DetectLocoIntent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = text.ToLowerInvariant()
            .Replace("foreward", "forward", StringComparison.Ordinal)
            .Replace("foward", "forward", StringComparison.Ordinal);

        var stepCm = ParseLocoDistanceCm(normalized) ?? 200.0;

        if (ContainsAny(normalized, "turn left"))
            return new LocoIntent(0, -stepCm, 0);
        if (ContainsAny(normalized, "turn right"))
            return new LocoIntent(0, stepCm, 0);
        if (ContainsAny(normalized, "go back", "step back", "walk back", "move back", "backward", "backwards"))
            return new LocoIntent(-stepCm, 0, 0);
        if (ContainsAny(normalized, "go forward", "step forward", "walk forward", "move forward"))
            return new LocoIntent(stepCm, 0, 0);

        if (ContainsAny(normalized, "step", "walk", "move", "forward", "go"))
            return new LocoIntent(stepCm, 0, 0);
        if (ContainsAny(normalized, "back"))
            return new LocoIntent(-stepCm, 0, 0);
        if (ContainsAny(normalized, "left"))
            return new LocoIntent(0, -stepCm, 0);
        if (ContainsAny(normalized, "right"))
            return new LocoIntent(0, stepCm, 0);

        return null;
    }

    internal static double? ParseLocoDistanceCm(string normalized)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"(\d+(?:\.\d+)?)\s*(feet|foot|ft|meters|meter|m|cm)\b",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (!match.Success)
            return null;

        if (!double.TryParse(
                match.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount) ||
            amount <= 0)
        {
            return null;
        }

        const double maxCm = 2000.0;
        var unit = match.Groups[2].Value;
        var cm = unit switch
        {
            "cm" => amount,
            "m" or "meter" or "meters" => amount * 100.0,
            _ => amount * 30.48
        };
        return Math.Min(cm, maxCm);
    }

    internal static string? DetectAnimationIntent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = text.ToLowerInvariant();

        if (ContainsAny(normalized, "wave hello", "wave goodbye", "wave bye"))
            return "wave";
        if (ContainsAny(normalized, "shake head", "shake your head"))
            return "shake_head";
        if (ContainsAny(normalized, "thumbs up", "thumbs-up"))
            return "thumbs_up";
        if (ContainsAny(normalized, "sit down"))
            return "sit";
        if (ContainsAny(normalized, "stand up"))
            return "stand";
        if (ContainsAny(normalized, "point at"))
            return "point";

        if (ContainsAny(normalized, "wave"))
            return "wave";
        if (ContainsAny(normalized, "nod", "yes"))
            return "nod";
        if (ContainsAny(normalized, "no"))
            return "shake_head";
        if (ContainsAny(normalized, "bow"))
            return "bow";
        if (ContainsAny(normalized, "clap", "applaud"))
            return "clap";
        if (ContainsAny(normalized, "dance"))
            return "dance";
        if (ContainsAny(normalized, "laugh", "giggle"))
            return "laugh";
        if (ContainsAny(normalized, "point"))
            return "point";
        if (ContainsAny(normalized, "jump"))
            return "jump";
        if (ContainsAny(normalized, "sit"))
            return "sit";
        if (ContainsAny(normalized, "stand"))
            return "stand";

        return null;
    }

    internal static bool DetectLookIntent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.ToLowerInvariant();

        if (ContainsAny(normalized, "look at me", "look at player", "look at", "face me",
            "turn to me", "watch me", "see me"))
            return true;

        if (ContainsAny(normalized, "look", "gaze"))
            return true;

        return false;
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (haystack.Contains(needle, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    internal sealed record LocoIntent(double Forward, double Right, double Up);
}
