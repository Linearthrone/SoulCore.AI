using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Host.Voice;

namespace SoulCore.Host.Hosting.ServiceCollectionExtensions;

internal static class VoiceServiceCollectionExtensions
{
    internal static IServiceCollection AddVoice(
        this IServiceCollection services,
        VoiceOptions voiceOptions)
    {
        // Voice: local Whisper STT + Chatterbox TTS (House.Voice satellites).
        services.AddHttpClient("voice-stt", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<VoiceOptions>>().Value;
            client.BaseAddress = new Uri(opts.SttUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds));
        });
        services.AddHttpClient("voice-tts", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<VoiceOptions>>().Value;
            client.BaseAddress = new Uri(opts.TtsUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds));
        });
        services.AddSingleton<ISttClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new WhisperSttClient(
                factory.CreateClient("voice-stt"),
                sp.GetRequiredService<IOptions<VoiceOptions>>(),
                sp.GetRequiredService<ILogger<WhisperSttClient>>());
        });
        services.AddSingleton<ITtsClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new ChatterboxTtsClient(
                factory.CreateClient("voice-tts"),
                sp.GetRequiredService<IOptions<VoiceOptions>>(),
                sp.GetRequiredService<ILogger<ChatterboxTtsClient>>());
        });
        if (voiceOptions.Enabled)
        {
            services.AddSingleton<IVoiceSpeakService, VoiceSpeakService>();
        }
        else
        {
            services.AddSingleton<IVoiceSpeakService, PassthroughVoiceSpeakService>();
        }

        return services;
    }
}
