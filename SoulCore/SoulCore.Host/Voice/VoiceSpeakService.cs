using System.Media;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;

namespace SoulCore.Host.Voice;

/// <summary>
/// PROP-21: Synthesize via Soniox, cache last WAV, play on Host speakers, forward to UE speak.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class VoiceSpeakService : IVoiceSpeakService
{
    private readonly ITtsClient _tts;
    private readonly IUnrealVerbClient _unreal;
    private readonly VoiceOptions _options;
    private readonly ILogger<VoiceSpeakService> _logger;
    private readonly object _gate = new();
    private byte[]? _lastWav;

    public VoiceSpeakService(
        ITtsClient tts,
        IUnrealVerbClient unreal,
        IOptions<VoiceOptions> options,
        ILogger<VoiceSpeakService> logger)
    {
        _tts = tts ?? throw new ArgumentNullException(nameof(tts));
        _unreal = unreal ?? throw new ArgumentNullException(nameof(unreal));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public byte[]? LastWav
    {
        get
        {
            lock (_gate)
                return _lastWav;
        }
    }

    public async Task SpeakAloudAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (!_options.Enabled)
        {
            await _unreal.SpeakAsync(SonioxSpeechText.StripAudioTagsForDisplay(text), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var speakText = SonioxSpeechText.NormalizeForSpeak(text);
        byte[]? wav = null;
        try
        {
            wav = await _tts.SynthesizeAsync(speakText, _options.DefaultVoice, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Soniox TTS synthesize failed — falling back to UE speak text only");
        }

        if (wav is { Length: > 0 })
        {
            lock (_gate)
                _lastWav = wav;

            if (_options.PlayOnHostSpeakers)
                TryPlayOnHost(wav);
        }

        try
        {
            var display = SonioxSpeechText.StripAudioTagsForDisplay(speakText);
            object payload = (_options.PlayInUnreal && wav is { Length: > 0 })
                ? (object)new { text = display, audio_url = _options.LastWavPublicUrl }
                : new { text = display };
            await _unreal.SpeakAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unreal SpeakAsync after TTS failed (Host audio may still have played)");
        }
    }

    /// <summary>Stream TTS chunks (PCM) for Call / desk playback.</summary>
    public Task SpeakStreamingAsync(
        string text,
        Func<ReadOnlyMemory<byte>, bool, CancellationToken, Task> onAudio,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(text))
            return Task.CompletedTask;

        var speakText = SonioxSpeechText.NormalizeForSpeak(text);
        return _tts.SynthesizeStreamingAsync(speakText, onAudio, _options.DefaultVoice, cancellationToken);
    }

    private void TryPlayOnHost(byte[] wav)
    {
        try
        {
            var copy = new MemoryStream(wav, writable: false);
            var player = new SoundPlayer(copy);
            player.Play();
            _logger.LogInformation("Host TTS playback started ({Bytes} bytes)", wav.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Host speaker playback failed");
        }
    }
}
