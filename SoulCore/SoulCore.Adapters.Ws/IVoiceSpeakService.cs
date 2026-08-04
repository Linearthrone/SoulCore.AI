namespace SoulCore.Adapters.Ws;

/// <summary>
/// Host-side audible speak: TTS + optional PC speakers + Unreal speak.
/// Implemented by Host <c>VoiceSpeakService</c>; tools may call this instead of raw SpeakAsync.
/// </summary>
public interface IVoiceSpeakService
{
    byte[]? LastWav { get; }

    Task SpeakAloudAsync(string text, CancellationToken cancellationToken = default);
}
