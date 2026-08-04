namespace SoulCore.Host.Voice;

/// <summary>Local faster-whisper STT (multipart WAV → text).</summary>
public interface ISttClient
{
    Task<string> TranscribeAsync(byte[] wavBytes, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>Local Chatterbox TTS (text → WAV bytes).</summary>
public interface ITtsClient
{
    Task<byte[]?> SynthesizeAsync(string text, string? voice = null, CancellationToken cancellationToken = default);
}
