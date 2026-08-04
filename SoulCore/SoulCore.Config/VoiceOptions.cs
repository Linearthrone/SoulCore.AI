namespace SoulCore.Config;

/// <summary>
/// Local STT (faster-whisper) + TTS (Chatterbox) knobs. Prefer loopback; $0 cloud.
/// </summary>
public sealed class VoiceOptions
{
    public const string SectionName = "Voice";

    /// <summary>When false, STT/TTS APIs no-op and speak stays UE-log-only.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>faster-whisper base URL (no trailing slash).</summary>
    public string SttUrl { get; set; } = "http://127.0.0.1:8000";

    /// <summary>Chatterbox TTS base URL (no trailing slash).</summary>
    public string TtsUrl { get; set; } = "http://127.0.0.1:8881";

    /// <summary>Default Chatterbox voice name (Media/ChatterboxVoices/{name}.wav).</summary>
    public string DefaultVoice { get; set; } = "default";

    /// <summary>Play synthesized WAV on the Host PC speakers after speak.</summary>
    public bool PlayOnHostSpeakers { get; set; } = true;

    /// <summary>
    /// When true, speak wire includes <c>audio_url</c> so UE can download and play
    /// the last clip in-world (PIE). Requires Host <c>/api/voice/last.wav</c>.
    /// </summary>
    public bool PlayInUnreal { get; set; } = true;

    /// <summary>
    /// Public URL UE should fetch for the last WAV (loopback when UE and Host share a machine).
    /// </summary>
    public string LastWavPublicUrl { get; set; } = "http://127.0.0.1:7700/api/voice/last.wav";

    public int TimeoutSeconds { get; set; } = 60;
}
