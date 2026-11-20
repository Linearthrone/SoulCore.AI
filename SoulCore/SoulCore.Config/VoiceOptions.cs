namespace SoulCore.Config;

/// <summary>
/// PROP-21: Soniox cloud STT/TTS. Key from <c>SONIOX_API_KEY</c> / <c>SOULCORE_Voice__ApiKey</c>.
/// </summary>
public sealed class VoiceOptions
{
    public const string SectionName = "Voice";

    /// <summary>When false, STT/TTS APIs no-op and speak stays UE-log-only.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Soniox project API key. Prefer env; never log.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>REST base for async STT file upload + transcriptions.</summary>
    public string ApiBaseUrl { get; set; } = "https://api.soniox.com";

    /// <summary>Realtime STT WebSocket.</summary>
    public string SttWsUrl { get; set; } = "wss://stt-rt.soniox.com/transcribe-websocket";

    /// <summary>Realtime TTS WebSocket.</summary>
    public string TtsWsUrl { get; set; } = "wss://tts-rt.soniox.com/tts-websocket";

    /// <summary>Async STT model (file / PTT).</summary>
    public string SttAsyncModel { get; set; } = "stt-async-v4";

    /// <summary>Realtime STT model (Call).</summary>
    public string SttRealtimeModel { get; set; } = "stt-rt-v6";

    /// <summary>Realtime TTS model.</summary>
    public string TtsModel { get; set; } = "tts-rt-v2";

    /// <summary>Soniox catalog voice (English female default for Victoria).</summary>
    public string DefaultVoice { get; set; } = "Rebecca";

    public string TtsLanguage { get; set; } = "en";

    public string TtsAudioFormat { get; set; } = "pcm_s16le";

    public int TtsSampleRate { get; set; } = 24000;

    /// <summary>Play synthesized audio on the Host PC speakers after speak (non-Call).</summary>
    public bool PlayOnHostSpeakers { get; set; } = true;

    /// <summary>
    /// When true, speak wire includes <c>audio_url</c> so UE can download and play
    /// the last clip in-world (PIE). Requires Host <c>/api/voice/last.wav</c>.
    /// </summary>
    public bool PlayInUnreal { get; set; } = true;

    public string LastWavPublicUrl { get; set; } = "http://127.0.0.1:7710/api/voice/last.wav";

    public int TimeoutSeconds { get; set; } = 90;

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
