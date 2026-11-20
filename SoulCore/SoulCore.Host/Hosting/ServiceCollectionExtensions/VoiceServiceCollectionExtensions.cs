using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Host.Voice;

namespace SoulCore.Host.Hosting.ServiceCollectionExtensions;

internal static class VoiceServiceCollectionExtensions
{
    internal static IServiceCollection AddVoice(
        this IServiceCollection services,
        VoiceOptions voiceOptions)
    {
        // PROP-21: Soniox cloud STT/TTS (no local House.Voice Whisper/Kokoro).
        services.AddHttpClient("soniox-api", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<VoiceOptions>>().Value;
            client.BaseAddress = new Uri(opts.ApiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(15, opts.TimeoutSeconds));
            if (opts.HasApiKey)
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey.Trim());
        });

        services.AddSingleton<ISttClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new SonioxSttClient(
                factory.CreateClient("soniox-api"),
                sp.GetRequiredService<IOptions<VoiceOptions>>(),
                sp.GetRequiredService<ILogger<SonioxSttClient>>());
        });
        services.AddSingleton<ITtsClient, SonioxTtsClient>();
        services.AddSingleton<IRealtimeSttSessionFactory, SonioxRealtimeSttSessionFactory>();

        if (voiceOptions.Enabled)
            services.AddSingleton<IVoiceSpeakService, VoiceSpeakService>();
        else
            services.AddSingleton<IVoiceSpeakService, PassthroughVoiceSpeakService>();

        return services;
    }
}
