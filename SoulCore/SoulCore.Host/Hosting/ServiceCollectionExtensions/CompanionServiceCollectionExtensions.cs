using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Inference;
using SoulCore.Host.Companion;
using SoulCore.Core.Abstractions;
using SoulCore.Host.Loop;
using SoulCore.Host.Ws;
using SoulCore.Inference.Tools.Browser;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Host.Hosting.ServiceCollectionExtensions;

internal static class CompanionServiceCollectionExtensions
{
    internal static IServiceCollection AddCompanion(
        this IServiceCollection services,
        UnrealBridgeOptions unrealOptions)
    {
        services.AddSingleton<PresenceWsHub>();
        services.AddSingleton<IWsFrameAdapter>(sp => sp.GetRequiredService<PresenceWsHub>());
        services.AddSingleton<ICompanionOutboundMessenger, CompanionOutboundMessenger>();
        services.AddSingleton<CompanionCallSessionStore>();
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(5) });
        services.AddSingleton<ComfyUiClient>();
        services.AddSingleton<ICompanionMediaService, CompanionMediaService>();
        services.AddSingleton<ISmsOutboundService>(sp => new SmsOutboundService(
            sp.GetRequiredService<IOptions<SmsOptions>>(),
            sp.GetRequiredService<ILogger<SmsOutboundService>>(),
            sp.GetService<IVictoriaBrowserViewHub>(),
            sp.GetService<IDesktopViewHub>(),
            sp.GetService<IHttpClientFactory>()));
        services.AddSingleton<ISmsInboundService, SmsInboundService>();
        services.AddSingleton<ITool, SendScreenshotMmsTool>();
        services.AddHttpClient("sms-outbound-webhook");
        services.AddSingleton<SoulLoopScaffold>();
        services.AddSingleton<ISoulLoop>(sp => sp.GetRequiredService<SoulLoopScaffold>());
        services.AddHostedService<SoulLoopHostedService>();
        services.AddSingleton<ChatWebSocketHandler>();

        if (unrealOptions.Enabled)
        {
            services.AddSingleton<UnrealVerbClientStub>();
            services.AddSingleton<IUnrealVerbClient>(sp => sp.GetRequiredService<UnrealVerbClientStub>());
            services.AddSingleton<IUnrealEyeCaptureClient>(sp => sp.GetRequiredService<UnrealVerbClientStub>());
            services.AddSingleton<IUnrealCallCameraClient>(sp => sp.GetRequiredService<UnrealVerbClientStub>());
        }
        else
        {
            services.AddSingleton<IUnrealVerbClient, NullUnrealVerbClient>();
            services.AddSingleton<IUnrealEyeCaptureClient, NullUnrealCaptureClient>();
            services.AddSingleton<IUnrealCallCameraClient, NullUnrealCaptureClient>();
        }

        return services;
    }
}
