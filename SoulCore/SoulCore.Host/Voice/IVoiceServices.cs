namespace SoulCore.Host.Voice;

/// <summary>Soniox STT (WAV/bytes → text). Used by /api/stt and PTT.</summary>
public interface ISttClient
{
    Task<string> TranscribeAsync(byte[] wavBytes, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>Soniox TTS (text → audio bytes, typically PCM or WAV).</summary>
public interface ITtsClient
{
    Task<byte[]?> SynthesizeAsync(string text, string? voice = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream TTS audio chunks as Soniox produces them. Invokes <paramref name="onAudio"/> per chunk.
    /// </summary>
    Task SynthesizeStreamingAsync(
        string text,
        Func<ReadOnlyMemory<byte>, bool, CancellationToken, Task> onAudio,
        string? voice = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Optional Call-session realtime STT bridge.</summary>
public interface IRealtimeSttSessionFactory
{
    Task<IRealtimeSttSession> StartAsync(
        Func<string, bool, CancellationToken, Task> onTranscript,
        CancellationToken cancellationToken = default);
}

public interface IRealtimeSttSession : IAsyncDisposable
{
    Task SendAudioAsync(ReadOnlyMemory<byte> pcmOrEncoded, CancellationToken cancellationToken = default);
    Task FinishAsync(CancellationToken cancellationToken = default);
}
