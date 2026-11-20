using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;
using SoulCore.Host.Companion;
using SoulCore.Host.Growth;
using SoulCore.Core.Abstractions;
using SoulCore.Host.Loop;
using SoulCore.Host.Ws;
using SoulCore.Inference.Presence;
using SoulCore.Inference.Tools.Browser;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Memory;

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
        // PROP-20.7: wire tool registry + research store; soft-depend 20.4 mind block when present.
        services.AddSingleton<ISmsInboundService>(sp => new SmsInboundService(
            sp.GetRequiredService<IOptions<SmsOptions>>(),
            sp.GetRequiredService<IOptions<InferenceOptions>>(),
            sp.GetRequiredService<IOptions<ChatWsOptions>>(),
            sp.GetRequiredService<IOptions<CompanionOptions>>(),
            sp.GetRequiredService<IInferenceClient>(),
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<IChatSessionHistoryStore>(),
            sp.GetRequiredService<ICompanionMediaService>(),
            sp.GetRequiredService<PresenceWsHub>(),
            sp.GetRequiredService<ISmsOutboundService>(),
            sp.GetRequiredService<ILogger<SmsInboundService>>(),
            transcript: sp.GetService<IChatTranscriptStore>(),
            lease: sp.GetService<IInferenceLease>(),
            leisure: sp.GetService<ILeisureSession>(),
            toolRegistry: sp.GetService<IToolRegistry>(),
            research: sp.GetService<IVictoriaResearchStore>(),
            mindBlock: sp.GetService<IVictoriaMindBlockBuilder>()));
        services.AddSingleton<ITool, SendScreenshotMmsTool>();
        services.AddHttpClient("sms-outbound-webhook");
        services.AddSingleton<SoulLoopScaffold>();
        services.AddSingleton<ISoulLoop>(sp => sp.GetRequiredService<SoulLoopScaffold>());
        services.AddHostedService<SoulLoopHostedService>();
        services.AddSingleton<WorkTurnGate>();
        services.AddSingleton<IOperatorConsentGate, OperatorConsentGate>();
        services.AddSingleton<IAssignedWorkStore, FileAssignedWorkStore>();
        // PROP-18.8: idle parked-work B3 soft budget (3 advances / 45 min tool time per local day).
        services.AddSingleton<IIdleParkedWorkBudget, FileIdleParkedWorkBudget>();
        // PROP-18.7: activities-log Desc + topic journal residue writers.
        services.AddSingleton<IActivityResidueWriter, ActivityResidueWriter>();
        services.AddSingleton<IInferenceLease, InferenceLease>();
        services.AddSingleton<ILeisureSession, LeisureSession>();
        services.AddSingleton<IBlockedAsideNotice, BlockedAsideNotice>();
        services.AddSingleton<ISurfaceAvailability, SurfaceAvailability>();
        services.AddSingleton<AssignedWorkRunner>();
        services.AddSingleton<LeisureRunner>();
        services.AddHostedService<AssignedWorkHostedService>();
        services.AddHostedService<LeisureHostedService>();
        // PROP-20.4: shared mind-block (floors + resting) for chat + SMS/leisure soft-deps.
        services.AddSingleton<IGrowthService, GrowthService>();
        services.AddSingleton<IVictoriaMindBlockBuilder, VictoriaMindBlockBuilder>();
        services.AddSingleton<IChatContextBuilder, ChatContextBuilder>();
        services.AddSingleton<EmotionSnapshotSender>();
        services.AddSingleton<ChatPostEffectsHandler>();
        services.AddSingleton<VoiceCallHandler>();
        services.AddSingleton<ChatSendHandler>();
        services.AddSingleton<EmotionCorrectHandler>();
        services.AddSingleton<LoopTickHandler>();
        services.AddSingleton<ChatWebSocketSessionRunner>();
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
